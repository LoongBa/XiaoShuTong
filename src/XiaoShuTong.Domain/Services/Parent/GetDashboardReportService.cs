using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.7：成长总览
/// </summary>
/// <remarks>
/// BR-15 未订阅：仅前 2 项 + locked=true | BR-16 无数据引导空态 | BR-17 试用过期 → 8003
/// BR-18 合规无排名 | BR-19 链式聚合（DailyStats/TaskAssignments/KnowledgeMastery）
/// VEntity 设计规格：三表链式聚合 + 动态作用域 → Service 编排（不建视图）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetDashboardReportService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    /// <summary>
    /// 成长总览（订阅门控：前 2 项预览 / 完整聚合）
    /// </summary>
    public async Task<GetDashboardReportResDto> ExecuteAsync(GetDashboardReportReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // 订阅门控（BR-15/BR-17）
        var gate = await ParentReportGate.CheckAsync(RelationsDs, SubscriptionsDs, parentId, request.StudentId, allowPreview: true, ct);
        if (!gate.Allowed)
            return new GetDashboardReportResDto { Success = false, ErrorCode = gate.ErrorCode };

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

        // 预览项（前 2 项，任何权益均可看）：今日任务完成 + 坚持天数
        var dailyStats = await DailyDs.EntitySelectAsync(
            x => x.UserId == request.StudentId, ct: ct);
        var streakDays = StreakCalculator.CalcCurrentStreak(dailyStats.Select(d => d.StatDate).ToList(), today);

        // 今日任务完成（跨模块任务域）
        var todayAssignments = await AssignmentsDs.EntitySelectAsync(
            x => x.UserId == request.StudentId, ct: ct);
        var todayCompleted = todayAssignments.Any(a => a.Status == AssignmentStatus.Completed);

        // BR-16：无数据 → 引导空态标记（码 0，非错误）
        var hasData = dailyStats.Count > 0 || todayAssignments.Count > 0;
        if (gate.Locked)
        {
            // 未订阅：仅前 2 项 + locked（BR-15）
            return new GetDashboardReportResDto
            {
                Success = true,
                Subscription = gate.Subscription == null ? null : new SubscriptionBriefDto
                {
                    Status = gate.Subscription.Status.ToString(),
                    TrialEndAt = gate.Subscription.TrialEndAt,
                },
                TodayCompleted = hasData ? todayCompleted : null,
                StreakDays = streakDays,
                Locked = true,
            };
        }

        // 完整项：本周进度 + 学科掌握度（BR-19 链式聚合）
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var weekDaily = dailyStats.Where(d => d.StatDate >= weekStart && d.StatDate <= today).ToList();
        var weekLearned = weekDaily.Sum(d => d.LearnedCount);
        var weekAccuracy = weekDaily.Where(d => d.Accuracy.HasValue).Select(d => d.Accuracy!.Value).ToList();

        var masteryRows = await MasteryDs.EntitySelectAsync(
            x => x.UserId == request.StudentId, ct: ct);
        var subjectsMastery = masteryRows
            .GroupBy(m => m.Subject)
            .Select(g => new SubjectMasteryDto
            {
                Subject = g.Key,
                Accuracy = Math.Round(g.Average(m => m.Accuracy), 4),
            })
            .ToList();

        // BR-18：合规——无群组正确率排名字段
        return new GetDashboardReportResDto
        {
            Success = true,
            Subscription = gate.Subscription == null ? null : new SubscriptionBriefDto
            {
                Status = gate.Subscription.Status.ToString(),
                TrialEndAt = gate.Subscription.TrialEndAt,
            },
            TodayCompleted = hasData ? todayCompleted : null,
            StreakDays = streakDays,
            WeekProgress = new WeekProgressDto
            {
                LearnedCount = weekLearned,
                Accuracy = weekAccuracy.Count == 0 ? null : Math.Round(weekAccuracy.Average(), 4),
            },
            SubjectsMastery = subjectsMastery,
            Locked = false,
        };
    }
}

/// <summary>成长总览请求 DTO</summary>
public sealed record GetDashboardReportReqDto
{
    /// <summary>孩子 Id</summary>
    public long StudentId { get; init; }
}

/// <summary>成长总览响应 DTO（合规：无群组正确率排名）</summary>
public sealed record GetDashboardReportResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>订阅状态</summary>
    public SubscriptionBriefDto? Subscription { get; init; }

    /// <summary>今日任务完成（预览项）</summary>
    public bool? TodayCompleted { get; init; }

    /// <summary>坚持天数（预览项）</summary>
    public int? StreakDays { get; init; }

    /// <summary>本周进度（完整）</summary>
    public WeekProgressDto? WeekProgress { get; init; }

    /// <summary>学科掌握度（完整）</summary>
    public List<SubjectMasteryDto> SubjectsMastery { get; init; } = [];

    /// <summary>未订阅标记（完整内容锁定）</summary>
    public bool Locked { get; init; }
}

/// <summary>订阅摘要 DTO</summary>
public sealed record SubscriptionBriefDto
{
    /// <summary>状态</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>试用结束</summary>
    public DateTime? TrialEndAt { get; init; }
}

/// <summary>本周进度 DTO</summary>
public sealed record WeekProgressDto
{
    /// <summary>本周学习题数</summary>
    public int LearnedCount { get; init; }

    /// <summary>本周正确率</summary>
    public double? Accuracy { get; init; }
}

/// <summary>学科掌握度 DTO</summary>
public sealed record SubjectMasteryDto
{
    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>聚合正确率</summary>
    public double Accuracy { get; init; }
}