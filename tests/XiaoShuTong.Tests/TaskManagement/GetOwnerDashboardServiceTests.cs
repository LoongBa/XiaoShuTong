using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.4 群主执行看板（GetOwnerDashboardService）Contract 测试
/// 覆盖 BR：BR-16 群组校验 | BR-17 执行率分母含全部 | BR-19 薄弱点 Top5 正确率升序 | BR-18 无正确率排名
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetOwnerDashboardServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string groupUid, params long[] studentIds)
    {
        var ds = User.Use<GroupsDataService>();
        var group = await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = ownerId,
            Name = "看板群组",
            Subject = "chinese",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);

        var membersDs = User.Use<GroupMembersDataService>();
        foreach (var studentId in studentIds)
        {
            await membersDs.EntityCreateAsync(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = studentId,
                Role = MemberRole.Student,
                JoinedAt = DateTime.UtcNow,
            }, TestContext.Current.CancellationToken);
        }
        return group;
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

    private async Task SeedMasteryAsync(long userId, string kp, double accuracy)
    {
        var ds = User.Use<KnowledgeMasteryDataService>();
        await ds.EntityCreateAsync(new KnowledgeMastery
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Subject = "chinese",
            KnowledgePoint = kp,
            State = MemoryState.Fuzzy,
            Accuracy = accuracy,
            AttemptCount = 1,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-16：非法群组 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        SetUser(24001);
        var svc = User.Use<GetOwnerDashboardService>();

        var result = await svc.ExecuteAsync(new GetOwnerDashboardReqDto { GroupUid = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-16：非 Owner 群组 → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsForbidden()
    {
        await SeedGroupAsync(999903, "group-dash-24002");
        SetUser(24002);
        var svc = User.Use<GetOwnerDashboardService>();

        var result = await svc.ExecuteAsync(new GetOwnerDashboardReqDto { GroupUid = "group-dash-24002" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-17：执行率 = 已完成/全部分配（5/10 → 0.5，分母含逾期）</summary>
    [Fact]
    public async Task ExecuteAsync_ExecutionRate_DenominatorIncludesAll()
    {
        var ownerId = SetUser(24003);
        var group = await SeedGroupAsync(ownerId, $"group-dash-{24003}", 24011, 24012, 24013, 24014, 24015);
        var task = await SeedTaskAsync(ownerId, group.Id, "执行率任务");
        await SeedAssignmentAsync(task.Id, 24011, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 24012, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 24013, AssignmentStatus.Overdue, 30);  // 分母含逾期
        await SeedAssignmentAsync(task.Id, 24014, AssignmentStatus.Pending, 0);
        await SeedAssignmentAsync(task.Id, 24015, AssignmentStatus.Pending, 0);
        var svc = User.Use<GetOwnerDashboardService>();

        var result = await svc.ExecuteAsync(new GetOwnerDashboardReqDto { GroupUid = group.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0.4, result.TodayExecutionRate); // 2 完成 / 5 全部
        Assert.Equal(1, result.OverdueCount);
        Assert.Equal(46, result.AvgProgress); // (100+100+30+0+0)/5 = 46
    }

    /// <summary>BR-19：薄弱点 Top5 按掌握度正确率升序（最薄弱在前）</summary>
    [Fact]
    public async Task ExecuteAsync_WeakPoints_OrderedByAccuracyAsc()
    {
        var ownerId = SetUser(24004);
        var group = await SeedGroupAsync(ownerId, $"group-dash-{24004}", 24041);
        var task = await SeedTaskAsync(ownerId, group.Id, "薄弱点任务");
        await SeedAssignmentAsync(task.Id, 24041, AssignmentStatus.Completed, 100);
        await SeedMasteryAsync(24041, "知识点A", 0.3);
        await SeedMasteryAsync(24041, "知识点B", 0.9);
        await SeedMasteryAsync(24041, "知识点C", 0.5);
        var svc = User.Use<GetOwnerDashboardService>();

        var result = await svc.ExecuteAsync(new GetOwnerDashboardReqDto { GroupUid = group.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(3, result.WeakPointsTop5.Count);
        Assert.Equal("知识点A", result.WeakPointsTop5[0].KnowledgePoint); // 0.3 最薄弱在前
        Assert.Equal("知识点C", result.WeakPointsTop5[1].KnowledgePoint); // 0.5
        Assert.Equal("知识点B", result.WeakPointsTop5[2].KnowledgePoint); // 0.9
    }

    /// <summary>BR-14/BR-18：无任务 → 空态数据；响应无正确率排名</summary>
    [Fact]
    public async Task ExecuteAsync_NoTasks_ReturnsEmptyDashboard()
    {
        var ownerId = SetUser(24005);
        var group = await SeedGroupAsync(ownerId, $"group-dash-{24005}"); // 无任务
        var svc = User.Use<GetOwnerDashboardService>();

        var result = await svc.ExecuteAsync(new GetOwnerDashboardReqDto { GroupUid = group.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TodayExecutionRate);
        Assert.Equal(0, result.OverdueCount);
        Assert.Empty(result.TaskList);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // 无正确率排名（合规红线）
    }
}