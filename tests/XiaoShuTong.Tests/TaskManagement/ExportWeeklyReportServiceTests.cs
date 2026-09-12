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
/// F9 群组执行周报导出（ExportWeeklyReportService）Contract 测试
/// 覆盖 BR：任务-BR-24 周报口径（执行率分母含 Overdue/平均进度/学习量）| 任务-BR-25 CSV 合规（不含正确率/答题明细）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ExportWeeklyReportServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    // 固定测试周（2026-09-07 为周一），窗口 = [2026-09-07, 2026-09-13]
    private static readonly DateOnly FixedWeekStart = new(2026, 9, 7);
    private static readonly DateTime FixedInWindowStart = new(2026, 9, 7, 9, 0, 0);

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
            Name = "周报群组",
            Subject = "chinese",
            Grade = "七年级",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);

        foreach (var studentId in studentIds)
            await SeedMemberAsync(group.Id, studentId, MemberRole.Student);
        return group;
    }

    private async Task SeedMemberAsync(long groupId, long userId, MemberRole role, string? nickname = null)
    {
        var ds = User.Use<GroupMembersDataService>();
        await ds.EntityCreateAsync(new GroupMembers
        {
            UId = UidGenerator.NewId(),
            GroupId = groupId,
            UserId = userId,
            Role = role,
            Nickname = nickname ?? $"学生{userId}",
            JoinedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Tasks> SeedTaskAsync(long ownerId, long groupId, string title, DateTime startedAt)
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
            StartedAt = startedAt,
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

    private async Task SeedDailyStatAsync(long userId, DateOnly date, int learned = 1)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            StatDate = date,
            LearnedCount = learned,
            StarredCount = 0,
            Accuracy = 0.8,
            StudySeconds = 300,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>任务-BR-24 主流程：2 任务 + 4 分配（2 Completed/1 InProgress/1 Overdue）→ 全局聚合正确</summary>
    [Fact]
    public async Task ExecuteAsync_OwnerGroup_ReturnsAggregates()
    {
        var ownerId = SetUser(51501);
        var group = await SeedGroupAsync(ownerId, "grp-weekly-51501", 51511, 51512);
        var taskA = await SeedTaskAsync(ownerId, group.Id, "周任务A", FixedInWindowStart);
        var taskB = await SeedTaskAsync(ownerId, group.Id, "周任务B", FixedInWindowStart);
        await SeedAssignmentAsync(taskA.Id, 51511, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(taskA.Id, 51512, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(taskB.Id, 51511, AssignmentStatus.InProgress, 60);
        await SeedAssignmentAsync(taskB.Id, 51512, AssignmentStatus.Overdue, 30); // 分母含逾期（同 BR-17）
        await SeedDailyStatAsync(51511, new DateOnly(2026, 9, 8), learned: 2);
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = group.UId, WeekStart = FixedWeekStart }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(FixedWeekStart, result.WeekStart);
        Assert.Equal(FixedWeekStart.AddDays(6), result.WeekEnd);
        Assert.Equal(2, result.TaskCount);
        Assert.Equal(2, result.MemberCount);
        Assert.Equal(4, result.TotalAssignments);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(0.5, result.ExecutionRate); // 2/4
        Assert.Equal(73, result.AvgProgress);    // (100+100+60+30)/4 = 72.5 → 73
        Assert.Equal(2, result.LearnedCount);    // 周内 DailyStats.LearnedCount 求和
        Assert.Equal(2, result.MemberReports.Count);
        Assert.NotNull(result.CsvFileUrl);
        Assert.Contains("weekly-report", result.CsvFileUrl);
    }

    /// <summary>任务-BR-24：无任务/无分配 → 全零值（非错误，前端空态）</summary>
    [Fact]
    public async Task ExecuteAsync_NoAssignments_ReturnsZero()
    {
        var ownerId = SetUser(51502);
        var group = await SeedGroupAsync(ownerId, "grp-weekly-51502", 51521);
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = group.UId, WeekStart = FixedWeekStart }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TaskCount);
        Assert.Equal(1, result.MemberCount);
        Assert.Equal(0, result.TotalAssignments);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(0, result.ExecutionRate);
        Assert.Equal(0, result.AvgProgress);
        Assert.Equal(0, result.LearnedCount);
    }

    /// <summary>任务-BR-16（沿用）：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        SetUser(51503);
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = "no-such-group" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>任务-BR-16（沿用）：他人群组 → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsForbidden()
    {
        await SeedGroupAsync(999903, "grp-weekly-51504");
        SetUser(51504);
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = "grp-weekly-51504" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>任务-BR-24：不传 WeekStart → 默认本周一（UTC+8）；窗口内计入、窗口外不计入</summary>
    [Fact]
    public async Task ExecuteAsync_DefaultWeekStart_UsesMonday()
    {
        var ownerId = SetUser(51505);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // 与 Service 同口径 GetMonday
        var group = await SeedGroupAsync(ownerId, "grp-weekly-51505", 51551);
        var taskIn = await SeedTaskAsync(ownerId, group.Id, "窗口内任务", monday.ToDateTime(TimeOnly.MinValue).AddHours(9));
        await SeedTaskAsync(ownerId, group.Id, "窗口外任务", monday.AddDays(14).ToDateTime(TimeOnly.MinValue).AddHours(9));
        await SeedAssignmentAsync(taskIn.Id, 51551, AssignmentStatus.Completed, 100);
        await SeedDailyStatAsync(51551, monday, learned: 3);          // 窗口内
        await SeedDailyStatAsync(51551, monday.AddDays(14), learned: 9); // 窗口外
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = group.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(monday, result.WeekStart);
        Assert.Equal(monday.AddDays(6), result.WeekEnd);
        Assert.Equal(1, result.TaskCount);           // 窗口外任务不计入
        Assert.Equal(1, result.TotalAssignments);    // 仅窗口内任务分配
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1.0, result.ExecutionRate);
        Assert.Equal(3, result.LearnedCount);        // 窗口外 DailyStats 不计入
    }

    /// <summary>任务-BR-25 合规红线：响应序列化不含 accuracy/correct（无正确率/答题明细）</summary>
    [Fact]
    public async Task ExecuteAsync_Compliance_NoAccuracyField()
    {
        var ownerId = SetUser(51506);
        var group = await SeedGroupAsync(ownerId, "grp-weekly-51506", 51561);
        var task = await SeedTaskAsync(ownerId, group.Id, "合规任务", FixedInWindowStart);
        await SeedAssignmentAsync(task.Id, 51561, AssignmentStatus.Completed, 100);
        await SeedDailyStatAsync(51561, new DateOnly(2026, 9, 8), learned: 2);
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = group.UId, WeekStart = FixedWeekStart }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.CsvFileUrl);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // 无正确率字段
        Assert.DoesNotContain("correct", json, StringComparison.OrdinalIgnoreCase);  // 无答题明细/正确
    }

    /// <summary>任务-BR-24/25：成员级报告 — 每 Student 成员一行（Parent 角色不计入）</summary>
    [Fact]
    public async Task ExecuteAsync_MemberLevelReports_Counted()
    {
        var ownerId = SetUser(51507);
        var group = await SeedGroupAsync(ownerId, "grp-weekly-51507", 51571, 51572);
        await SeedMemberAsync(group.Id, 51573, MemberRole.Parent, "家长"); // 非 Student 不计入
        var task = await SeedTaskAsync(ownerId, group.Id, "成员级任务", FixedInWindowStart);
        await SeedAssignmentAsync(task.Id, 51571, AssignmentStatus.Completed, 100);
        await SeedAssignmentAsync(task.Id, 51572, AssignmentStatus.InProgress, 40);
        await SeedDailyStatAsync(51571, new DateOnly(2026, 9, 8), learned: 5);
        var svc = User.Use<ExportWeeklyReportService>();

        var result = await svc.ExecuteAsync(new ExportWeeklyReportReqDto { GroupUid = group.UId, WeekStart = FixedWeekStart }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.MemberCount);
        Assert.Equal(2, result.MemberReports.Count); // 仅 Student 成员
        Assert.Contains(result.MemberReports, r => r.UserId == 51571 && r.ExecutionRate == 1.0 && r.AvgProgress == 100 && r.LearnedCount == 5);
        Assert.Contains(result.MemberReports, r => r.UserId == 51572 && r.ExecutionRate == 0.0 && r.AvgProgress == 40 && r.LearnedCount == 0);
        Assert.DoesNotContain(result.MemberReports, r => r.UserId == 51573);
    }
}
