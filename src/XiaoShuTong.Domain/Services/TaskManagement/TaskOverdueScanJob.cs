using TKW.Framework.Domain;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.5：任务逾期扫描（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度：Hangfire 每日 0:00（UTC+8）+ 每次任务发布时触发（切片验证：测试直接调用）。
/// BR-20 无到期任务跳过（幂等）| BR-21 单任务失败不中断整批 | BR-22 到期未完成分配置 Overdue | BR-23 任务保持 Active（AllowRedo 逾期仍可复习）
/// </remarks>
internal class TaskOverdueScanJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    /// <summary>
    /// 扫描到期任务 → 未完成分配置 Overdue
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // BR-20：扫描 DeadlineAt < now 且 Active 的任务，空则跳过
        var overdueTasks = await TasksDs.EntitySelectAsync(
            x => x.Status == TaskStatus.Active && x.DeadlineAt != null && x.DeadlineAt < now, ct: ct);
        if (overdueTasks.Count == 0)
            return;

        foreach (var task in overdueTasks)
        {
            try
            {
                // BR-22：Pending/InProgress 分配置 Overdue（Completed 不变）
                var pending = await AssignmentsDs.EntitySelectAsync(
                    x => x.TaskId == task.Id
                         && (x.Status == AssignmentStatus.Pending || x.Status == AssignmentStatus.InProgress),
                    ct: ct);
                foreach (var assignment in pending)
                {
                    assignment.Status = AssignmentStatus.Overdue;
                    await AssignmentsDs.EntityUpdateAsync(assignment, ct);
                }
                // BR-23：任务保持 Active（历史保留，AllowRedo 任务逾期仍可进入复习会话）
            }
            catch
            {
                // BR-21：单任务失败记录日志（切片吞异常继续），不中断整批
            }
        }
    }
}