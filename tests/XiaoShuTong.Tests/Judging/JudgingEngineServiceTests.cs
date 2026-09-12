using System.Text.Json;
using XiaoShuTong.Services.Judging;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Judging;

/// <summary>
/// UC-J.1 判题引擎（JudgingEngineService）Contract 测试
/// 覆盖 BR：BR-32 required 必中强制 partial | BR-33 aliases 组内任一命中 | BR-34 LLM 降级 | BR-35 阈值边界 | BR-36 五键契约
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class JudgingEngineServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private static string GroupsJson(params KeywordGroup[] groups)
        => JsonSerializer.Serialize(groups);

    /// <summary>BR-33：aliases 组内任一命中即该组命中（"李世民"命中 → "唐太宗"组命中）</summary>
    [Fact]
    public async Task JudgeAsync_AliasAnyHit_GroupHit()
    {
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = "Q-1",
            QType = "O5",
            Keywords = GroupsJson(new KeywordGroup(["唐太宗", "李世民"], 1d, false)),
            UserAnswer = "李世民开创贞观之治",
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Correct", result.Result); // 组命中（别名李世民）→ 命中率 1.0
        Assert.Equal(1.0, result.Confidence);
        Assert.Contains(result.MatchedKeywords, m => m.Contains("唐太宗"));
    }

    /// <summary>BR-32：required 必中要点未命中 → 强制 Partial（即使命中率 0.9）</summary>
    [Fact]
    public async Task JudgeAsync_RequiredMissed_ForcedPartial()
    {
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = "Q-2",
            QType = "O5",
            Keywords = GroupsJson(
                new KeywordGroup(["东临碣石"], 1d, true),       // required：必须命中
                new KeywordGroup(["以观沧海"], 1d, false),
                new KeywordGroup(["水何澹澹"], 1d, false)),
            UserAnswer = "东临碣石 以观沧海 水何澹澹 山岛竦峙",  // 全部命中（1.0），但缺 required？——不，东临碣石命中了
        };
        // 修正：缺 required 的用例
        var request2 = request with
        {
            UserAnswer = "以观沧海 水何澹澹", // 漏掉 required 组"东临碣石"
        };

        var result = await svc.JudgeAsync(request2, TestContext.Current.CancellationToken);

        Assert.Equal("Partial", result.Result); // required 未命中 → 强制 Partial
        Assert.True(result.Confidence < 0.85);
    }

    /// <summary>BR-35：阈值边界——0.9 → Correct；0.7 → Partial；0.4 → Wrong</summary>
    [Fact]
    public async Task JudgeAsync_Thresholds_CorrectPartialWrong()
    {
        var svc = User.Use<JudgingEngineService>();
        // 10 组关键词：命中 9 → 0.9
        var nineOfTen = Enumerable.Range(0, 10).Select(i => new KeywordGroup([$"kw{i}"], 1d, false)).ToArray();
        var high = await svc.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = "Q-3",
            Keywords = GroupsJson(nineOfTen),
            UserAnswer = string.Join(" ", Enumerable.Range(0, 9).Select(i => $"kw{i}")),
        }, TestContext.Current.CancellationToken);
        Assert.Equal("Correct", high.Result);

        // 命中 7 → 0.7 → Partial
        var mid = await svc.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = "Q-4",
            Keywords = GroupsJson(nineOfTen),
            UserAnswer = string.Join(" ", Enumerable.Range(0, 7).Select(i => $"kw{i}")),
        }, TestContext.Current.CancellationToken);
        Assert.Equal("Partial", mid.Result);

        // 命中 4 → 0.4 → Wrong
        var low = await svc.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = "Q-5",
            Keywords = GroupsJson(nineOfTen),
            UserAnswer = string.Join(" ", Enumerable.Range(0, 4).Select(i => $"kw{i}")),
        }, TestContext.Current.CancellationToken);
        Assert.Equal("Wrong", low.Result);
    }

    /// <summary>BR-34：LLM 失败 → 降级本地规则 + 降级标记（不判错保守处理）</summary>
    [Fact]
    public async Task JudgeAsync_LlmFailed_DegradedToLocal()
    {
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = "Q-6",
            QType = "X1",
            Keywords = GroupsJson(new KeywordGroup(["关键要点"], 1d, false)),
            UserAnswer = "关键要点相关作答",
            LlmFailed = true, // LLM 超时/失败
            PreferLlm = true,
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsDegraded);
        Assert.True(result.Result is "Correct" or "Partial"); // 降级保守，不轻易判错
        Assert.InRange(result.Confidence, 0d, 1d);
    }

    /// <summary>BR-36：五键契约输出（所有分支结构一致）</summary>
    [Fact]
    public async Task JudgeAsync_OutputsFiveKeyContract()
    {
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = "Q-7",
            QType = "O5",
            Keywords = GroupsJson(new KeywordGroup(["甲"], 1d, false), new KeywordGroup(["乙"], 1d, false)),
            UserAnswer = "甲",
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(result.Result));
        Assert.InRange(result.Confidence, 0d, 1d);
        Assert.NotNull(result.MatchedKeywords);
        Assert.NotNull(result.MissingKeywords);
        Assert.NotNull(result.Hint);
        Assert.Contains(result.MatchedKeywords, m => m.Contains("甲"));
        Assert.Contains(result.MissingKeywords, m => m.Contains("乙"));
    }
}
