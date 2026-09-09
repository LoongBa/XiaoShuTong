using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.3 请求提示（GetHintService）Contract 测试
/// 覆盖 BR：BR-28 题目必须存在 → 1502 | BR-29 提示 ≤20 字、不给答案 | BR-30 难度档按状态路由
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetHintServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private static void RegisterQuestion(string questionId)
    {
        LearningQuestionRegistry.Register(new LearningQuestionMeta(
            QuestionId: questionId,
            BankId: "bank-ch-7a",
            Subject: "chinese",
            KnowledgePoint: "岳阳楼记-背诵",
            QType: "R1",
            AnswerKeywords: ["若出其中", "星汉灿烂"],
            Hint: "首字：若"));
    }

    private async Task SeedStateAsync(long userId, string questionId, MemoryState state)
    {
        var ds = User.Use<MemoryStatesDataService>();
        await ds.EntityCreateAsync(new MemoryStates
        {
            UserId = userId,
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            State = state,
            ConsecutiveCorrect = 0,
            HistoryAccuracy = 0.8,
            NextReviewAt = DateTime.UtcNow.AddDays(1),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-28：题目不存在 → QUESTION_NOT_IN_BANK</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownQuestion_ReturnsQuestionNotInBank()
    {
        SetUser(43001);
        var svc = User.Use<GetHintService>();

        var result = await svc.ExecuteAsync(new GetHintReqDto { QuestionId = "Q-NOT-EXIST-43001" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.QuestionNotInBank, result.ErrorCode);
    }

    /// <summary>BR-29：提示 ≤20 字（注册表 Hint 桩"首字：若"=4 字）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidQuestion_ReturnsShortHint()
    {
        SetUser(43002);
        var questionId = "Q-43002";
        RegisterQuestion(questionId);
        var svc = User.Use<GetHintService>();

        var result = await svc.ExecuteAsync(new GetHintReqDto { QuestionId = questionId, DifficultySlot = "S1" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.InRange(result.Hint.Length, 1, 20);
        Assert.DoesNotContain("星汉灿烂", result.Hint); // 严禁直接给答案
        Assert.Equal("memory-hook", result.HintSource);
    }

    /// <summary>BR-30：未指定难度档时按状态路由（NotMastered → S3）</summary>
    [Fact]
    public async Task ExecuteAsync_NotMastered_RoutesToS3()
    {
        var userId = SetUser(43003);
        var questionId = "Q-43003";
        RegisterQuestion(questionId);
        await SeedStateAsync(userId, questionId, MemoryState.NotMastered);
        var svc = User.Use<GetHintService>();

        var result = await svc.ExecuteAsync(new GetHintReqDto { QuestionId = questionId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("S3", result.DifficultySlot);
    }

    /// <summary>BR-30：Fuzzy → S2；Mastered → S1</summary>
    [Fact]
    public async Task ExecuteAsync_StateRouting_S2AndS1()
    {
        var userId = SetUser(43004);
        var qFuzzy = "Q-43004a";
        var qMastered = "Q-43004b";
        RegisterQuestion(qFuzzy);
        RegisterQuestion(qMastered);
        await SeedStateAsync(userId, qFuzzy, MemoryState.Fuzzy);
        await SeedStateAsync(userId, qMastered, MemoryState.Mastered);
        var svc = User.Use<GetHintService>();

        var fuzzy = await svc.ExecuteAsync(new GetHintReqDto { QuestionId = qFuzzy }, TestContext.Current.CancellationToken);
        var mastered = await svc.ExecuteAsync(new GetHintReqDto { QuestionId = qMastered }, TestContext.Current.CancellationToken);

        Assert.Equal("S2", fuzzy.DifficultySlot);
        Assert.Equal("S1", mastered.DifficultySlot);
    }

    /// <summary>BR-29/30：非法难度档 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidDifficultySlot_ReturnsParamInvalid()
    {
        SetUser(43005);
        var questionId = "Q-43005";
        RegisterQuestion(questionId);
        var svc = User.Use<GetHintService>();

        var result = await svc.ExecuteAsync(new GetHintReqDto { QuestionId = questionId, DifficultySlot = "S9" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }
}
