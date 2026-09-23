using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
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

    private async Task SeedQuestionAsync(string questionId, string kp = "岳阳楼记-背诵")
    {
        await SeedBankAsync();
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            ChapterId = "7a",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            Keywords = "[{\"Aliases\":[\"若出其中\"],\"Weight\":1,\"Required\":false}]",
            KnowledgePoints = [kp],
            Difficulty = 0,
            Status = QuestionStatus.Active,
            Hint = "首字：若",
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedBankAsync()
    {
        var ds = User.Use<BanksDataService>();
        var existing = await ds.EntityGetAsync(x => x.BankId == "bank-ch-7a", TestContext.Current.CancellationToken);
        if (existing != null) return;
        await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = "bank-ch-7a",
            Name = "会话结果测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = "bank.bank-ch-7a.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
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
        await SeedQuestionAsync("Q-46004a");
        await SeedQuestionAsync("Q-46004b");
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
        await SeedQuestionAsync("Q-46005a", kp: "岳阳楼记-背诵");
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
