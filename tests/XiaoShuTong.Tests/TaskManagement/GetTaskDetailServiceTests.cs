using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.2 任务详情（GetTaskDetailService）Contract 测试
/// 覆盖 BR：BR-07 任务不存在 → TASK_NOT_FOUND | BR-08 学生非本人任务 → FORBIDDEN | BR-10 成员列表不含正确率排名（合规红线）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetTaskDetailServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Tasks> SeedTaskAsync(long ownerId, string title)
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

    /// <summary>主流程（群主）：返回任务头 + 全组成员执行状态</summary>
    [Fact]
    public async Task Execute_Owner_SeesAllMembers()
    {
        var ownerId = SetUser(49001);
        var task = await SeedTaskAsync(ownerId, "背诵任务49001");
        await SeedAssignmentAsync(task.Id, 49111, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 49112, AssignmentStatus.Pending, 0);
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = task.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Task); // 任务头返回
        Assert.Equal(task.UId, result.Task.UId); // 任务头对应本任务
        Assert.Equal(2, result.Members.Count); // 群主见全组进度
    }

    /// <summary>主流程（学生）：仅见本人分配（RLS）</summary>
    [Fact]
    public async Task Execute_Student_SeesOwnAssignmentOnly()
    {
        var studentId = SetUser(49121);
        var task = await SeedTaskAsync(489910, "他人任务49002");
        await SeedAssignmentAsync(task.Id, studentId, AssignmentStatus.InProgress, 40);
        await SeedAssignmentAsync(task.Id, 49122, AssignmentStatus.Pending, 0); // 其他学生
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = task.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Members); // 仅本人进度
    }

    /// <summary>BR-07：任务不存在 → TASK_NOT_FOUND</summary>
    [Fact]
    public async Task Execute_UnknownTask_ReturnsTaskNotFound()
    {
        SetUser(49003);
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = "no-such-task" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.TaskNotFound, result.ErrorCode);
    }

    /// <summary>BR-08：学生非本人任务（无分配）→ FORBIDDEN</summary>
    [Fact]
    public async Task Execute_StudentWithoutAssignment_ReturnsForbidden()
    {
        var task = await SeedTaskAsync(489911, "他人任务49004");
        await SeedAssignmentAsync(task.Id, 49131, AssignmentStatus.Pending);
        SetUser(49004); // 无该任务分配的学生
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = task.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-10：成员列表不含正确率/排名（合规红线）</summary>
    [Fact]
    public async Task Execute_MemberList_NoAccuracyRanking()
    {
        var ownerId = SetUser(49005);
        var task = await SeedTaskAsync(ownerId, "任务49005");
        await SeedAssignmentAsync(task.Id, 49141, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 49142, AssignmentStatus.Pending, 0);
        var svc = User.Use<GetTaskDetailService>();

        var result = await svc.ExecuteAsync(new GetTaskDetailReqDto { TaskUid = task.UId }, TestContext.Current.CancellationToken);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // 无正确率列
        Assert.DoesNotContain("rank", json, StringComparison.OrdinalIgnoreCase); // 无排名
    }
}
