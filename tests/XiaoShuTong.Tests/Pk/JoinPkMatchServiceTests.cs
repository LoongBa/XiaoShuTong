using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.2 加入 PK（JoinPkMatchService）Contract 测试
/// 覆盖 BR：BR-06 对局不存在 | BR-07 已结束 | BR-08 人数已满 | BR-09 搭子资格 | BR-10 对战码匹配
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class JoinPkMatchServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
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

    private async Task<(PkMatches Match, PkPlayers Creator)> SeedPendingMatchAsync(long creatorId)
    {
        var ds = User.Use<PkMatchesDataService>();
        var match = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank-pk-92001",
            QuestionCount = 10,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Pending,
            InviteCode = "1234",
        }, TestContext.Current.CancellationToken);

        var playersDs = User.Use<PkPlayersDataService>();
        var creator = await playersDs.EntityCreateAsync(new PkPlayers
        {
            UId = UidGenerator.NewId(),
            MatchId = match.Id,
            UserId = creatorId,
        }, TestContext.Current.CancellationToken);
        return (match, creator);
    }

    /// <summary>主流程：有效对战码 + 搭子 → 加入成功 + 对局 Ongoing</summary>
    [Fact]
    public async Task ExecuteAsync_ValidJoin_JoinsMatch()
    {
        SetUser(92001);
        var (match, _) = await SeedPendingMatchAsync(92101); // 发起者
        await SeedBuddyAsync(92101, 92001);                  // 互为搭子
        var svc = User.Use<JoinPkMatchService>();

        var result = await svc.ExecuteAsync(new JoinPkMatchReqDto
        {
            MatchUid = match.UId,
            InviteCode = "1234",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(match.UId, result.MatchUid);
        Assert.Equal(10, result.QuestionCount);

        var playersDs = User.Use<PkPlayersDataService>();
        var players = await playersDs.EntitySelectAsync(x => x.MatchId == match.Id, ct: TestContext.Current.CancellationToken);
        Assert.Equal(2, players.Count); // 发起方 + 加入方

        var matchesDs = User.Use<PkMatchesDataService>();
        var updated = await matchesDs.EntityGetAsync(x => x.Id == match.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(PkMatchStatus.Ongoing, updated.Status);
    }

    /// <summary>BR-06/BR-10：对战码不符 → 2001</summary>
    [Fact]
    public async Task ExecuteAsync_WrongInviteCode_Returns2001()
    {
        SetUser(92002);
        var (match, _) = await SeedPendingMatchAsync(92201);
        await SeedBuddyAsync(92201, 92002);
        var svc = User.Use<JoinPkMatchService>();

        var result = await svc.ExecuteAsync(new JoinPkMatchReqDto
        {
            MatchUid = match.UId,
            InviteCode = "9999",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.MatchNotFound, result.ErrorCode);
    }

    /// <summary>BR-07：对局已结束 → 2002</summary>
    [Fact]
    public async Task ExecuteAsync_FinishedMatch_Returns2002()
    {
        SetUser(92003);
        var ds = User.Use<PkMatchesDataService>();
        var finished = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank",
            QuestionCount = 10,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Finished,
            InviteCode = "1234",
            FinishReason = PkFinishReason.Score,
        }, TestContext.Current.CancellationToken);
        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = finished.Id, UserId = 92301 }, TestContext.Current.CancellationToken);
        var svc = User.Use<JoinPkMatchService>();

        var result = await svc.ExecuteAsync(new JoinPkMatchReqDto { MatchUid = finished.UId, InviteCode = "1234" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.MatchClosed, result.ErrorCode);
    }

    /// <summary>BR-09：非搭子 → 2005</summary>
    [Fact]
    public async Task ExecuteAsync_NotBuddy_Returns2005()
    {
        SetUser(92004);
        var (match, _) = await SeedPendingMatchAsync(92401); // 发起者，无搭子关系
        var svc = User.Use<JoinPkMatchService>();

        var result = await svc.ExecuteAsync(new JoinPkMatchReqDto { MatchUid = match.UId, InviteCode = "1234" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.NotBuddyPk, result.ErrorCode);
    }

    /// <summary>BR-08：人数已满 → 2003</summary>
    [Fact]
    public async Task ExecuteAsync_FullMatch_Returns2003()
    {
        SetUser(92005);
        var ds = User.Use<PkMatchesDataService>();
        var match = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank",
            QuestionCount = 10,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Pending,
            InviteCode = "1234",
        }, TestContext.Current.CancellationToken);
        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = 92501 }, TestContext.Current.CancellationToken);
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = 92502 }, TestContext.Current.CancellationToken);
        await SeedBuddyAsync(92501, 92005);
        var svc = User.Use<JoinPkMatchService>();

        var result = await svc.ExecuteAsync(new JoinPkMatchReqDto { MatchUid = match.UId, InviteCode = "1234" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.MatchFull, result.ErrorCode);
    }
}