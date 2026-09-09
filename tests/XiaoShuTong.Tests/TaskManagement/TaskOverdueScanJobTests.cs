using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.5 任务逾期扫描（TaskOverdueScanJob）Contract 测试
/// 覆盖 BR：BR-20 无到期任务跳过（幂等）| BR-22 到期 Pending/InProgress → Overdue，Completed 不变 | BR-23 任务保持 Active
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class TaskOverdueScanJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private async Task<Tasks> SeedTaskAsync(string title, TaskStatus status, DateTime? deadline)
    {
        var ds = User.Use<TasksDataService>();
        return await ds.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = 25001,
            GroupId = 1,
            Title = title,
            QuestionIds = ["Q-1"],
            QuestionCount = 1,
            Scenario = TaskScenario.Memorize,
            SessionType = TaskSessionType.Progressive,
            AllowRedo = true,
            DeadlineAt = deadline,
            Status = status,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedAssignmentAsync(long taskId, long userId, AssignmentStatus status)
    {
        var ds = User.Use<TaskAssignmentsDataService>();
        await ds.EntityCreateAsync(new TaskAssignments
        {
            UId = UidGenerator.NewId(),
            TaskId = taskId,
            UserId = userId,
            Status = status,
            Progress = status == AssignmentStatus.Completed ? 100 : 0,
            AssignedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-20：无到期任务 → 不产生写入（按任务隔离断言，fixture 共享数据）</summary>
    [Fact]
    public async Task ExecuteAsync_NoOverdueTasks_Skips()
    {
        var task = await SeedTaskAsync("未到期任务", TaskStatus.Active, DateTime.UtcNow.AddDays(1));
        var job = User.Use<TaskOverdueScanJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var all = await ds.EntitySelectAsync(x => x.TaskId == task.Id, ct: TestContext.Current.CancellationToken);
        Assert.Empty(all); // 该任务未产生任何分配写入
    }

    /// <summary>BR-22：到期 Pending/InProgress → Overdue；Completed 不变</summary>
    [Fact]
    public async Task ExecuteAsync_OverdueTask_MarksPendingOnly()
    {
        var task = await SeedTaskAsync("已到期任务", TaskStatus.Active, DateTime.UtcNow.AddHours(-1));
        await SeedAssignmentAsync(task.Id, 25011, AssignmentStatus.Pending);
        await SeedAssignmentAsync(task.Id, 25012, AssignmentStatus.InProgress);
        await SeedAssignmentAsync(task.Id, 25013, AssignmentStatus.Completed);
        var job = User.Use<TaskOverdueScanJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var assignments = await ds.EntitySelectAsync(x => x.TaskId == task.Id, ct: TestContext.Current.CancellationToken);
        Assert.Equal(AssignmentStatus.Overdue, assignments.Single(a => a.UserId == 25011).Status);
        Assert.Equal(AssignmentStatus.Overdue, assignments.Single(a => a.UserId == 25012).Status);
        Assert.Equal(AssignmentStatus.Completed, assignments.Single(a => a.UserId == 25013).Status); // 已完成不变
    }

    /// <summary>BR-23：任务保持 Active（AllowRedo 逾期任务仍可复习）</summary>
    [Fact]
    public async Task ExecuteAsync_TaskStaysActive()
    {
        var task = await SeedTaskAsync("到期任务", TaskStatus.Active, DateTime.UtcNow.AddHours(-2));
        await SeedAssignmentAsync(task.Id, 25021, AssignmentStatus.Pending);
        var job = User.Use<TaskOverdueScanJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TasksDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(TaskStatus.Active, updated.Status); // 任务保持 Active（历史保留）
    }

    /// <summary>BR-20/幂等：二次扫描不重复影响（已 Overdue 保持不变）</summary>
    [Fact]
    public async Task ExecuteAsync_Rerun_Idempotent()
    {
        var task = await SeedTaskAsync("到期任务", TaskStatus.Active, DateTime.UtcNow.AddHours(-1));
        await SeedAssignmentAsync(task.Id, 25031, AssignmentStatus.Pending);
        var job = User.Use<TaskOverdueScanJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);
        await job.ExecuteAsync(TestContext.Current.CancellationToken); // 二次扫描

        var ds = User.Use<TaskAssignmentsDataService>();
        var assignment = await ds.EntityGetAsync(
            x => x.TaskId == task.Id && x.UserId == 25031, TestContext.Current.CancellationToken);
        Assert.NotNull(assignment);
        Assert.Equal(AssignmentStatus.Overdue, assignment.Status); // 仍为 Overdue，无副作用
    }
}