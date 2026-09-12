using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.6 会话结果（GetSessionResultService）Contract 测试
/// 覆盖 BR：BR-39 会话归属 | BR-40 空会话 | BR-41 NewStarCount 升★次数 | BR-42 卡壳知识点
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetSessionResultServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private static void RegisterQuestion(string questionId, string kp = "岳阳楼记-背诵")
    {
        LearningQuestionRegistry.Register(new LearningQuestionMeta(
            QuestionId: questionId,
            BankId: "bank-ch-7a",
            Subject: "chinese",
            KnowledgePoint: kp,
            QType: "R1",
            AnswerKeywords: ["若出其中", "星汉灿烂"],
            Hint: "首字：若"));
    }

    private async Task<StudySessions> SeedSessionAsync(long userId)
    {
        var ds = User.Use<StudySessionsDataService>();
        return await ds.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = LearningScenario.Memorize,
            BankId = "bank-ch-7a",
            SessionType = SessionType.Free,
            QuestionCount = 10,
            StartedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedAttemptAsync(long userId, long sessionId, string questionId, MemoryState pre, MemoryState post)
    {
        var ds = User.Use<AttemptsDataService>();
        await ds.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = sessionId,
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = pre,
            PostState = post,
            Result = post == MemoryState.Proficient || post == MemoryState.Mastered ? JudgmentResult.Correct : JudgmentResult.Wrong,
            HintLevel = HintLevel.None,
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-39：他人会话 → SESSION_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_OthersSession_ReturnsSessionNotFound()
    {
        var owner = SetUser(46001);
        var session = await SeedSessionAsync(owner);
        SetUser(46002);

        var svc = User.Use<GetSessionResultService>();
        var result = await svc.ExecuteAsync(new GetSessionResultReqDto { SessionUid = session.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.SessionNotFound, result.ErrorCode);
    }

    /// <summary>BR-40：空会话（未作答退出）→ 空统计正常返回</summary>
    [Fact]
    public async Task ExecuteAsync_EmptySession_ReturnsEmptyStats()
    {
        var userId = SetUser(46003);
        var session = await SeedSessionAsync(userId);
        var svc = User.Use<GetSessionResultService>();

        var result = await svc.ExecuteAsync(new GetSessionResultReqDto { SessionUid = session.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(0, result.NewStarCount);
        Assert.Empty(result.BlockedPoints);
    }

    /// <summary>BR-41：NewStarCount = PostState=Proficient 且 PreState&lt;Proficient 的次数</summary>
    [Fact]
    public async Task ExecuteAsync_SessionWithStarUpgrade_CountsNewStars()
    {
        var userId = SetUser(46004);
        var session = await SeedSessionAsync(userId);
        RegisterQuestion("Q-46004a");
        RegisterQuestion("Q-46004b");
        await SeedAttemptAsync(userId, session.Id, "Q-46004a", MemoryState.Fuzzy, MemoryState.Proficient);   // 升★
        await SeedAttemptAsync(userId, session.Id, "Q-46004b", MemoryState.Mastered, MemoryState.Mastered);  // 无升★
        var svc = User.Use<GetSessionResultService>();

        var result = await svc.ExecuteAsync(new GetSessionResultReqDto { SessionUid = session.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.NewStarCount);
    }

    /// <summary>BR-42：BlockedPoints = ✕/△ 题目 + 知识点</summary>
    [Fact]
    public async Task ExecuteAsync_SessionWithBlockedPoints_ListsKnowledgePoints()
    {
        var userId = SetUser(46005);
        var session = await SeedSessionAsync(userId);
        RegisterQuestion("Q-46005a", kp: "岳阳楼记-背诵");
        await SeedAttemptAsync(userId, session.Id, "Q-46005a", MemoryState.Mastered, MemoryState.NotMastered); // 卡壳
        var svc = User.Use<GetSessionResultService>();

        var result = await svc.ExecuteAsync(new GetSessionResultReqDto { SessionUid = session.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var blocked = Assert.Single(result.BlockedPoints);
        Assert.Equal("Q-46005a", blocked.QuestionId);
        Assert.Equal("岳阳楼记-背诵", blocked.KnowledgePoint);
        Assert.Equal("NotMastered", blocked.State);
    }
}
