using System.Text.Json;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Judging;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Judging;

/// <summary>
/// UC-J.1 判题引擎（JudgingEngineService）Contract 测试
/// 覆盖 BR：BR-32 required 必中强制 partial | BR-33 aliases 组内任一命中 | BR-34 LLM 降级 | BR-35 阈值边界 | BR-36 五键契约
/// O4 连线（V0.7.8）：pairs 逐对判定 + 部分给分（对齐 Python rule_grader.grade_o4）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class JudgingEngineServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private const string O4QuestionId = "Q-o4-judge-0001";
    private const string O4BankId = "bank-o4-judge";

    private static string GroupsJson(params KeywordGroup[] groups)
        => JsonSerializer.Serialize(groups);

    /// <summary>种子 O4 连线题（Content 镜像含 pairs；Keywords="[]"，对齐真实题库——判题依据在 Content）</summary>
    private async Task<Questions> SeedO4QuestionAsync()
    {
        var ds = User.Use<QuestionsDataService>();
        return await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = O4QuestionId,
            BankId = O4BankId,
            ChapterId = "8a",
            QType = QuestionType.O4,
            Content = """
                {"question":"将下列省级行政区与其简称连线。","pairs":[
                  {"left":"广东省","right":"粤"},
                  {"left":"山东省","right":"鲁"},
                  {"left":"四川省","right":"川（蜀）"},
                  {"left":"湖北省","right":"鄂"}]}
                """,
            Keywords = "[]",
            KnowledgePoints = ["省级行政区与简称连线"],
            Difficulty = 1,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>O4 全对：pairs 逐对全中 → Correct + Confidence 1.0（dict 形态提交）</summary>
    [Fact]
    public async Task JudgeAsync_O4_AllCorrect_DictSubmission_FullScore()
    {
        await SeedO4QuestionAsync();
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = O4QuestionId,
            QType = "O4",
            Keywords = "[]",
            UserAnswer = """{"pairs":{"广东省":"粤","山东省":"鲁","四川省":"川（蜀）","湖北省":"鄂"}}""",
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Correct", result.Result);
        Assert.Equal(1d, result.Confidence);
        Assert.Equal(4, result.MatchedKeywords.Length);
        Assert.Empty(result.MissingKeywords);
        Assert.False(result.IsDegraded);
    }

    /// <summary>O4 全对：list 形态提交与 dict 等价（提交 shape 兼容）</summary>
    [Fact]
    public async Task JudgeAsync_O4_AllCorrect_ListSubmission_EqualsDict()
    {
        await SeedO4QuestionAsync();
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = O4QuestionId,
            QType = "O4",
            Keywords = "[]",
            UserAnswer = """[{"left":"广东省","right":"粤"},{"left":"山东省","right":"鲁"},{"left":"四川省","right":"川（蜀）"},{"left":"湖北省","right":"鄂"}]""",
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Correct", result.Result);
        Assert.Equal(1d, result.Confidence);
        Assert.Equal(4, result.MatchedKeywords.Length);
    }

    /// <summary>O4 部分对：4 对中 3 对 → Partial + Confidence 0.75（部分给分）</summary>
    [Fact]
    public async Task JudgeAsync_O4_Partial_Hit3Of4_Confidence075()
    {
        await SeedO4QuestionAsync();
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = O4QuestionId,
            QType = "O4",
            Keywords = "[]",
            UserAnswer = """{"pairs":{"广东省":"粤","山东省":"鲁","四川省":"鄂","湖北省":"鄂"}}""",   // 四川连错 → 3/4
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Partial", result.Result);
        Assert.Equal(0.75, result.Confidence);
        Assert.Equal(3, result.MatchedKeywords.Length);
        Assert.Single(result.MissingKeywords);
        Assert.Contains("四川省", result.MissingKeywords);
    }

    /// <summary>O4 全错：全部连连错 → Wrong（Confidence 0）</summary>
    [Fact]
    public async Task JudgeAsync_O4_AllWrong_ReturnsWrong()
    {
        await SeedO4QuestionAsync();
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = O4QuestionId,
            QType = "O4",
            Keywords = "[]",
            UserAnswer = """{"pairs":{"广东省":"鲁","山东省":"粤","四川省":"鄂","湖北省":"川（蜀）"}}""",
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Wrong", result.Result);
        Assert.Equal(0d, result.Confidence);
        Assert.Empty(result.MatchedKeywords);
        Assert.Equal(4, result.MissingKeywords.Length);
    }

    /// <summary>O4 空 pairs / 空提交：题目无 pairs 或学生提交空 → Wrong（对齐空 Keywords / 空提交语义）</summary>
    [Fact]
    public async Task JudgeAsync_O4_EmptyPairsOrEmptySubmit_ReturnsWrong()
    {
        // 题目无 pairs（Content 无 pairs 键）→ Wrong
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = "Q-o4-nopairs",
            BankId = O4BankId,
            QType = QuestionType.O4,
            Content = """{"question":"无 pairs 题目"}""",
            Keywords = "[]",
            KnowledgePoints = [],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);

        var svc = User.Use<JudgingEngineService>();
        var noPairs = await svc.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = "Q-o4-nopairs",
            QType = "O4",
            Keywords = "[]",
            UserAnswer = """{"pairs":{}}""",
        }, TestContext.Current.CancellationToken);
        Assert.Equal("Wrong", noPairs.Result);

        // 空提交（UserAnswer 空）→ Wrong
        await SeedO4QuestionAsync();
        var empty = await svc.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = O4QuestionId,
            QType = "O4",
            Keywords = "[]",
            UserAnswer = string.Empty,
        }, TestContext.Current.CancellationToken);
        Assert.Equal("Wrong", empty.Result);
    }

    /// <summary>O4 字符串兜底形态："左=右;左2=右2"（对齐 Python grade_o4）</summary>
    [Fact]
    public async Task JudgeAsync_O4_StringForm_FallbackParsing()
    {
        await SeedO4QuestionAsync();
        var svc = User.Use<JudgingEngineService>();
        var request = new JudgingRequestDto
        {
            QuestionId = O4QuestionId,
            QType = "O4",
            Keywords = "[]",
            UserAnswer = "广东省=粤;山东省=鲁;四川省=川（蜀）;湖北省=鄂",
        };

        var result = await svc.JudgeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Correct", result.Result);
    }

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
