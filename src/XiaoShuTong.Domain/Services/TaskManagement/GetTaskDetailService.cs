using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Entities.TaskManagement.DTOs;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.2：任务详情（群主见全组执行进度；学生仅见自己进度）
/// </summary>
/// <remarks>
/// BR-07 任务不存在 → 5101 | BR-08 非本人任务 → 1004 | BR-10 成员列表不按正确率排名（合规红线）
/// 成员执行状态展示完成状态/进度，不含正确率列。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetTaskDetailService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    /// <summary>
    /// 任务详情（成员执行状列表，不含正确率排名）
    /// </summary>
    public async Task<GetTaskDetailResDto> ExecuteAsync(GetTaskDetailReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-07：任务不存在 → 5101
        var task = await TasksDs.EntityGetAsync(x => x.UId == request.TaskUid, ct);
        if (task == null)
            return new GetTaskDetailResDto { Success = false, ErrorCode = TaskErrorCodes.TaskNotFound };

        var assignments = task.OwnerId == userId
            ? await AssignmentsDs.EntitySelectAsync(x => x.TaskId == task.Id, ct: ct)
            : await AssignmentsDs.EntitySelectAsync(x => x.TaskId == task.Id && x.UserId == userId, ct: ct);

        // BR-08：非本人任务且非 Owner → 1004
        if (task.OwnerId != userId && assignments.Count == 0)
            return new GetTaskDetailResDto { Success = false, ErrorCode = TaskErrorCodes.Forbidden };

        // BR-10：成员执行状态（完成状态/进度，不含正确率——聚合正确率列省略，合规无排名）
        // DTO 最小化：复用自动生成 TaskAssignmentsDto（BR-10 合规不受影响——实体无正确率字段）
        var memberDetails = assignments
            .OrderBy(a => a.UserId)
            .Select(a => a.ToDto())
            .ToList();

        return new GetTaskDetailResDto
        {
            Success = true,
            Task = task.ToDto(),
            Members = memberDetails,
        };
    }
}

/// <summary>任务详情请求 DTO</summary>
public sealed record GetTaskDetailReqDto
{
    /// <summary>任务外部键</summary>
    public string TaskUid { get; init; } = string.Empty;
}

/// <summary>任务详情响应 DTO（外层壳：Success/ErrorCode + 内嵌实体 Dto）</summary>
public sealed record GetTaskDetailResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>任务信息（复用 TasksDto，含任务头/截止/状态等）</summary>
    public TasksDto? Task { get; init; }

    /// <summary>成员执行状态列表（不含正确率排名，合规）</summary>
    public List<TaskAssignmentsDto> Members { get; init; } = [];
}