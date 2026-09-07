using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Services.Rank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Rank;

/// <summary>
/// UC-7.3 排名快照冻结（RankSnapshotFreezeJob）Contract 测试
/// 覆盖 BR：BR-09 无数据跳过 | BR-11 战力指标求和 | BR-12 战绩指标求和 | BR-13 幂等 upsert
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class RankSnapshotFreezeJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

    private async Task<Groups> SeedGroupAsync(string groupUid, params long[] studentIds)
    {
        var ds = User.Use<GroupsDataService>();
        var group = await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = 73001,
            Name = "冻结群组",
            Subject = "chinese",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);

        var membersDs = User.Use<GroupMembersDataService>();
        foreach (var studentId in studentIds)
        {
            await membersDs.EntityCreateAsync(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = studentId,
                Role = MemberRole.Student,
                JoinedAt = DateTime.UtcNow,
            }, TestContext.Current.CancellationToken);
        }
        return group;
    }

    private async Task SeedDailyAsync(long userId, DateOnly date, int learned)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            StatDate = date,
            LearnedCount = learned,
            StarredCount = 1,
            Accuracy = 0.8,
            StudySeconds = 300,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedMasteryAsync(long userId, double accuracy)
    {
        var ds = User.Use<KnowledgeMasteryDataService>();
        await ds.EntityCreateAsync(new KnowledgeMastery
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Subject = "chinese",
            KnowledgePoint = $"KP-{Guid.NewGuid():N}"[..8],
            State = MemoryState.Fuzzy,
            Accuracy = accuracy,
            AttemptCount = 1,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-11/12：冻结聚合（volume 累计、accuracy 均值、mastery 均值、streak 连击）</summary>
    [Fact]
    public async Task ExecuteAsync_AggregatesMetricsAndWritesSnapshots()
    {
        var today = Today();
        var group = await SeedGroupAsync("group-freeze-73101", 73111, 73112);
        await SeedDailyAsync(73111, today.AddDays(-1), learned: 3);
        await SeedDailyAsync(73111, today, learned: 7);      // volume=10
        await SeedDailyAsync(73112, today, learned: 5);      // volume=5
        await SeedMasteryAsync(73111, 0.5);                   // mastery=0.5
        await SeedMasteryAsync(73112, 1.0);                   // mastery=1.0
        var job = User.Use<RankSnapshotFreezeJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var snapshotsDs = User.Use<RankSnapshotsDataService>();
        var snapshots = await snapshotsDs.EntitySelectAsync(
            x => x.ScopeId == group.UId, ct: TestContext.Current.CancellationToken);

        // volume：用户A=10 > 用户B=5
        var volumeA = snapshots.First(s => s.UserId == 73111 && s.MetricType == RankMetricType.Volume);
        var volumeB = snapshots.First(s => s.UserId == 73112 && s.MetricType == RankMetricType.Volume);
        Assert.Equal(10m, volumeA.MetricValue);
        Assert.Equal(5m, volumeB.MetricValue);
        Assert.Equal(1, volumeA.Rank); // 值大者名次前
        Assert.Equal(2, volumeB.Rank);

        // mastery：B=1.0 > A=0.5
        var masteryB = snapshots.First(s => s.UserId == 73112 && s.MetricType == RankMetricType.Mastery);
        Assert.Equal(1.0m, masteryB.MetricValue);

        // accuracy：均值 0.8
        var accuracyA = snapshots.First(s => s.UserId == 73111 && s.MetricType == RankMetricType.Accuracy);
        Assert.Equal(0.8m, accuracyA.MetricValue);

        // streak：A 有今日+昨日 → 2
        var streakA = snapshots.First(s => s.UserId == 73111 && s.MetricType == RankMetricType.Streak);
        Assert.Equal(2m, streakA.MetricValue);
    }

    /// <summary>BR-09：无成员群组 → 不产生快照</summary>
    [Fact]
    public async Task ExecuteAsync_GroupWithoutMembers_Skips()
    {
        var group = await SeedGroupAsync("group-freeze-73102");
        var job = User.Use<RankSnapshotFreezeJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var snapshotsDs = User.Use<RankSnapshotsDataService>();
        var snapshots = await snapshotsDs.EntitySelectAsync(
            x => x.ScopeId == group.UId, ct: TestContext.Current.CancellationToken);
        Assert.Empty(snapshots);
    }

    /// <summary>BR-13：重复执行幂等（upsert 不重复写）</summary>
    [Fact]
    public async Task ExecuteAsync_Rerun_Idempotent()
    {
        var today = Today();
        var group = await SeedGroupAsync("group-freeze-73103", 73131);
        await SeedDailyAsync(73131, today, learned: 5);
        var job = User.Use<RankSnapshotFreezeJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);
        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var snapshotsDs = User.Use<RankSnapshotsDataService>();
        var snapshots = await snapshotsDs.EntitySelectAsync(
            x => x.ScopeId == group.UId, ct: TestContext.Current.CancellationToken);
        // 5 个指标类型 × 1 用户 × 1 日期 = 5 条（幂等无重复）
        Assert.Equal(5, snapshots.Count);
    }
}