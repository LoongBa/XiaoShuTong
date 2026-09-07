using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.3 学生任务列表（ListMyTasksService）Contract 测试
/// 覆盖 BR：BR-11 无任务空态 | BR-12 已截止红标 | BR-13 仅本人分配（RLS）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListMyTasksServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Tasks> SeedTaskAsync(long ownerId, string title, DateTime? deadline = null)
    {
        var ds = User.Use<TasksDataService>();
        return await ds.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            GroupId = 1,
            Title = title,
            QuestionIds = ["Q-1"],
            QuestionCount = 1,
            Scenario = TaskScenario.Memorize,
            SessionType = TaskSessionType.Progressive,
            AllowRedo = false,
            DeadlineAt = deadline,
            Status = TaskStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedAssignmentAsync(long taskId, long userId, AssignmentStatus status, int progress = 0)
    {
        var ds = User.Use<TaskAssignmentsDataService>();
        await ds.EntityCreateAsync(new TaskAssignments
        {
            UId = UidGenerator.NewId(),
            TaskId = taskId,
            UserId = userId,
            Status = status,
            Progress = progress,
            AssignedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-13：仅返回当前学生分配的任务</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnAssignments()
    {
        var userId = SetUser(23001);
        var myTask = await SeedTaskAsync(999901, "我的任务");
        var otherTask = await SeedTaskAsync(999901, "他人任务");
        await SeedAssignmentAsync(myTask.Id, userId, AssignmentStatus.Pending);
        await SeedAssignmentAsync(otherTask.Id, 99991, AssignmentStatus.Pending); // 他人分配
        var svc = User.Use<ListMyTasksService>();

        var result = await svc.ExecuteAsync(new ListMyTasksReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.Equal("我的任务", item.Title);
    }

    /// <summary>BR-11：无任务正常返回空</summary>
    [Fact]
    public async Task ExecuteAsync_NoAssignments_ReturnsEmpty()
    {
        SetUser(23002);
        var svc = User.Use<ListMyTasksService>();

        var result = await svc.ExecuteAsync(new ListMyTasksReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-12：已截止未完成任务 → Overdue 红标</summary>
    [Fact]
    public async Task ExecuteAsync_DeadlinePassed_ShowsOverdue()
    {
        var userId = SetUser(23003);
        var task = await SeedTaskAsync(999901, "已截止任务", deadline: DateTime.UtcNow.AddHours(-1)); // 已过期
        await SeedAssignmentAsync(task.Id, userId, AssignmentStatus.Pending);
        var svc = User.Use<ListMyTasksService>();

        var result = await svc.ExecuteAsync(new ListMyTasksReqDto(), TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("Overdue", item.Status);
        Assert.Equal("已截止任务", item.Title);
    }

    /// <summary>主流程：进度 + 截止时间 + 状态透传</summary>
    [Fact]
    public async Task ExecuteAsync_ReturnsProgressAndDeadline()
    {
        var userId = SetUser(23004);
        var deadline = DateTime.UtcNow.AddDays(2);
        var task = await SeedTaskAsync(999901, "背诵任务", deadline);
        await SeedAssignmentAsync(task.Id, userId, AssignmentStatus.InProgress, 40);
        var svc = User.Use<ListMyTasksService>();

        var result = await svc.ExecuteAsync(new ListMyTasksReqDto(), TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(40, item.Progress);
        Assert.Equal(deadline, item.DeadlineAt);
        Assert.Equal("InProgress", item.Status);
    }
}