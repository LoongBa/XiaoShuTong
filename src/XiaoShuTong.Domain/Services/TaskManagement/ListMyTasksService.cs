using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.3：学生任务列表（我的任务）[Proposed-待D03补充]
/// </summary>
/// <remarks>
/// BR-11 无任务正常返回空态 | BR-12 已截止任务红标（不可进入新会话，会话入口在学习域 Block）| BR-13 仅本人分配（RLS）
/// 群主名（OwnerName）依赖账户域，切片返回空串。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListMyTasksService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    /// <summary>
    /// 当前学生的任务卡列表（仅本人分配）
    /// </summary>
    public async Task<ListMyTasksResDto> ExecuteAsync(ListMyTasksReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-13：仅返回当前学生分配的任务
        var assignments = await AssignmentsDs.EntitySelectAsync(
            x => x.UserId == userId
                 && (string.IsNullOrWhiteSpace(request.Status) || x.Status.ToString() == request.Status),
            ct: ct);

        var items = new List<MyTaskItemDto>();
        foreach (var assignment in assignments.OrderByDescending(a => a.AssignedAt))
        {
            var task = await TasksDs.EntityGetAsync(x => x.Id == assignment.TaskId, ct);
            if (task == null)
                continue;

            // BR-12：已截止（DeadlineAt 已过且未完成）→ 状态显示为 Overdue 红标
            var effectiveStatus = assignment.Status == AssignmentStatus.Pending
                && task.DeadlineAt is { } deadline && deadline < DateTime.UtcNow
                ? AssignmentStatus.Overdue
                : assignment.Status;

            items.Add(new MyTaskItemDto
            {
                TaskUid = task.UId,
                Title = task.Title,
                Progress = assignment.Progress,
                DeadlineAt = task.DeadlineAt,
                Status = effectiveStatus.ToString(),
                OwnerName = string.Empty, // 依赖账户域（跨模块），切片返回空
            });
        }

        return new ListMyTasksResDto { Success = true, Items = items };
    }
}

/// <summary>我的任务列表请求 DTO</summary>
public sealed record ListMyTasksReqDto
{
    /// <summary>状态过滤（Pending/InProgress/Completed/Overdue）</summary>
    public string? Status { get; init; }
}

/// <summary>我的任务列表响应 DTO</summary>
public sealed record ListMyTasksResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>任务卡列表</summary>
    public List<MyTaskItemDto> Items { get; init; } = [];
}

/// <summary>我的任务项 DTO</summary>
public sealed record MyTaskItemDto
{
    /// <summary>任务外部键</summary>
    public string TaskUid { get; init; } = string.Empty;

    /// <summary>任务名</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>完成度</summary>
    public int Progress { get; init; }

    /// <summary>截止时间</summary>
    public DateTime? DeadlineAt { get; init; }

    /// <summary>状态（Pending/InProgress/Completed/Overdue）</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>群主名（账户域依赖，切片为空）</summary>
    public string OwnerName { get; init; } = string.Empty;
}