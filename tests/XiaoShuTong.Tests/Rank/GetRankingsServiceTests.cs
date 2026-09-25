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
/// UC-7.1 榜单查询（GetRankingsService）Contract 测试
/// 覆盖 BR：BR-01 空快照 | BR-02 战绩榜 RankEnabled 门控 | BR-03 参数校验 | BR-04 战力榜不受影响 | BR-05 趋势箭头
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetRankingsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(string groupUid, bool rankEnabled)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = 70001,
            Name = "榜单群组",
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

    /// <summary>主流程 + BR-11/12：战力榜组合求和排序（streak+volume+pk_wins）</summary>
    [Fact]
    public async Task ExecuteAsync_CombatRanking_SumsMetricsAndSorts()
    {
        SetUser(71001);
        var group = await SeedGroupAsync("group-rank-71001", rankEnabled: true);
        var today = Today();
        // 用户 A：streak=2, volume=10, pk_wins=0 → 12
        await SeedSnapshotAsync(71011, group.UId, RankMetricType.Streak, 2, 1, today);
        await SeedSnapshotAsync(71011, group.UId, RankMetricType.Volume, 10, 1, today);
        await SeedSnapshotAsync(71011, group.UId, RankMetricType.PkWins, 0, 1, today);
        // 用户 B：streak=1, volume=5, pk_wins=1 → 7
        await SeedSnapshotAsync(71012, group.UId, RankMetricType.Streak, 1, 2, today);
        await SeedSnapshotAsync(71012, group.UId, RankMetricType.Volume, 5, 2, today);
        await SeedSnapshotAsync(71012, group.UId, RankMetricType.PkWins, 1, 2, today);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group",
            ScopeId = group.UId,
            Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(71011, result.Items[0].UserId); // A 12 > B 7
        Assert.Equal(12m, result.Items[0].Value);
        Assert.Equal(7m, result.Items[1].Value);
        Assert.Equal(1, result.Items[0].Rank);
    }

    /// <summary>BR-05：趋势箭头——今日 rank=昨日 rank → Flat</summary>
    [Fact]
    public async Task ExecuteAsync_TrendFlat_WhenRanksEqual()
    {
        SetUser(71002);
        var group = await SeedGroupAsync("group-rank-71002", rankEnabled: true);
        var today = Today();
        await SeedSnapshotAsync(71021, group.UId, RankMetricType.Streak, 2, 1, today);
        await SeedSnapshotAsync(71022, group.UId, RankMetricType.Streak, 1, 2, today);
        await SeedSnapshotAsync(71021, group.UId, RankMetricType.Streak, 2, 1, today.AddDays(-1));
        await SeedSnapshotAsync(71022, group.UId, RankMetricType.Streak, 1, 2, today.AddDays(-1));
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.All(result.Items, i => Assert.Equal("Flat", i.Trend)); // 今日=昨日 → flat
    }

    /// <summary>BR-02：RankEnabled=false 查战绩榜 → 7001 + rankEnabled=false</summary>
    [Fact]
    public async Task ExecuteAsync_PerformanceDisabled_Returns7001()
    {
        SetUser(71003);
        var group = await SeedGroupAsync("group-rank-71003", rankEnabled: false);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Performance",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(RankErrorCodes.RankPerformanceDisabled, result.ErrorCode);
        Assert.False(result.RankEnabled);
    }

    /// <summary>BR-04：RankEnabled=false 查战力榜 → 正常返回</summary>
    [Fact]
    public async Task ExecuteAsync_CombatUnaffectedByRankEnabled()
    {
        SetUser(71004);
        var group = await SeedGroupAsync("group-rank-71004", rankEnabled: false);
        var today = Today();
        await SeedSnapshotAsync(71041, group.UId, RankMetricType.Streak, 2, 1, today);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success); // 战力榜不受 RankEnabled 影响
        Assert.Single(result.Items);
    }

    /// <summary>BR-01：无快照 → 空列表（码 0）</summary>
    [Fact]
    public async Task ExecuteAsync_NoSnapshots_ReturnsEmpty()
    {
        SetUser(71005);
        var group = await SeedGroupAsync("group-rank-71005", rankEnabled: true);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-03：非法 Scope → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidScope_ReturnsParamInvalid()
    {
        SetUser(71006);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "BadScope", ScopeId = "x", Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(RankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>战绩榜：Accuracy/Mastery 单指标直读（Value 仍为 accuracy+mastery 求和）</summary>
    [Fact]
    public async Task ExecuteAsync_PerformanceRanking_ExposesSingleMetrics()
    {
        SetUser(71007);
        var group = await SeedGroupAsync("group-rank-71031", rankEnabled: true);
        var today = Today();
        // 用户 A：accuracy=0.75 + mastery=0.6 → 求和 1.35
        await SeedSnapshotAsync(71031, group.UId, RankMetricType.Accuracy, 0.75m, 1, today);
        await SeedSnapshotAsync(71031, group.UId, RankMetricType.Mastery, 0.6m, 1, today);
        // 用户 B：accuracy=0.7 + mastery=0.5 → 求和 1.2
        await SeedSnapshotAsync(71032, group.UId, RankMetricType.Accuracy, 0.7m, 2, today);
        await SeedSnapshotAsync(71032, group.UId, RankMetricType.Mastery, 0.5m, 2, today);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Performance",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(71031, result.Items[0].UserId); // A=1.35 > B=1.2 按求和排序
        Assert.Equal(1.35m, result.Items[0].Value);
        Assert.Equal(0.75, result.Items[0].Accuracy);
        Assert.Equal(0.6, result.Items[0].Mastery);
        Assert.Equal(1.2m, result.Items[1].Value);
        Assert.Equal(0.7, result.Items[1].Accuracy);
    }

    /// <summary>战绩榜：缺失单指标行 → 该项默认 0</summary>
    [Fact]
    public async Task ExecuteAsync_Performance_MissingMetricDefaultsToZero()
    {
        SetUser(71008);
        var group = await SeedGroupAsync("group-rank-71041", rankEnabled: true);
        var today = Today();
        // 用户仅有 Accuracy 行（无 Mastery 行）
        await SeedSnapshotAsync(71041, group.UId, RankMetricType.Accuracy, 0.9m, 1, today);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Performance",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal(0.9, result.Items[0].Accuracy);
        Assert.Equal(0, result.Items[0].Mastery); // 缺失 → 默认 0
    }

    /// <summary>战力榜：Streak/Volume/PkWins 单指标直读（Value 仍为 streak+volume+pk_wins 求和）</summary>
    [Fact]
    public async Task ExecuteAsync_CombatRanking_ExposesSingleMetrics()
    {
        SetUser(71009);
        var group = await SeedGroupAsync("group-rank-71051", rankEnabled: false);
        var today = Today();
        // 用户 A：streak=12 + volume=45 + pk_wins=3 → 求和 60（BR-11）
        await SeedSnapshotAsync(71051, group.UId, RankMetricType.Streak, 12m, 1, today);
        await SeedSnapshotAsync(71051, group.UId, RankMetricType.Volume, 45m, 1, today);
        await SeedSnapshotAsync(71051, group.UId, RankMetricType.PkWins, 3m, 1, today);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success); // 战力榜不受 RankEnabled 影响（BR-04）
        Assert.Single(result.Items);
        Assert.Equal(60m, result.Items[0].Value);
        Assert.Equal(12, result.Items[0].Streak);
        Assert.Equal(45, result.Items[0].Volume);
        Assert.Equal(3, result.Items[0].PkWins);
    }

    /// <summary>战力榜：缺失单指标行 → 该项默认 0</summary>
    [Fact]
    public async Task ExecuteAsync_Combat_MissingMetricDefaultsToZero()
    {
        SetUser(71010);
        var group = await SeedGroupAsync("group-rank-71061", rankEnabled: true);
        var today = Today();
        // 用户仅有 Streak 行（无 Volume/PkWins 行）
        await SeedSnapshotAsync(71061, group.UId, RankMetricType.Streak, 7m, 1, today);
        var svc = User.Use<GetRankingsService>();

        var result = await svc.ExecuteAsync(new GetRankingsReqDto
        {
            ScopeType = "Group", ScopeId = group.UId, Metric = "Combat",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal(7, result.Items[0].Streak);
        Assert.Equal(0, result.Items[0].Volume); // 缺失 → 默认 0
        Assert.Equal(0, result.Items[0].PkWins);
    }
}