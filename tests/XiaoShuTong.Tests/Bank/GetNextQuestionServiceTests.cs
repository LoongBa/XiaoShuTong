using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.5 防爬取题（GetNextQuestionService）Contract 测试
/// 覆盖 BR：BR-17 题库存在且有访问权 | BR-18 题集耗尽空结果 | BR-19 不含答案与关键词 | BR-20 状态机排序
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetNextQuestionServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(long? ownerId, BankPrivacy privacy, string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "取题测试题库",
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
            Keywords = "[{\"aliases\":[\"以观沧海\"]}]",
            KnowledgePoints = [knowledgePoint],
            Difficulty = 0,
            Status = QuestionStatus.Active,
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
            BankId = "bank-next-35001",
            SessionType = SessionType.Free,
            QuestionCount = 10,
            StartedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-17：非法 bankId → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_ReturnsBankNotFound()
    {
        SetUser(35001);
        var svc = User.Use<GetNextQuestionService>();

        var result = await svc.ExecuteAsync(new GetNextQuestionReqDto { BankId = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-17：私域题库非 Owner → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_PrivateBankNotOwner_ReturnsForbidden()
    {
        await SeedBankAsync(999902, BankPrivacy.Private, "bank-next-35002");
        SetUser(35002);
        var svc = User.Use<GetNextQuestionService>();

        var result = await svc.ExecuteAsync(new GetNextQuestionReqDto { BankId = "bank-next-35002" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>主流程 + BR-19：返回题目不含答案与关键词（防爬 DRM 核心）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidBank_ReturnsQuestionWithoutAnswer()
    {
        var userId = SetUser(35003);
        await SeedBankAsync(ownerId: null, BankPrivacy.Public, "bank-next-35003");
        await SeedQuestionAsync("bank-next-35003", "Q-35003a");
        var session = await SeedSessionAsync(userId);
        var svc = User.Use<GetNextQuestionService>();

        var result = await svc.ExecuteAsync(new GetNextQuestionReqDto
        {
            SessionId = session.UId,
            BankId = "bank-next-35003",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Q-35003a", result.QuestionId);
        Assert.Equal("R1", result.Type);
        Assert.Equal("观沧海", result.KnowledgePoint);

        // BR-19：响应 JSON 不含 answer / keywords（防渗透）
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("以观沧海", json); // 关键词不下发
    }

    /// <summary>BR-20：状态机排序——到期复习题优先于新题</summary>
    [Fact]
    public async Task ExecuteAsync_DueReview_PrioritizedOverNew()
    {
        var userId = SetUser(35004);
        await SeedBankAsync(ownerId: null, BankPrivacy.Public, "bank-next-35004");
        await SeedQuestionAsync("bank-next-35004", "Q-35004a"); // 新题
        await SeedQuestionAsync("bank-next-35004", "Q-35004b"); // 到期复习
        var session = await SeedSessionAsync(userId);

        // Q-35004b 已到期（NotMastered，NextReviewAt 过去）
        var statesDs = User.Use<MemoryStatesDataService>();
        await statesDs.EntityCreateAsync(new MemoryStates
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = "Q-35004b",
            BankId = "bank-next-35004",
            State = MemoryState.NotMastered,
            ConsecutiveCorrect = 0,
            HistoryAccuracy = 0.5,
            NextReviewAt = DateTime.UtcNow.AddMinutes(-5),
        }, TestContext.Current.CancellationToken);

        var svc = User.Use<GetNextQuestionService>();
        var result = await svc.ExecuteAsync(new GetNextQuestionReqDto
        {
            SessionId = session.UId,
            BankId = "bank-next-35004",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Q-35004b", result.QuestionId); // 到期复习优先
    }

    /// <summary>BR-18：题集耗尽 → 空结果（会话已答全部题目）</summary>
    [Fact]
    public async Task ExecuteAsync_AllAnswered_ReturnsEmpty()
    {
        var userId = SetUser(35005);
        await SeedBankAsync(ownerId: null, BankPrivacy.Public, "bank-next-35005");
        await SeedQuestionAsync("bank-next-35005", "Q-35005a");
        var session = await SeedSessionAsync(userId);

        // 会话已答 Q-35005a
        var attemptsDs = User.Use<AttemptsDataService>();
        await attemptsDs.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = session.Id,
            QuestionId = "Q-35005a",
            BankId = "bank-next-35005",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = MemoryState.NotMastered,
            PostState = MemoryState.Fuzzy,
            Result = JudgmentResult.Correct,
            HintLevel = HintLevel.None,
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        var svc = User.Use<GetNextQuestionService>();
        var result = await svc.ExecuteAsync(new GetNextQuestionReqDto
        {
            SessionId = session.UId,
            BankId = "bank-next-35005",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.QuestionId); // 空结果 = 会话结束信号
    }
}
