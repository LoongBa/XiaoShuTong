using System.Globalization;
using System.Text;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// F9：群组执行周报导出（动态周窗口聚合，Service 层聚合，不走 VEntity）
/// </summary>
/// <remarks>
/// 任务-BR-24 周报口径：执行率 = 周窗口内 Completed / 全部分配（分母含 Overdue，同 BR-17）；
/// 平均进度 = 周内分配 Progress 均值；学习量 = 周内 DailyStats.LearnedCount 求和
/// 任务-BR-25 合规：CSV 仅含成员汇总指标（昵称/执行率/平均进度/学习量），不含答题明细与正确率排名
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ExportWeeklyReportService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const string CsvTtl = "mock://weekly-report";

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    /// <summary>
    /// 群组执行周报（预览聚合 + 成员级 CSV 导出）
    /// </summary>
    public async Task<ExportWeeklyReportResDto> ExecuteAsync(ExportWeeklyReportReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-16（沿用）：群组必须存在且为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.UId == request.GroupUid, ct);
        if (group == null)
            return new ExportWeeklyReportResDto { Success = false, ErrorCode = TaskErrorCodes.GroupNotFound };
        if (group.OwnerId != ownerId)
            return new ExportWeeklyReportResDto { Success = false, ErrorCode = TaskErrorCodes.Forbidden };

        // 动态周窗口：WeekStart 默认本周一（UTC+8），窗口 = [WeekStart, WeekStart+6]
        var weekStart = request.WeekStart ?? GetMonday(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)));
        var weekEnd = weekStart.AddDays(6);

        // 周窗口内任务（StartedAt ∈ 窗口）——一次 IN 查询替代 foreach N+1
        var windowStartUtc = weekStart.ToDateTime(TimeOnly.MinValue);
        var windowEndExclusiveUtc = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue);
        var tasks = await TasksDs.EntitySelectAsync(
            x => x.GroupId == group.Id && x.StartedAt >= windowStartUtc && x.StartedAt < windowEndExclusiveUtc, ct: ct);
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var allAssignments = taskIds.Length == 0
            ? new List<TaskAssignments>()
            : await AssignmentsDs.EntitySelectAsync(x => taskIds.Contains(x.TaskId), ct: ct);

        // 群组 Student 成员 + 周窗口内学习量（UserId IN + StatDate range，一次查询）
        var members = await MembersDs.EntitySelectAsync(
            x => x.GroupId == group.Id && x.Role == MemberRole.Student, ct: ct);
        var memberIds = members.Select(m => m.UserId).ToArray();
        var dailyStats = memberIds.Length == 0
            ? new List<DailyStats>()
            : await DailyDs.EntitySelectAsync(
                x => memberIds.Contains(x.UserId) && x.StatDate >= weekStart && x.StatDate <= weekEnd, ct: ct);

        // 任务-BR-24：全局聚合口径
        var totalAssignments = allAssignments.Count;
        var completedCount = allAssignments.Count(a => a.Status == AssignmentStatus.Completed);
        var executionRate = totalAssignments == 0 ? 0d : Math.Round((double)completedCount / totalAssignments, 2);
        var avgProgress = totalAssignments == 0 ? 0 : RoundToInt(allAssignments.Average(a => a.Progress));
        var learnedCount = dailyStats.Sum(d => d.LearnedCount);

        // 成员明细（内存聚合，无 N+1）
        var learnedByUser = dailyStats
            .GroupBy(d => d.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.LearnedCount));
        var memberReports = members.Select(m =>
        {
            var memberAssignments = allAssignments.Where(a => a.UserId == m.UserId).ToList();
            var memberCompleted = memberAssignments.Count(a => a.Status == AssignmentStatus.Completed);
            return new WeeklyMemberReportDto
            {
                UserId = m.UserId,
                Nickname = m.Nickname,
                ExecutionRate = memberAssignments.Count == 0 ? 0d
                    : Math.Round((double)memberCompleted / memberAssignments.Count, 2),
                AvgProgress = memberAssignments.Count == 0 ? 0 : RoundToInt(memberAssignments.Average(a => a.Progress)),
                LearnedCount = learnedByUser.TryGetValue(m.UserId, out var learned) ? learned : 0,
            };
        }).ToList();

        // 任务-BR-25：CSV 仅成员汇总指标（不含正确率/答题明细）；周报即时生成，不落库 → mock OSS 链接
        var csv = BuildCsv(memberReports);
        var csvFileUrl = $"{CsvTtl}/{group.Id}-{weekStart:yyyyMMdd}.csv";

        return new ExportWeeklyReportResDto
        {
            Success = true,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            TaskCount = tasks.Count,
            MemberCount = members.Count,
            TotalAssignments = totalAssignments,
            CompletedCount = completedCount,
            ExecutionRate = executionRate,
            AvgProgress = avgProgress,
            LearnedCount = learnedCount,
            MemberReports = memberReports,
            CsvFileUrl = csvFileUrl,
        };
    }

    /// <summary>最近一个周一（UTC+8 业务日期）</summary>
    private static DateOnly GetMonday(DateOnly today) => today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

    private static int RoundToInt(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// 构建成员级 CSV（header：昵称,执行率,平均进度,学习量；不含正确率/答题明细，BR-25）
    /// </summary>
    private static string BuildCsv(List<WeeklyMemberReportDto> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("昵称,执行率,平均进度,学习量");
        foreach (var r in reports)
        {
            sb.AppendLine(string.Join(',',
                r.Nickname ?? string.Empty,
                r.ExecutionRate.ToString("0.##", CultureInfo.InvariantCulture),
                r.AvgProgress.ToString(CultureInfo.InvariantCulture),
                r.LearnedCount.ToString(CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }
}

/// <summary>周报导出请求 DTO</summary>
public sealed record ExportWeeklyReportReqDto
{
    /// <summary>群组外部键</summary>
    public string GroupUid { get; init; } = string.Empty;

    /// <summary>周起始日（UTC+8 业务日期；为空默认本周一）</summary>
    public DateOnly? WeekStart { get; init; }
}

/// <summary>周报导出响应 DTO</summary>
public sealed record ExportWeeklyReportResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>周起始日</summary>
    public DateOnly WeekStart { get; init; }

    /// <summary>周结束日（WeekStart+6）</summary>
    public DateOnly WeekEnd { get; init; }

    /// <summary>周窗口内任务数</summary>
    public int TaskCount { get; init; }

    /// <summary>群组 Student 成员数</summary>
    public int MemberCount { get; init; }

    /// <summary>周窗口内全部分配数（分母含 Overdue）</summary>
    public int TotalAssignments { get; init; }

    /// <summary>其中已完成分配数</summary>
    public int CompletedCount { get; init; }

    /// <summary>执行率（Completed / TotalAssignments，Total=0 → 0）</summary>
    public double ExecutionRate { get; init; }

    /// <summary>平均进度（周内全部分配 Progress 均值，int）</summary>
    public int AvgProgress { get; init; }

    /// <summary>学习量（周内 DailyStats.LearnedCount 求和）</summary>
    public int LearnedCount { get; init; }

    /// <summary>成员汇总明细（BR-25：不含正确率/答题明细）</summary>
    public List<WeeklyMemberReportDto> MemberReports { get; init; } = [];

    /// <summary>CSV 下载链接（mock OSS，周报即时生成不落库）</summary>
    public string? CsvFileUrl { get; init; }
}

/// <summary>周报成员汇总 DTO</summary>
public sealed record WeeklyMemberReportDto
{
    /// <summary>成员用户 Id</summary>
    public long UserId { get; init; }

    /// <summary>群内昵称</summary>
    public string? Nickname { get; init; }

    /// <summary>该成员周内执行率（完成分配数/全部分配数）</summary>
    public double ExecutionRate { get; init; }

    /// <summary>该成员周内平均进度</summary>
    public int AvgProgress { get; init; }

    /// <summary>该成员周内学习量（DailyStats 求和）</summary>
    public int LearnedCount { get; init; }
}