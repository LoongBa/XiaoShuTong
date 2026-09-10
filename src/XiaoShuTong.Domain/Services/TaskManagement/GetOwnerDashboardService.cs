using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Entities.TaskManagement.DTOs;
using XiaoShuTong.Services.Shared;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.4：群主执行看板
/// </summary>
/// <remarks>
/// BR-14 无任务空态 | BR-15 无学生空态 | BR-16 群组不存在/非 Owner | BR-17 执行率 = completed / 全部分配（含逾期）
/// BR-18 学生执行表按完成度展示，不按正确率排名 | BR-19 薄弱点 Top5 按掌握度正确率升序
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetOwnerDashboardService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    /// <summary>
    /// 群主看板（指标卡 + 任务列表 + 薄弱点 Top5）
    /// </summary>
    public async Task<GetOwnerDashboardResDto> ExecuteAsync(GetOwnerDashboardReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-16：群组必须存在且为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.UId == request.GroupUid, ct);
        if (group == null)
            return new GetOwnerDashboardResDto { Success = false, ErrorCode = TaskErrorCodes.GroupNotFound };
        if (group.OwnerId != ownerId)
            return new GetOwnerDashboardResDto { Success = false, ErrorCode = TaskErrorCodes.Forbidden };

        // 本群任务 + 全部分配（一次 IN 查询替代 foreach N+1）
        var tasks = await TasksDs.EntitySelectAsync(
            x => x.GroupId == group.Id, ct: ct);
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var allAssignments = taskIds.Length == 0
            ? new List<TaskAssignments>()
            : await AssignmentsDs.EntitySelectAsync(x => taskIds.Contains(x.TaskId), ct: ct);

        // BR-14：无任务 → 空态数据（指标为零）
        if (tasks.Count == 0 || allAssignments.Count == 0)
        {
            return new GetOwnerDashboardResDto
            {
                Success = true,
                TodayExecutionRate = 0,
                AvgProgress = 0,
                OverdueCount = 0,
                WeakPointsTop5 = await BuildWeakPointsAsync(group.Id, ownerId, ct),
                TaskList = tasks.Select(t => new DashboardTaskDto
                {
                    Task = t.ToDto(),
                    CompletionRate = 0,
                    Status = "Normal",
                }).ToList(),
            };
        }

        // BR-17：执行率 = 已完成分配 / 全部分配（分母含逾期）
        var completedCount = allAssignments.Count(a => a.Status == AssignmentStatus.Completed);
        var todayExecutionRate = Math.Round((double)completedCount / allAssignments.Count, 2);

        // 平均进度（含全部分配）
        var avgProgress = Math.Round(allAssignments.Average(a => a.Progress), 2);

        // 逾期数
        var overdueCount = allAssignments.Count(a => a.Status == AssignmentStatus.Overdue);

        // 任务列表（完成率 + Normal/Overdue）
        var taskList = tasks.Select(t =>
        {
            var taskAssignments = allAssignments.Where(a => a.TaskId == t.Id).ToList();
            var taskCompleted = taskAssignments.Count(a => a.Status == AssignmentStatus.Completed);
            var rate = taskAssignments.Count == 0 ? 0d
                : Math.Round((double)taskCompleted / taskAssignments.Count, 2);
            var hasOverdue = taskAssignments.Any(a => a.Status == AssignmentStatus.Overdue);
            return new DashboardTaskDto
            {
                Task = t.ToDto(),
                CompletionRate = rate,
                Status = hasOverdue ? "Overdue" : "Normal",
            };
        }).ToList();

        // BR-19：薄弱点 Top5（掌握度正确率升序，跨组员聚合）
        var weakPoints = await BuildWeakPointsAsync(group.Id, ownerId, ct);

        return new GetOwnerDashboardResDto
        {
            Success = true,
            TodayExecutionRate = todayExecutionRate,
            AvgProgress = avgProgress,
            OverdueCount = overdueCount,
            WeakPointsTop5 = weakPoints,
            TaskList = taskList,
        };
    }

    /// <summary>BR-19：组内成员 KnowledgeMastery 按知识点聚合，正确率升序取 Top5</summary>
    private async Task<List<WeakPointDto>> BuildWeakPointsAsync(long groupId, long ownerId, CancellationToken ct)
    {
        var members = await MembersDs.EntitySelectAsync(
            x => x.GroupId == groupId && x.Role == MemberRole.Student, ct: ct);
        var memberIds = members.Select(m => m.UserId).ToArray();
        if (memberIds.Length == 0)
            return [];

        // 一次 IN 查询替代 foreach N+1
        var masteryRows = await MasteryDs.EntitySelectAsync(
            x => memberIds.Contains(x.UserId), ct: ct);

        return masteryRows
            .GroupBy(x => x.KnowledgePoint)
            .Select(g => new WeakPointDto
            {
                KnowledgePoint = g.Key,
                Accuracy = Math.Round(g.Average(x => x.Accuracy), 4),
            })
            .OrderBy(x => x.Accuracy) // 正确率升序：最薄弱在前
            .Take(5)
            .ToList();
    }
}

/// <summary>群主看板请求 DTO</summary>
public sealed record GetOwnerDashboardReqDto
{
    /// <summary>群组外部键</summary>
    public string GroupUid { get; init; } = string.Empty;
}

/// <summary>群主看板响应 DTO</summary>
public sealed record GetOwnerDashboardResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>今日执行率（已完成/全部分配，分母含全部）</summary>
    public double TodayExecutionRate { get; init; }

    /// <summary>平均进度（含全部分配）</summary>
    public double AvgProgress { get; init; }

    /// <summary>逾期任务/分配数</summary>
    public int OverdueCount { get; init; }

    /// <summary>薄弱知识点 Top5（掌握度正确率升序）</summary>
    public List<WeakPointDto> WeakPointsTop5 { get; init; } = [];

    /// <summary>任务列表</summary>
    public List<DashboardTaskDto> TaskList { get; init; } = [];
}

/// <summary>看板任务项 DTO（复用 TasksDto + 完成率/状态壳）</summary>
public sealed record DashboardTaskDto
{
    /// <summary>任务信息（复用 TasksDto）</summary>
    public TasksDto? Task { get; init; }

    /// <summary>完成任务率</summary>
    public double CompletionRate { get; init; }

    /// <summary>状态（Normal/Overdue）</summary>
    public string Status { get; init; } = string.Empty;
}