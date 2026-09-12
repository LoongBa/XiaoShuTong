using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.2 任务列表/详情（ListTasksService / GetTaskDetailService）Contract 测试
/// 覆盖 BR：BR-07 任务不存在 | BR-09 群主仅见自己任务 | BR-10 详情无正确率排名 | BR-17 完成率口径
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListTasksServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string groupUid)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = ownerId,
            Name = "群组",
            Subject = "chinese",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Tasks> SeedTaskAsync(long ownerId, long groupId, string title)
    {
        var ds = User.Use<TasksDataService>();
        return await ds.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            GroupId = groupId,
            Title = title,
            QuestionIds = ["Q-1"],
            QuestionCount = 1,
            Scenario = TaskScenario.Memorize,
            SessionType = TaskSessionType.Progressive,
            AllowRedo = false,
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

    /// <summary>BR-09：群主仅见自己布置的任务</summary>
    [Fact]
    public async Task ListTasks_OwnerIsolation_OnlyOwnTasks()
    {
        var ownerA = SetUser(22001);
        var groupA = await SeedGroupAsync(ownerA, $"group-{22001}");
        await SeedTaskAsync(ownerA, groupA.Id, "A的任务");
        await SeedTaskAsync(999902, groupA.Id, "B的任务"); // 群主 B 布置的任务
        var svc = User.Use<ListTasksService>();

        var result = await svc.ExecuteAsync(new ListTasksReqDto { PageSize = 50 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.All(result.Items, i => Assert.Equal("A的任务", i.Title)); // 仅含 A 的任务
    }

    /// <summary>主流程：完成率 = 已完成/全部分配（含逾期分母）</summary>
    [Fact]
    public async Task ListTasks_CompletionRate_AllAssignmentsDenominator()
    {
        var ownerId = SetUser(22002);
        var group = await SeedGroupAsync(ownerId, $"group-{22002}");
        var task = await SeedTaskAsync(ownerId, group.Id, "任务22002");
        await SeedAssignmentAsync(task.Id, 22011, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 22012, AssignmentStatus.Overdue, 20);
        await SeedAssignmentAsync(task.Id, 22013, AssignmentStatus.Pending, 0);
        var svc = User.Use<ListTasksService>();

        var result = await svc.ExecuteAsync(new ListTasksReqDto { PageSize = 50 }, TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(0.3333, item.CompletionRate); // 1 完成 / 3 全部（含逾期分母）
    }

    /// <summary>BR-07 群组过滤：非法 GroupUid → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ListTasks_UnknownGroup_ReturnsGroupNotFound()
    {
        SetUser(22003);
        var svc = User.Use<ListTasksService>();

        var result = await svc.ExecuteAsync(new ListTasksReqDto { GroupUid = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>主流程 + BR-10：任务详情成员列表不含正确率排名</summary>
    [Fact]
    public async Task GetTaskDetail_MemberList_NoRanking()
    {
        var ownerId = SetUser(22004);
        var group = await SeedGroupAsync(ownerId, $"group-{22004}");
        var task = await SeedTaskAsync(ownerId, group.Id, "任务22004");
        await SeedAssignmentAsync(task.Id, 22041, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 22042, AssignmentStatus.Pending, 0);
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = task.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Members.Count);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // 无正确率列（合规）
    }

    /// <summary>BR-07：任务不存在 → TASK_NOT_FOUND</summary>
    [Fact]
    public async Task GetTaskDetail_UnknownTask_ReturnsTaskNotFound()
    {
        SetUser(22005);
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = "no-such-task" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.TaskNotFound, result.ErrorCode);
    }

    /// <summary>BR-08：学生查看非本人任务 → FORBIDDEN</summary>
    [Fact]
    public async Task GetTaskDetail_NotOwnerNotAssigned_ReturnsForbidden()
    {
        var ownerId = SetUser(22006);
        var group = await SeedGroupAsync(ownerId, $"group-{22006}");
        var task = await SeedTaskAsync(ownerId, group.Id, "任务22006");
        await SeedAssignmentAsync(task.Id, 22061, AssignmentStatus.Pending);
        SetUser(22062); // 另一个学生（无该任务分配）
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = task.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.Forbidden, result.ErrorCode);
    }
}