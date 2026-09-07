using XiaoShuTong.DataServices.Judging;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Judging;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Judging;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Judging;

/// <summary>
/// UC-J.2 判错反馈（SubmitJudgmentFeedbackService）Contract 测试
/// 覆盖 BR：BR-37 作答必须存在 → 9001 | BR-38 同作答同用户同类型幂等
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class SubmitJudgmentFeedbackServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Attempts> SeedAttemptAsync(long userId, string attemptUid)
    {
        var ds = User.Use<AttemptsDataService>();
        return await ds.EntityCreateAsync(new Attempts
        {
            UId = attemptUid,
            UserId = userId,
            QuestionId = "Q-39001",
            BankId = "bank-fb-39001",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = MemoryState.NotMastered,
            PostState = MemoryState.Fuzzy,
            Result = JudgmentResult.Correct,
            HintLevel = HintLevel.None,
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private static string NewAttemptUid() => UidGenerator.NewId(); // 32 位（实体校验 UId ≤32）

    /// <summary>主流程：有效作答 → 反馈写入（Pending）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidAttempt_CreatesFeedback()
    {
        var userId = SetUser(39001);
        var attempt = await SeedAttemptAsync(userId, NewAttemptUid());
        var svc = User.Use<SubmitJudgmentFeedbackService>();

        var result = await svc.ExecuteAsync(new SubmitJudgmentFeedbackReqDto { AttemptUid = attempt.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.FeedbackUid));
        Assert.Equal("Pending", result.Status);

        var feedbackDs = User.Use<JudgmentFeedbackDataService>();
        var feedback = await feedbackDs.EntityGetAsync(x => x.UId == result.FeedbackUid, TestContext.Current.CancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal(attempt.Id, feedback.AttemptId);
        Assert.Equal(userId, feedback.UserId);
        Assert.Equal(FeedbackStatus.Pending, feedback.Status);
    }

    /// <summary>BR-37：作答不存在 → FEEDBACK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownAttempt_ReturnsFeedbackNotFound()
    {
        SetUser(39002);
        var svc = User.Use<SubmitJudgmentFeedbackService>();

        var result = await svc.ExecuteAsync(new SubmitJudgmentFeedbackReqDto
        {
            AttemptUid = "no-such-attempt",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(JudgingErrorCodes.FeedbackNotFound, result.ErrorCode);
    }

    /// <summary>BR-38：同作答同用户重复反馈 → 幂等返回原反馈，不重复写</summary>
    [Fact]
    public async Task ExecuteAsync_SameAttemptTwice_Idempotent()
    {
        var userId = SetUser(39003);
        var attempt = await SeedAttemptAsync(userId, NewAttemptUid());
        var svc = User.Use<SubmitJudgmentFeedbackService>();

        var first = await svc.ExecuteAsync(new SubmitJudgmentFeedbackReqDto { AttemptUid = attempt.UId }, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(new SubmitJudgmentFeedbackReqDto { AttemptUid = attempt.UId }, TestContext.Current.CancellationToken);

        Assert.True(second.Success);
        Assert.Equal(first.FeedbackUid, second.FeedbackUid); // 返回已有反馈

        var feedbackDs = User.Use<JudgmentFeedbackDataService>();
        var all = await feedbackDs.EntitySelectAsync(
            x => x.AttemptId == attempt.Id && x.UserId == userId, ct: TestContext.Current.CancellationToken);
        Assert.Single(all); // 仅一条有效反馈
    }
}
