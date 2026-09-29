using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.6 任务进度漂移周期巡检（ProgressDriftAuditJob）Contract 测试
/// 覆盖：漂移校正（缺失/多余元素）| 一致不写 | 状态迁移（Pending→InProgress、卡 &lt;100→Completed）| 幂等
/// 对齐 BR-24 判定口径（Correct 或 ≥MaxAttempts），算法副本溯源 SubmitAttemptService.RebuildIfDriftedAsync L487-491
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ProgressDriftAuditJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private async Task<Tasks> SeedTaskAsync(long ownerId, int questionCount, string[] questionIds)
    {
        var ds = User.Use<TasksDataService>();
        return await ds.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            GroupId = 1,
            Title = "漂移巡检测试任务",
            QuestionIds = questionIds,
            QuestionCount = questionCount,
            Scenario = TaskScenario.Memorize,
            SessionType = TaskSessionType.Progressive,
            AllowRedo = false,
            Status = TaskStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<TaskAssignments> SeedAssignmentAsync(long taskId, long userId,
        AssignmentStatus status = AssignmentStatus.InProgress, int progress = 50, string consumed = "")
    {
        var ds = User.Use<TaskAssignmentsDataService>();
        return await ds.EntityCreateAsync(new TaskAssignments
        {
            UId = UidGenerator.NewId(),
            TaskId = taskId,
            UserId = userId,
            Status = status,
            Progress = progress,
            ConsumedQuestionIds = consumed,
            AssignedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<StudySessions> SeedSessionAsync(long userId, long taskId)
    {
        var ds = User.Use<StudySessionsDataService>();
        return await ds.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = LearningScenario.Memorize,
            BankId = "bank-ch-7a",
            SessionType = SessionType.Progressive,
            QuestionCount = 10,
            TaskId = taskId,
            StartedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedAttemptAsync(long userId, long sessionId, string questionId, JudgmentResult result)
    {
        var ds = User.Use<AttemptsDataService>();
        await ds.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = sessionId,
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = MemoryState.NotMastered,
            PostState = result == JudgmentResult.Correct ? MemoryState.Mastered : MemoryState.NotMastered,
            Result = result,
            HintLevel = HintLevel.None,
            AnswerHash = $"{userId}-{sessionId}-{questionId}-{result}",
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：漂移校正——增量集合缺元素（模拟并发/遗留漂移）→ Job 全量重算恢复</summary>
    [Fact]
    public async Task ExecuteAsync_DriftedMissingElement_Rebuilds()
    {
        var task = await SeedTaskAsync(1, questionCount: 3, ["Q-43001a", "Q-43001b", "Q-43001c"]);
        // 增量集合缺 Q-c（漂移：实际已消费 3 题但集合只有 2 个）
        await SeedAssignmentAsync(task.Id, 47001, consumed: "Q-43001a,Q-43001b", progress: 67);
        var session = await SeedSessionAsync(47001, task.Id);
        await SeedAttemptAsync(47001, session.Id, "Q-43001a", JudgmentResult.Correct);
        await SeedAttemptAsync(47001, session.Id, "Q-43001b", JudgmentResult.Correct);
        await SeedAttemptAsync(47001, session.Id, "Q-43001c", JudgmentResult.Correct); // 实际已消费
        var job = User.Use<ProgressDriftAuditJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id && x.UserId == 47001, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Q-43001a,Q-43001b,Q-43001c", updated.ConsumedQuestionIds); // 全量重算恢复
        Assert.Equal(100, updated.Progress);
        Assert.Equal(AssignmentStatus.Completed, updated.Status); // ≥100 → Completed
    }

    /// <summary>一致不写：增量集合正确 → Job 不改变（幂等）</summary>
    [Fact]
    public async Task ExecuteAsync_Consistent_NoChange()
    {
        var task = await SeedTaskAsync(1, questionCount: 2, ["Q-43002a", "Q-43002b"]);
        await SeedAssignmentAsync(task.Id, 47002, consumed: "Q-43002a", progress: 50);
        var session = await SeedSessionAsync(47002, task.Id);
        await SeedAttemptAsync(47002, session.Id, "Q-43002a", JudgmentResult.Correct);
        var job = User.Use<ProgressDriftAuditJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id && x.UserId == 47002, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Q-43002a", updated.ConsumedQuestionIds); // 不变
        Assert.Equal(50, updated.Progress); // 不变
        Assert.Equal(AssignmentStatus.InProgress, updated.Status); // 不变
    }

    /// <summary>状态迁移：Pending 且有作答 → InProgress（对齐 SubmitAttempt L445-446）</summary>
    [Fact]
    public async Task ExecuteAsync_PendingWithAttempts_BecomesInProgress()
    {
        var task = await SeedTaskAsync(1, questionCount: 2, ["Q-43003a", "Q-43003b"]);
        await SeedAssignmentAsync(task.Id, 47003, status: AssignmentStatus.Pending, progress: 0, consumed: "");
        var session = await SeedSessionAsync(47003, task.Id);
        await SeedAttemptAsync(47003, session.Id, "Q-43003a", JudgmentResult.Correct);
        var job = User.Use<ProgressDriftAuditJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id && x.UserId == 47003, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(AssignmentStatus.InProgress, updated.Status); // Pending → InProgress
        Assert.Equal("Q-43003a", updated.ConsumedQuestionIds); // 集合已重算（原空 → 实际 1 题）
        Assert.Equal(50, updated.Progress);
    }

    /// <summary>无作答记录 → 不巡检（Oracle C1：不创造"有作答但未消费"的迁移副作用）</summary>
    [Fact]
    public async Task ExecuteAsync_NoAttempts_NotAudited()
    {
        var task = await SeedTaskAsync(1, questionCount: 2, ["Q-43004a", "Q-43004b"]);
        await SeedAssignmentAsync(task.Id, 47004, status: AssignmentStatus.Pending, progress: 0, consumed: "");
        // 无任务会话 → 无作答记录
        var job = User.Use<ProgressDriftAuditJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id && x.UserId == 47004, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(AssignmentStatus.Pending, updated.Status); // 保持 Pending（未迁移）
        Assert.Equal(0, updated.Progress);
    }

    /// <summary>Overdue/Completed 不巡检（已完成幂等，BR-24 Completed 语义）</summary>
    [Fact]
    public async Task ExecuteAsync_CompletedNotAudited()
    {
        var task = await SeedTaskAsync(1, questionCount: 1, ["Q-43005"]);
        await SeedAssignmentAsync(task.Id, 47005, status: AssignmentStatus.Completed, progress: 100, consumed: "Q-43005");
        // 无任务会话（Completed 分配已幂等）
        var job = User.Use<ProgressDriftAuditJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id && x.UserId == 47005, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(AssignmentStatus.Completed, updated.Status); // 保持 Completed
        Assert.Equal(100, updated.Progress);
        Assert.Equal("Q-43005", updated.ConsumedQuestionIds); // 不变
    }

    /// <summary>损坏数据归一化（Oracle C4）：集合内容一致但序列化含空格 → 归一化写回</summary>
    [Fact]
    public async Task ExecuteAsync_DamagedSerialization_Normalizes()
    {
        var task = await SeedTaskAsync(1, questionCount: 1, ["Q-43006"]);
        // 损坏：序列化含空格（" Q-43006 "），集合内容正确
        await SeedAssignmentAsync(task.Id, 47006, consumed: " Q-43006 ", progress: 100);
        var session = await SeedSessionAsync(47006, task.Id);
        await SeedAttemptAsync(47006, session.Id, "Q-43006", JudgmentResult.Correct);
        var job = User.Use<ProgressDriftAuditJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<TaskAssignmentsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == task.Id && x.UserId == 47006, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Q-43006", updated.ConsumedQuestionIds); // 归一化写回（去除空格）
    }
}
