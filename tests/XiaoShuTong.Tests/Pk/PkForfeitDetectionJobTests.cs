using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.5 掉线弃权检测（PkForfeitDetectionJob）Contract 测试
/// 覆盖 BR：BR-21 30s 内重连取消 | BR-22 幂等 | BR-23 30s 未重连 → Forfeit + 对方胜
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class PkForfeitDetectionJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private async Task<(PkMatches Match, long OnlineId, long OfflineId)> SeedOngoingMatchAsync(
        long onlineId, long offlineId)
    {
        var ds = User.Use<PkMatchesDataService>();
        var match = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank",
            QuestionCount = 5,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Ongoing,
        }, TestContext.Current.CancellationToken);

        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = onlineId }, TestContext.Current.CancellationToken);
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = offlineId }, TestContext.Current.CancellationToken);
        return (match, onlineId, offlineId);
    }

    /// <summary>BR-23：断线超 30s → Forfeit + 对方获胜</summary>
    [Fact]
    public async Task ExecuteAsync_DisconnectedOver30s_ForfeitOpponentWins()
    {
        var (match, onlineId, offlineId) = await SeedOngoingMatchAsync(95001, 95101);
        PkDisconnectTracker.Track(match.Id, offlineId, DateTime.UtcNow.AddSeconds(-31)); // 已断线 31s
        var job = User.Use<PkForfeitDetectionJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var matchesDs = User.Use<PkMatchesDataService>();
        var updated = await matchesDs.EntityGetAsync(x => x.Id == match.Id, TestContext.Current.CancellationToken);
        Assert.Equal(PkMatchStatus.Finished, updated.Status);
        Assert.Equal(PkFinishReason.Forfeit, updated.FinishReason);
        Assert.Equal(onlineId, updated.WinnerId); // 对方获胜
    }

    /// <summary>BR-21：断线 30s 内（未超时）→ 不判弃权，对局继续</summary>
    [Fact]
    public async Task ExecuteAsync_DisconnectedUnder30s_NoForfeit()
    {
        var (match, _, offlineId) = await SeedOngoingMatchAsync(95002, 95201);
        PkDisconnectTracker.Track(match.Id, offlineId, DateTime.UtcNow.AddSeconds(-10)); // 仅 10s
        var job = User.Use<PkForfeitDetectionJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var matchesDs = User.Use<PkMatchesDataService>();
        var updated = await matchesDs.EntityGetAsync(x => x.Id == match.Id, TestContext.Current.CancellationToken);
        Assert.Equal(PkMatchStatus.Ongoing, updated.Status); // 未判弃权
    }

    /// <summary>BR-21：重连（取消追踪）→ 对局继续；BR-22：幂等（已 Finished 不再处理）</summary>
    [Fact]
    public async Task ExecuteAsync_ReconnectCancels_AndIdempotent()
    {
        var (match, onlineId, offlineId) = await SeedOngoingMatchAsync(95003, 95301);
        PkDisconnectTracker.Track(match.Id, offlineId, DateTime.UtcNow.AddSeconds(-40)); // 超时
        PkDisconnectTracker.Cancel(match.Id, offlineId); // 重连取消
        var job = User.Use<PkForfeitDetectionJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);
        await job.ExecuteAsync(TestContext.Current.CancellationToken); // 二次（幂等）

        var matchesDs = User.Use<PkMatchesDataService>();
        var updated = await matchesDs.EntityGetAsync(x => x.Id == match.Id, TestContext.Current.CancellationToken);
        Assert.Equal(PkMatchStatus.Ongoing, updated.Status); // 重连后不判弃权
        Assert.Null(updated.WinnerId);
    }
}