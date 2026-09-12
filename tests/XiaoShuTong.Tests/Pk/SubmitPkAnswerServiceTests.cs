using System.Text.Json;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.3 对战答题（SubmitPkAnswerService）Contract 测试
/// 覆盖 BR：BR-11 非参赛者 | BR-12 答对+10 | BR-13 Partial 0 分 | BR-15 防重复幂等 | BR-16 用时上送 + 末题结算
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class SubmitPkAnswerServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private static readonly string[] Keywords = ["东临碣石", "以观沧海", "水何澹澹"];

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedBuddyAsync(long userA, long userB)
    {
        var now = DateTime.UtcNow;
        var ds = User.Use<StudyBuddiesDataService>();
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = userA,
            InviteeId = userB,
            Status = BuddyStatus.Accepted,
            InvitedAt = now,
            ExpiresAt = now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedQuestionAsync(string questionId, string bankId)
    {
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全\"}}",
            Keywords = JsonSerializer.Serialize(Keywords.Select(k => new KeywordGroup([k], 1d, false))),
            KnowledgePoints = ["观沧海"],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>直接种子 2 人对局（Ongoing，绕过 Create/Join——BR-03 题量限 5/10/20，测试用小题量）</summary>
    private async Task<(PkMatches Match, long MeId, long OpponentId)> SeedOngoingMatchAsync(long meId, long opponentId, int questionCount = 3)
    {
        var matchesDs = User.Use<PkMatchesDataService>();
        var match = await matchesDs.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank-pk-93001",
            QuestionCount = questionCount,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Ongoing,
        }, TestContext.Current.CancellationToken);

        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = meId }, TestContext.Current.CancellationToken);
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = opponentId }, TestContext.Current.CancellationToken);
        return (match, meId, opponentId);
    }

    /// <summary>主流程 + BR-12/16：答对 → +10 分 + 明细落库 + 末题结算</summary>
    [Fact]
    public async Task ExecuteAsync_CorrectAnswer_Scores10AndFinalizes()
    {
        var meId = SetUser(93001);
        var (match, _, _) = await SeedOngoingMatchAsync(meId, 93101, questionCount: 1);
        await SeedQuestionAsync("Q-93001a", "bank-pk-93001");
        var svc = User.Use<SubmitPkAnswerService>();

        var result = await svc.ExecuteAsync(new SubmitPkAnswerReqDto
        {
            MatchUid = match.UId,
            QuestionId = "Q-93001a",
            UserAnswer = "东临碣石 以观沧海 水何澹澹",
            TimeCostMs = 5000,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.IsCorrect);
        Assert.Equal(10, result.Score); // BR-12：答对 +10

        // BR-16：用时上送落库 + 分数更新
        var attemptsDs = User.Use<PkAttemptsDataService>();
        var attempt = await attemptsDs.EntityGetAsync(x => x.QuestionId == "Q-93001a", TestContext.Current.CancellationToken);
        Assert.NotNull(attempt);
        Assert.Equal(5000, attempt.TimeCostMs);

        var playersDs = User.Use<PkPlayersDataService>();
        var me = await playersDs.EntityGetAsync(x => x.MatchId == match.Id && x.UserId == meId, TestContext.Current.CancellationToken);
        Assert.NotNull(me);
        Assert.Equal(10, me.Score);

        // 末题答完 → 结算（对手 0 分 → 我方胜）
        var matchesDs = User.Use<PkMatchesDataService>();
        var updated = await matchesDs.EntityGetAsync(x => x.Id == match.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(PkMatchStatus.Finished, updated.Status);
        Assert.Equal(meId, updated.WinnerId);
        Assert.Equal(PkFinishReason.Score, updated.FinishReason);
    }

    /// <summary>BR-13：Partial → 0 分（IsCorrect=false）</summary>
    [Fact]
    public async Task ExecuteAsync_PartialAnswer_ScoresZero()
    {
        var meId = SetUser(93002);
        var (match, _, _) = await SeedOngoingMatchAsync(meId, 93201, questionCount: 2);
        await SeedQuestionAsync("Q-93002a", "bank-pk-93001");
        var svc = User.Use<SubmitPkAnswerService>();

        // 只命中 1/3 关键词 → Partial（0.33 < 0.5 → Wrong？—— 0.33<0.5 → Wrong；构造 0.6 → Partial 需 2/3）
        var result = await svc.ExecuteAsync(new SubmitPkAnswerReqDto
        {
            MatchUid = match.UId,
            QuestionId = "Q-93002a",
            UserAnswer = "东临碣石 以观沧海", // 2/3 = 0.67 → Partial
            TimeCostMs = 3000,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Partial", result.Result);
        Assert.False(result.IsCorrect);
        Assert.Equal(0, result.Score); // BR-13：Partial 计 0 分
    }

    /// <summary>BR-11：非参赛者 → 2004</summary>
    [Fact]
    public async Task ExecuteAsync_NotParticipant_Returns2004()
    {
        var meId = SetUser(93003);
        var (match, _, _) = await SeedOngoingMatchAsync(meId, 93301);
        await SeedQuestionAsync("Q-93003a", "bank-pk-93001");
        SetUser(93999); // 非参赛者
        var svc = User.Use<SubmitPkAnswerService>();

        var result = await svc.ExecuteAsync(new SubmitPkAnswerReqDto
        {
            MatchUid = match.UId,
            QuestionId = "Q-93003a",
            UserAnswer = "东临碣石 以观沧海 水何澹澹",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.NotParticipant, result.ErrorCode);
    }

    /// <summary>BR-15：同题重复提交 → 幂等返回（不重复计分）</summary>
    [Fact]
    public async Task ExecuteAsync_DuplicateQuestion_Idempotent()
    {
        var meId = SetUser(93004);
        var (match, _, _) = await SeedOngoingMatchAsync(meId, 93401, questionCount: 3);
        await SeedQuestionAsync("Q-93004a", "bank-pk-93001");
        var svc = User.Use<SubmitPkAnswerService>();
        var request = new SubmitPkAnswerReqDto { MatchUid = match.UId, QuestionId = "Q-93004a", UserAnswer = "东临碣石 以观沧海 水何澹澹", TimeCostMs = 2000 };

        var first = await svc.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Score, second.Score);

        var attemptsDs = User.Use<PkAttemptsDataService>();
        var attempts = await attemptsDs.EntitySelectAsync(x => x.QuestionId == "Q-93004a", ct: TestContext.Current.CancellationToken);
        Assert.Single(attempts); // 仅一条明细
    }
}