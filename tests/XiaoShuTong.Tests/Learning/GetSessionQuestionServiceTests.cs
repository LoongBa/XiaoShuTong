using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.2a 会话取题（GetSessionQuestionService）Contract 测试
/// 覆盖 BR：BR-06 会话归属负例（非本人/不存在 → 3001）| 题库-BR-17 权限（私库非 Owner → Forbidden）
/// 题库-BR-18 题集耗尽空结果 | 题库-BR-19 响应不含答案与关键词（防爬）| 题库-BR-20 状态机排序
/// 薄包装唯一业务逻辑 = BR-06；其余 BR 语义由 Bank Callee 承载（ADR-008 决策一）。
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetSessionQuestionServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(long? ownerId, BankPrivacy privacy, string bankId, string name = "会话取题测试题库")
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = name,
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = privacy,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Questions> SeedQuestionAsync(string bankId, string questionId, string knowledgePoint = "观沧海")
    {
        var ds = User.Use<QuestionsDataService>();
        return await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            ChapterId = "7a",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            Keywords = "[{\"Aliases\":[\"以观沧海\"],\"Weight\":1,\"Required\":false}]",
            KnowledgePoints = [knowledgePoint],
            Difficulty = 0,
            Status = QuestionStatus.Active,
            Hint = "首字：以",
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
            StartedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<GetNextQuestionResDto> ExecuteAsync(string sessionUid, string bankId)
    {
        var svc = User.Use<GetSessionQuestionService>();
        return await svc.ExecuteAsync(new GetSessionQuestionReqDto
        {
            SessionUid = sessionUid,
            BankId = bankId,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-06（负例）：他人会话 → SESSION_NOT_FOUND（3001）</summary>
    [Fact]
    public async Task ExecuteAsync_OthersSession_ReturnsSessionNotFound()
    {
        var owner = SetUser(43901);
        await SeedBankAsync(null, BankPrivacy.Public, "bank-sq-43901");
        var session = await SeedSessionAsync(owner, "bank-sq-43901");
        SetUser(43902); // 另一用户

        var result = await ExecuteAsync(session.UId, "bank-sq-43901");

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.SessionNotFound, result.ErrorCode);
    }

    /// <summary>BR-06（负例）：会话不存在 → SESSION_NOT_FOUND（3001）</summary>
    [Fact]
    public async Task ExecuteAsync_NonexistentSession_ReturnsSessionNotFound()
    {
        SetUser(43903);

        var result = await ExecuteAsync("no-such-session-uid", "bank-sq-43903");

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.SessionNotFound, result.ErrorCode);
    }

    /// <summary>题库-BR-17：私域题库非 Owner → Forbidden（会话归属通过后由 Callee 承载）</summary>
    [Fact]
    public async Task ExecuteAsync_PrivateBankNotOwner_ReturnsForbidden()
    {
        var userId = SetUser(43904);
        var session = await SeedSessionAsync(userId, "bank-sq-43904");
        await SeedQuestionAsync("bank-sq-43904", "Q-43904a");
        // 私域题库属他人（非当前用户）→ Callee BR-17 Forbidden
        await SeedBankAsync(999991, BankPrivacy.Private, "bank-sq-43904");

        var result = await ExecuteAsync(session.UId, "bank-sq-43904");

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>题库-BR-19：主流程返回题目且不含答案/关键词（防爬 DRM）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidSession_ReturnsQuestionWithoutAnswer()
    {
        var userId = SetUser(43905);
        await SeedBankAsync(null, BankPrivacy.Public, "bank-sq-43905");
        await SeedQuestionAsync("bank-sq-43905", "Q-43905a");
        var session = await SeedSessionAsync(userId, "bank-sq-43905");

        var result = await ExecuteAsync(session.UId, "bank-sq-43905");

        Assert.True(result.Success);
        Assert.Equal("Q-43905a", result.QuestionId);
        Assert.Equal("R1", result.Type);
        Assert.Equal("观沧海", result.KnowledgePoint);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("以观沧海", json); // 关键词不下发
    }

    /// <summary>题库-BR-18：题集耗尽（会话已答全部题）→ Success=true 空结果（会话结束信号）</summary>
    [Fact]
    public async Task ExecuteAsync_AllAnswered_ReturnsEmpty()
    {
        var userId = SetUser(43906);
        await SeedBankAsync(null, BankPrivacy.Public, "bank-sq-43906");
        await SeedQuestionAsync("bank-sq-43906", "Q-43906a");
        var session = await SeedSessionAsync(userId, "bank-sq-43906");

        // 会话已答全部题目
        var attemptsDs = User.Use<AttemptsDataService>();
        await attemptsDs.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = session.Id,
            QuestionId = "Q-43906a",
            BankId = "bank-sq-43906",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = MemoryState.NotMastered,
            PostState = MemoryState.Fuzzy,
            Result = JudgmentResult.Correct,
            HintLevel = HintLevel.None,
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        var result = await ExecuteAsync(session.UId, "bank-sq-43906");

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.QuestionId); // 空结果 = 会话结束信号
    }

    /// <summary>题库-BR-20：状态机排序——到期复习优先于新题</summary>
    [Fact]
    public async Task ExecuteAsync_DueReview_PrioritizedOverNew()
    {
        var userId = SetUser(43907);
        await SeedBankAsync(null, BankPrivacy.Public, "bank-sq-43907");
        await SeedQuestionAsync("bank-sq-43907", "Q-43907a"); // 新题
        await SeedQuestionAsync("bank-sq-43907", "Q-43907b"); // 到期复习
        var session = await SeedSessionAsync(userId, "bank-sq-43907");

        // Q-43907b 已到期（NotMastered，NextReviewAt 过去）
        var statesDs = User.Use<MemoryStatesDataService>();
        await statesDs.EntityCreateAsync(new MemoryStates
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = "Q-43907b",
            BankId = "bank-sq-43907",
            State = MemoryState.NotMastered,
            ConsecutiveCorrect = 0,
            HistoryAccuracy = 0.5,
            NextReviewAt = DateTime.UtcNow.AddMinutes(-5),
        }, TestContext.Current.CancellationToken);

        var result = await ExecuteAsync(session.UId, "bank-sq-43907");

        Assert.True(result.Success);
        Assert.Equal("Q-43907b", result.QuestionId); // 到期复习优先（BR-20）
    }

    /// <summary>学习-BR-05 语义配合（G-4 联动）：会话已结束 → SESSION_ENDED（3003）</summary>
    [Fact]
    public async Task ExecuteAsync_EndedSession_ReturnsSessionEnded()
    {
        var userId = SetUser(43908);
        await SeedBankAsync(null, BankPrivacy.Public, "bank-sq-43908");
        await SeedQuestionAsync("bank-sq-43908", "Q-43908a");
        var session = await SeedSessionAsync(userId, "bank-sq-43908");

        // 标记会话已结束（EndStudySession 写入 EndedAt）
        var sessionsDs = User.Use<StudySessionsDataService>();
        session.EndedAt = DateTime.UtcNow.AddMinutes(-1);
        await sessionsDs.EntityUpdateAsync(session, TestContext.Current.CancellationToken);

        var result = await ExecuteAsync(session.UId, "bank-sq-43908");

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.SessionEnded, result.ErrorCode);
    }
}