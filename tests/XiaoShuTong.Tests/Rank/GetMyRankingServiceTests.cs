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
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
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
}