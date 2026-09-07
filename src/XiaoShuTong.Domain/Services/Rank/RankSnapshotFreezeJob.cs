using TKW.Framework.Domain;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Rank;

/// <summary>
/// UC-7.3：排名快照每日冻结（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度：Hangfire 每日 0:00(UTC+8)（切片验证：测试直接调用）。
/// BR-09 无指标数据跳过（幂等）| BR-10 单范围失败不中断整批 | BR-11 战力 = streak+volume+pk_wins（简单求和）
/// BR-12 战绩 = accuracy+mastery（简单求和）| BR-13 同一 (User,Scope,Subject,Metric,Date) 幂等 upsert
/// 跨模块：streak/volume = DailyStats、mastery = KnowledgeMastery、pk_wins = PkPlayerStats（切片 07 未实施 → 桩 0）。
/// 范围：本切片冻结群组范围（ScopeType=Group，ScopeId=群组 Uid）；年级范围（平台同年级）留待扩展。
/// </remarks>
internal class RankSnapshotFreezeJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    private RankSnapshotsDataService? _snapshotsDs;
    private RankSnapshotsDataService SnapshotsDs => _snapshotsDs ??= User.Use<RankSnapshotsDataService>();

    /// <summary>
    /// 扫描全部激活群组 → 逐组聚合成员指标 → 幂等写入当日快照
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)); // UTC+8 业务日

        var groups = await GroupsDs.EntitySelectAsync(
            x => x.Status == GroupStatus.Active, ct: ct);

        foreach (var group in groups)
        {
            try
            {
                await FreezeGroupAsync(group, snapshotDate, ct);
            }
            catch
            {
                // BR-10：单范围失败记录日志（切片吞异常继续），不中断整批
            }
        }
    }

    private async Task FreezeGroupAsync(Groups group, DateOnly snapshotDate, CancellationToken ct)
    {
        var members = await MembersDs.EntitySelectAsync(
            x => x.GroupId == group.Id && x.Role == MemberRole.Student, ct: ct);
        var memberIds = members.Select(m => m.UserId).ToArray();

        // BR-09：无成员 → 跳过该范围
        if (memberIds.Length == 0)
            return;

        // 逐用户聚合指标
        var userMetrics = new Dictionary<long, Dictionary<RankMetricType, decimal>>();
        var today = snapshotDate;

        foreach (var memberId in memberIds)
        {
            var dailyStats = await DailyDs.EntitySelectAsync(
                x => x.UserId == memberId, ct: ct);
            var dates = dailyStats.Select(d => d.StatDate).ToList();
            var mastery = await MasteryDs.EntitySelectAsync(
                x => x.UserId == memberId, ct: ct);

            // streak = 当前连击（BR-11 战力组成）
            var streak = (decimal)StreakCalculator.CalcCurrentStreak(dates, today);
            // volume = DailyStats.LearnedCount 累计（已决策口径）
            var volume = dailyStats.Sum(d => d.LearnedCount);
            // accuracy = 有记录日正确率均值
            var accuracies = dailyStats.Where(d => d.Accuracy.HasValue).Select(d => d.Accuracy!.Value).ToList();
            var accuracy = accuracies.Count == 0 ? 0m : (decimal)Math.Round(accuracies.Average(), 4);
            // mastery = KnowledgeMastery.Accuracy 均值
            var masteryAcc = mastery.Count == 0 ? 0m : (decimal)Math.Round(mastery.Average(m => m.Accuracy), 4);
            // pk_wins = PkPlayerStats（切片 07 未实施 → 桩 0）
            var pkWins = 0m;

            userMetrics[memberId] = new Dictionary<RankMetricType, decimal>
            {
                [RankMetricType.Streak] = streak,
                [RankMetricType.Volume] = volume,
                [RankMetricType.Accuracy] = accuracy,
                [RankMetricType.Mastery] = masteryAcc,
                [RankMetricType.PkWins] = pkWins,
            };
        }

        // BR-09：无指标数据 → 跳过（所有用户无任何指标）
        if (userMetrics.All(kv => kv.Value.Values.All(v => v == 0m)))
            return;

        // 逐指标排名 + 幂等 upsert（BR-13）
        foreach (var metric in new[] { RankMetricType.Streak, RankMetricType.Volume, RankMetricType.PkWins, RankMetricType.Accuracy, RankMetricType.Mastery })
        {
            var ordered = userMetrics
                .Select(kv => new { kv.Key, Value = kv.Value.GetValueOrDefault(metric) })
                .OrderByDescending(x => x.Value)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                await UpsertSnapshotAsync(group.UId, row.Key, metric, row.Value, i + 1, snapshotDate, ct);
            }
        }
    }

    /// <summary>BR-13：同一 (User,Scope,Subject,Metric,Date) 幂等 upsert</summary>
    private async Task UpsertSnapshotAsync(
        string groupUid, long userId, RankMetricType metric, decimal value, int rank, DateOnly date, CancellationToken ct)
    {
        var existing = await SnapshotsDs.EntityGetAsync(
            x => x.UserId == userId && x.ScopeType == RankScopeType.Group && x.ScopeId == groupUid
                 && x.Subject == "All" && x.MetricType == metric && x.SnapshotDate == date, ct);

        if (existing == null)
        {
            await SnapshotsDs.EntityCreateAsync(new RankSnapshots
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
            }, ct);
        }
        else
        {
            existing.MetricValue = value;
            existing.Rank = rank;
            await SnapshotsDs.EntityUpdateAsync(existing, ct);
        }
    }
}