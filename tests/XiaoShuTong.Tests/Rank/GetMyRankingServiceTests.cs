using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Services.Rank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Rank;

/// <summary>
/// UC-7.2 我的排名（GetMyRankingService）Contract 测试
/// 覆盖 BR：BR-06 无快照 | BR-07 RankChange = 今日−昨日（正=上升）| BR-02 战绩榜门控
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetMyRankingServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(string groupUid, bool rankEnabled = true)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = 72001,
            Name = "群组",
            Subject = "chinese",
            Status = GroupStatus.Active,
            RankEnabled = rankEnabled,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedSnapshotAsync(
        long userId, string groupUid, RankMetricType metric, decimal value, int rank, DateOnly date)
    {
        var ds = User.Use<RankSnapshotsDataService>();
        await ds.EntityCreateAsync(new RankSnapshots
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            ScopeType = RankScopeType.Group,
            ScopeId = groupUid,
            Subject = "All",
            MetricType = metric,
            MetricValue = value,
            Rank = rank,
            SnapshotDate = date,
        }, TestContext.Current.CancellationToken);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

    /// <summary>主流程 + BR-07：今日 rank 3、昨日 rank 5 → RankChange=+2（上升）+ 趋势 Up</summary>
    [Fact]
    public async Task ExecuteAsync_ImprovedRank_ReturnsPositiveChange()
    {
        var userId = SetUser(72001);
        var group = await SeedGroupAsync("group-my-72001");
        var today = Today();
        // 今日：A(me) streak=2 → 组合值 2 → 第 3；B=4 → 1；C=3 → 2
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.Streak, 2, 3, today);
        await SeedSnapshotAsync(72901, group.UId, RankMetricType.Streak, 4, 1, today);
        await SeedSnapshotAsync(72902, group.UId, RankMetricType.Streak, 3, 2, today);
        // 昨日：A=1 → 第 5；B=5 → 1；C=4 → 2；D=3 → 3；E=2 → 4
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.Streak, 1, 5, today.AddDays(-1));
        await SeedSnapshotAsync(72901, group.UId, RankMetricType.Streak, 5, 1, today.AddDays(-1));
        await SeedSnapshotAsync(72902, group.UId, RankMetricType.Streak, 4, 2, today.AddDays(-1));
        await SeedSnapshotAsync(72903, group.UId, RankMetricType.Streak, 3, 3, today.AddDays(-1));
        await SeedSnapshotAsync(72904, group.UId, RankMetricType.Streak, 2, 4, today.AddDays(-1));
        var svc = User.Use<GetMyRankingService>();

        var result = await svc.ExecuteAsync(new GetMyRankingReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(3, result.Rank);
        Assert.Equal(2m, result.Value);
        Assert.Equal(2, result.RankChange); // 今日 3 − 昨日 5 = +2 上升
        Assert.Equal("Up", result.Trend);
    }

    /// <summary>BR-06：无快照 → 空响应（码 0）</summary>
    [Fact]
    public async Task ExecuteAsync_NoSnapshot_ReturnsEmpty()
    {
        SetUser(72002);
        var group = await SeedGroupAsync("group-my-72002");
        var svc = User.Use<GetMyRankingService>();

        var result = await svc.ExecuteAsync(new GetMyRankingReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.Rank);
    }

    /// <summary>BR-02 门控：RankEnabled=false 查战绩榜 → 7001</summary>
    [Fact]
    public async Task ExecuteAsync_PerformanceDisabled_Returns7001()
    {
        SetUser(72003);
        var group = await SeedGroupAsync("group-my-72003", rankEnabled: false);
        var svc = User.Use<GetMyRankingService>();

        var result = await svc.ExecuteAsync(new GetMyRankingReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Performance",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(RankErrorCodes.RankPerformanceDisabled, result.ErrorCode);
        Assert.False(result.RankEnabled);
    }

    /// <summary>战绩榜：Accuracy/Mastery 单指标直读（当前用户冻结值）</summary>
    [Fact]
    public async Task ExecuteAsync_Performance_ExposesSingleMetrics()
    {
        var userId = SetUser(72004);
        var group = await SeedGroupAsync("group-my-72004");
        var today = Today();
        // 当前用户：accuracy=0.75 + mastery=0.6 → 求和 1.35（第 2）
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.Accuracy, 0.75m, 2, today);
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.Mastery, 0.6m, 2, today);
        // 对手：accuracy=0.9 + mastery=0.8 → 求和 1.7（第 1）
        await SeedSnapshotAsync(72905, group.UId, RankMetricType.Accuracy, 0.9m, 1, today);
        await SeedSnapshotAsync(72905, group.UId, RankMetricType.Mastery, 0.8m, 1, today);
        var svc = User.Use<GetMyRankingService>();

        var result = await svc.ExecuteAsync(new GetMyRankingReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Performance",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Rank); // 对手 1.7 > 本人 1.35 → 第 2
        Assert.Equal(1.35m, result.Value);
        Assert.Equal(0.75, result.Accuracy);
        Assert.Equal(0.6, result.Mastery);
    }

    /// <summary>战力榜：Streak/Volume/PkWins 单指标直读（当前用户冻结值）</summary>
    [Fact]
    public async Task ExecuteAsync_Combat_ExposesSingleMetrics()
    {
        var userId = SetUser(72005);
        var group = await SeedGroupAsync("group-my-72005");
        var today = Today();
        // 当前用户：streak=12 + volume=45 + pk_wins=3 → 求和 60（第 2）
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.Streak, 12m, 2, today);
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.Volume, 45m, 2, today);
        await SeedSnapshotAsync(userId, group.UId, RankMetricType.PkWins, 3m, 2, today);
        // 对手：streak=20 + volume=50 + pk_wins=5 → 求和 75（第 1）
        await SeedSnapshotAsync(72906, group.UId, RankMetricType.Streak, 20m, 1, today);
        await SeedSnapshotAsync(72906, group.UId, RankMetricType.Volume, 50m, 1, today);
        await SeedSnapshotAsync(72906, group.UId, RankMetricType.PkWins, 5m, 1, today);
        var svc = User.Use<GetMyRankingService>();

        var result = await svc.ExecuteAsync(new GetMyRankingReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Rank); // 对手 75 > 本人 60 → 第 2
        Assert.Equal(60m, result.Value);
        Assert.Equal(12, result.Streak);
        Assert.Equal(45, result.Volume);
        Assert.Equal(3, result.PkWins);
    }
}