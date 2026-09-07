using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Services.Buddy;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Buddy;

/// <summary>
/// UC-8.4 搭子列表（ListBuddiesService）Contract 测试
/// 覆盖 BR：BR-24 空态 | BR-25 仅 accepted（UNION 双向）| BR-26 排名快照 | BR-27 不暴露答题明细
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListBuddiesServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedBuddyAsync(long inviterId, long inviteeId, BuddyStatus status)
    {
        var now = DateTime.UtcNow;
        var ds = User.Use<StudyBuddiesDataService>();
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = inviterId,
            InviteeId = inviteeId,
            Status = status,
            InvitedAt = now,
            ExpiresAt = now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedDailyAsync(long userId, DateOnly date)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            StatDate = date,
            LearnedCount = 1,
            StudySeconds = 300,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedSnapshotAsync(long userId, int rank)
    {
        var ds = User.Use<RankSnapshotsDataService>();
        await ds.EntityCreateAsync(new RankSnapshots
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            ScopeType = RankScopeType.Group,
            ScopeId = "group-list",
            Subject = "All",
            MetricType = RankMetricType.Streak,
            MetricValue = rank * 2m,
            Rank = rank,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-25：UNION 双向（我邀请 + 我受邀）+ 仅 accepted</summary>
    [Fact]
    public async Task ExecuteAsync_BothDirections_OnlyAccepted()
    {
        var userId = SetUser(84001);
        await SeedBuddyAsync(userId, 84101, BuddyStatus.Accepted);   // 我邀请
        await SeedBuddyAsync(84102, userId, BuddyStatus.Accepted);   // 我受邀
        await SeedBuddyAsync(userId, 84103, BuddyStatus.Pending);    // pending 排除
        var svc = User.Use<ListBuddiesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count); // 仅 accepted 双向
        Assert.Contains(result.Items, i => i.UserId == 84101);
        Assert.Contains(result.Items, i => i.UserId == 84102);
        Assert.DoesNotContain(result.Items, i => i.UserId == 84103);
    }

    /// <summary>BR-24：无搭子 → 空列表</summary>
    [Fact]
    public async Task ExecuteAsync_NoBuddies_ReturnsEmpty()
    {
        SetUser(84002);
        var svc = User.Use<ListBuddiesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-26/27：搭子连续打卡天数 + 排名快照（最小化暴露）</summary>
    [Fact]
    public async Task ExecuteAsync_StreakDaysAndRankSnapshot()
    {
        var userId = SetUser(84003);
        await SeedBuddyAsync(userId, 84301, BuddyStatus.Accepted);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        await SeedDailyAsync(84301, today.AddDays(-1));
        await SeedDailyAsync(84301, today);
        await SeedSnapshotAsync(84301, rank: 3);
        var svc = User.Use<ListBuddiesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(84301, item.UserId);
        Assert.Equal(2, item.StreakDays);   // 连续 2 天打卡
        Assert.NotNull(item.Rank);
        Assert.Equal(3, item.Rank!.Rank);
        Assert.Equal(6m, item.Rank!.MetricValue);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase); // 不暴露答题明细
    }
}