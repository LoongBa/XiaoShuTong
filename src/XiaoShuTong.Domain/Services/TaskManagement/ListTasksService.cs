using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.2：任务列表 / 详情（群主视角）
/// </summary>
/// <remarks>
/// BR-07 任务不存在 → 5101 | BR-08 学生仅见本人分配（RLS）| BR-09 群主仅见自己布置的任务 | BR-10 详情不按正确率排名（合规红线）
/// 完成任务率 = 已完成分配 / 全部分配（含逾期）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListTasksService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    /// <summary>
    /// 群主按群组/状态查看自己布置的任务（分页 + 完成率）
    /// </summary>
    public async Task<ListTasksResDto> ExecuteAsync(ListTasksReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // 群组过滤（GroupUid → GroupId）
        long? groupId = null;
        if (!string.IsNullOrWhiteSpace(request.GroupUid))
        {
            var group = await GroupsDs.EntityGetAsync(x => x.UId == request.GroupUid, ct);
            if (group == null)
                return new ListTasksResDto { Success = false, ErrorCode = TaskErrorCodes.GroupNotFound };
            if (group.OwnerId != ownerId)
                return new ListTasksResDto { Success = false, ErrorCode = TaskErrorCodes.Forbidden };
            groupId = group.Id;
        }

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        // BR-09：群主仅见自己布置的任务（OwnerId 过滤）
        System.Linq.Expressions.Expression<Func<Tasks, bool>> predicate = x =>
            x.OwnerId == ownerId
            && (!groupId.HasValue || x.GroupId == groupId.Value)
            && (string.IsNullOrWhiteSpace(request.Status) || x.Status.ToString() == request.Status);

        var items = await TasksDs.EntitySelectAsync(
            predicate,
            (pageIndex - 1) * pageSize,
            pageSize,
            q => q.OrderByDescending(x => x.CreateTime),
            ct);
        var totalCount = await TasksDs.CountAsync(predicate, ct);

        // 一次 IN 查询替代 foreach N+1：按 TaskId 分组组装完成率
        var taskIds = items.Select(t => t.Id).ToArray();
        var assignmentRows = taskIds.Length == 0
            ? new List<TaskAssignments>()
            : await AssignmentsDs.EntitySelectAsync(x => taskIds.Contains(x.TaskId), ct: ct);
        var assignmentsByTask = assignmentRows
            .GroupBy(a => a.TaskId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var listItems = items.Select(task =>
        {
            var taskAssignments = assignmentsByTask.GetValueOrDefault(task.Id) ?? [];
            var completed = taskAssignments.Count(a => a.Status == AssignmentStatus.Completed);
            var completionRate = taskAssignments.Count == 0 ? 0d : Math.Round((double)completed / taskAssignments.Count, 4);

            return new TaskListItemDto
            {
                TaskUid = task.UId,
                Title = task.Title,
                DeadlineAt = task.DeadlineAt,
                Status = task.Status.ToString(),
                QuestionCount = task.QuestionCount,
                CompletionRate = completionRate,
            };
        }).ToList();

        return new ListTasksResDto { Success = true, Items = listItems, Total = (int)totalCount };
    }
}

/// <summary>任务列表请求 DTO</summary>
public sealed record ListTasksReqDto
{
    /// <summary>群组过滤（群主）</summary>
    public string? GroupUid { get; init; }

    /// <summary>状态过滤（Active/Closed）</summary>
    public string? Status { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页数（默认 20）</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>任务列表响应 DTO</summary>
public sealed record ListTasksResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>任务列表</summary>
    public List<TaskListItemDto> Items { get; init; } = [];

    /// <summary>总数</summary>
    public int Total { get; init; }
}

/// <summary>任务列表项 DTO</summary>
public sealed record TaskListItemDto
{
    /// <summary>任务外部键</summary>
    public string TaskUid { get; init; } = string.Empty;

    /// <summary>任务名</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>截止时间</summary>
    public DateTime? DeadlineAt { get; init; }

    /// <summary>状态</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>题目数</summary>
    public int QuestionCount { get; init; }

    /// <summary>完成任务率（已完成/全部分配，含逾期分母）</summary>
    public double CompletionRate { get; init; }
}