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
/// UC-4.2b 结束学习会话（EndStudySessionService）Contract 测试
/// 覆盖 BR：BR-39 会话归属（非本人 → 3001）| 幂等（EndedAt 已置回读不覆盖，BR-05 语义配合）
/// 聚合写回（EndedAt/CorrectCount/TotalTimeMs，口径与 GetSessionResult 一致 = SessionResultAggregator）
/// 结束后再 createStudySession → 新建会话（BR-05 幂等失效验证）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class EndStudySessionServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "结束会话测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<StudySessions> SeedSessionAsync(long userId, string bankId)
    {
        var ds = User.Use<StudySessionsDataService>();
        return await ds.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = LearningScenario.Memorize,
            BankId = bankId,
            SessionType = SessionType.Free,
            QuestionCount = 10,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedAttemptAsync(long userId, long sessionId, string bankId, string questionId,
        JudgmentResult result, MemoryState preState, MemoryState postState, int timeCostMs)
    {
        var ds = User.Use<AttemptsDataService>();
        await ds.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = sessionId,
            QuestionId = questionId,
            BankId = bankId,
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = preState,
            PostState = postState,
            Result = result,
            HintLevel = HintLevel.None,
            TimeCostMs = timeCostMs,
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：首次结束 → 写入 EndedAt/CorrectCount/TotalTimeMs（聚合口径正确）</summary>
    [Fact]
    public async Task ExecuteAsync_FirstEnd_WritesThreeFields()
    {
        var userId = SetUser(44001);
        await SeedBankAsync("bank-es-44001");
        var session = await SeedSessionAsync(userId, "bank-es-44001");

        // 3 条作答：2 Correct + 1 Wrong（NotMastered→Fuzzy 等），耗时 1200/800/1500
        await SeedAttemptAsync(userId, session.Id, "bank-es-44001", "Q-44001a",
            JudgmentResult.Correct, MemoryState.Fuzzy, MemoryState.Mastered, 1200);
        await SeedAttemptAsync(userId, session.Id, "bank-es-44001", "Q-44001b",
            JudgmentResult.Correct, MemoryState.NotMastered, MemoryState.Fuzzy, 800);
        await SeedAttemptAsync(userId, session.Id, "bank-es-44001", "Q-44001c",
            JudgmentResult.Wrong, MemoryState.NotMastered, MemoryState.NotMastered, 1500);

        var svc = User.Use<EndStudySessionService>();
        var result = await svc.ExecuteAsync(new EndStudySessionReqDto { SessionUid = session.UId },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
        Assert.Equal(session.UId, result.SessionUid);
        Assert.NotNull(result.EndedAt);
        Assert.Equal(2, result.CorrectCount);          // 2 Correct
        Assert.Equal(1200 + 800 + 1500, result.TotalTimeMs); // 3500

        // 回读实体确认落库
        var sessionsDs = User.Use<StudySessionsDataService>();
        var persisted = await sessionsDs.EntityGetAsync(x => x.UId == session.UId,
            TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.NotNull(persisted.EndedAt);
        Assert.Equal(2, persisted.CorrectCount);
        Assert.Equal(3500, persisted.TotalTimeMs);
    }

    /// <summary>BR-40 空会话：attempts 为空 → 仍写 EndedAt，CorrectCount/TotalTimeMs = 0</summary>
    [Fact]
    public async Task ExecuteAsync_EmptySession_WritesEndedAtOnly()
    {
        var userId = SetUser(44002);
        await SeedBankAsync("bank-es-44002");
        var session = await SeedSessionAsync(userId, "bank-es-44002");

        var svc = User.Use<EndStudySessionService>();
        var result = await svc.ExecuteAsync(new EndStudySessionReqDto { SessionUid = session.UId },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.EndedAt);
        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(0, result.TotalTimeMs);
    }

    /// <summary>幂等：已结束再调 → 回读原值不覆盖不重复聚合（BR-05 语义配合）</summary>
    [Fact]
    public async Task ExecuteAsync_AlreadyEnded_ReturnsCurrentValues()
    {
        var userId = SetUser(44003);
        await SeedBankAsync("bank-es-44003");
        var session = await SeedSessionAsync(userId, "bank-es-44003");
        await SeedAttemptAsync(userId, session.Id, "bank-es-44003", "Q-44003a",
            JudgmentResult.Correct, MemoryState.Fuzzy, MemoryState.Mastered, 1000);

        var svc = User.Use<EndStudySessionService>();
        var first = await svc.ExecuteAsync(new EndStudySessionReqDto { SessionUid = session.UId },
            TestContext.Current.CancellationToken);
        Assert.True(first.Success);
        var firstEndedAt = first.EndedAt;

        // 第二次调用 → 幂等回读，EndedAt 不变
        var second = await svc.ExecuteAsync(new EndStudySessionReqDto { SessionUid = session.UId },
            TestContext.Current.CancellationToken);
        Assert.True(second.Success);
        Assert.Equal(firstEndedAt, second.EndedAt);
        Assert.Equal(first.CorrectCount, second.CorrectCount);
        Assert.Equal(first.TotalTimeMs, second.TotalTimeMs);
    }

    /// <summary>BR-39（负例）：他人会话 → SESSION_NOT_FOUND（3001）</summary>
    [Fact]
    public async Task ExecuteAsync_OthersSession_ReturnsSessionNotFound()
    {
        var owner = SetUser(44004);
        await SeedBankAsync("bank-es-44004");
        var session = await SeedSessionAsync(owner, "bank-es-44004");
        SetUser(44005); // 另一用户

        var svc = User.Use<EndStudySessionService>();
        var result = await svc.ExecuteAsync(new EndStudySessionReqDto { SessionUid = session.UId },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.SessionNotFound, result.ErrorCode);
    }

    /// <summary>BR-05 语义配合：结束后再 createStudySession（同任务）→ 新建会话（幂等失效，EndedAt!=null 不复用）</summary>
    [Fact]
    public async Task ExecuteAsync_AfterEnd_CreateStudySessionCreatesNew()
    {
        var userId = SetUser(44006);
        await SeedBankAsync("bank-es-44006");
        var sessionsDs = User.Use<StudySessionsDataService>();

        // 任务会话（TaskId > 0）
        var taskId = 44006L;
        var session = await sessionsDs.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = LearningScenario.Memorize,
            BankId = "bank-es-44006",
            SessionType = SessionType.Progressive,
            QuestionCount = 10,
            TaskId = taskId,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
        }, TestContext.Current.CancellationToken);

        // 结束会话
        var svc = User.Use<EndStudySessionService>();
        var ended = await svc.ExecuteAsync(new EndStudySessionReqDto { SessionUid = session.UId },
            TestContext.Current.CancellationToken);
        Assert.True(ended.Success);

        // 再 createStudySession（同任务）→ BR-05 幂等失效（EndedAt!=null）→ 新建
        var createSvc = User.Use<CreateStudySessionService>();
        var created = await createSvc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "bank-es-44006",
            SessionType = "Progressive",
            TaskId = taskId,
            QuestionCount = 10,
        }, TestContext.Current.CancellationToken);

        Assert.True(created.Success);
        Assert.NotEqual(session.UId, created.SessionUid); // 新建会话，不复用已结束的
    }
}
