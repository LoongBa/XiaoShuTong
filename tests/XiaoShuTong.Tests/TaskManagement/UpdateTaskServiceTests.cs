using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.1 任务编辑（UpdateTaskService）Contract 测试
/// 覆盖 BR：BR-26 任务存在 + 仅 Owner | BR-27 题集 ⊆ 题库 + SetEquals 幂等 | BR-28 题集变更重算 Progress（对齐学习-BR-24 消费口径）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class UpdateTaskServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Tasks> SeedTaskAsync(long ownerId, string[] questionIds, string? bankId = null)
    {
        var ds = User.Use<TasksDataService>();
        return await ds.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            GroupId = 1,
            BankId = bankId,
            Title = "任务编辑测试任务",
            QuestionIds = questionIds,
            QuestionCount = questionIds.Length,
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

    /// <summary>建题库 + 题目，返回题目 ID 列表（对齐 CreateTaskServiceTests 范式）</summary>
    private async Task<List<string>> SeedBankWithQuestionsAsync(string bankId, int count)
    {
        var bankDs = User.Use<BanksDataService>();
        await bankDs.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);

        var qDs = User.Use<QuestionsDataService>();
        var ids = new List<string>();
        for (var i = 1; i <= count; i++)
        {
            var qid = $"Q-{bankId}-{i}";
            await qDs.EntityCreateAsync(new Questions
            {
                UId = UidGenerator.NewId(),
                QuestionId = qid,
                BankId = bankId,
                QType = QuestionType.R1,
                Content = $"{{\"questionId\":\"{qid}\"}}",
                Keywords = "[]",
                KnowledgePoints = [],
                Difficulty = 0,
                Status = QuestionStatus.Active,
            }, TestContext.Current.CancellationToken);
            ids.Add(qid);
        }
        return ids;
    }

    /// <summary>BR-28 主流程：题集变更 → 重算各成员 Progress（已消费 ∩ 新题集 / 新题数），移出题不计入、新增题默认未消费</summary>
    [Fact]
    public async Task ExecuteAsync_QuestionSetChange_RecomputesProgress()
    {
        var ownerId = SetUser(44001);
        var ids = await SeedBankWithQuestionsAsync("upd4401", 7); // Q-upd4401-1..7
        var task = await SeedTaskAsync(ownerId, [ids[0], ids[1], ids[2], ids[3]], bankId: "upd4401"); // 旧集 4 题

        // 成员 A：Q-1 答对（消费）、Q-2 仅答错 1 次（<2 次不消费）
        await SeedAssignmentAsync(task.Id, 47001, consumed: ids[0], progress: 25);
        var sessionA = await SeedSessionAsync(47001, task.Id);
        await SeedAttemptAsync(47001, sessionA.Id, ids[0], JudgmentResult.Correct);
        await SeedAttemptAsync(47001, sessionA.Id, ids[1], JudgmentResult.Wrong);

        // 成员 B：Q-1/Q-2/Q-3 均答对（3 题消费）
        await SeedAssignmentAsync(task.Id, 47002, consumed: $"{ids[0]},{ids[1]},{ids[2]}", progress: 75);
        var sessionB = await SeedSessionAsync(47002, task.Id);
        await SeedAttemptAsync(47002, sessionB.Id, ids[0], JudgmentResult.Correct);
        await SeedAttemptAsync(47002, sessionB.Id, ids[1], JudgmentResult.Correct);
        await SeedAttemptAsync(47002, sessionB.Id, ids[2], JudgmentResult.Correct);

        var svc = User.Use<UpdateTaskService>();
        // 新集 5 题：[Q-1, Q-2, Q-5, Q-6, Q-7]（Q-3/Q-4 移出，Q-5/6/7 新增）
        var newSet = new[] { ids[0], ids[1], ids[4], ids[5], ids[6] };
        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            QuestionIds = newSet,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.QuestionSetChanged);
        Assert.Equal(2, result.RecalculatedAssignments);
        Assert.Equal(5, result.QuestionCount);

        var tasksDs = User.Use<TasksDataService>();
        var updatedTask = await tasksDs.EntityGetAsync(x => x.UId == task.UId, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedTask);
        Assert.Equal(5, updatedTask.QuestionCount); // QuestionCount 同步新题数

        var assignmentsDs = User.Use<TaskAssignmentsDataService>();
        var a = await assignmentsDs.EntityGetAsync(x => x.TaskId == task.Id && x.UserId == 47001, TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        // A：attempts 消费 = {Q-1}（Q-2 错 1 次 <2 不消费）→ ∩ 新集 = {Q-1} → 1/5 = 20
        Assert.Equal(20, a.Progress);
        Assert.Equal(ids[0], a.ConsumedQuestionIds);
        Assert.Equal(AssignmentStatus.InProgress, a.Status); // InProgress 保持

        var b = await assignmentsDs.EntityGetAsync(x => x.TaskId == task.Id && x.UserId == 47002, TestContext.Current.CancellationToken);
        Assert.NotNull(b);
        // B：attempts 消费 = {Q-1,Q-2,Q-3} → ∩ 新集 = {Q-1,Q-2}（Q-3 移出）→ 2/5 = 40
        Assert.Equal(40, b.Progress);
        Assert.Equal($"{ids[0]},{ids[1]}", b.ConsumedQuestionIds);
        Assert.Equal(AssignmentStatus.InProgress, b.Status);
    }

    /// <summary>BR-26 纯更新：标题/说明/截止时间变更不影响进度（非题集变更 = 纯更新）</summary>
    [Fact]
    public async Task ExecuteAsync_TitleOnlyUpdate_ProgressUnchanged()
    {
        var ownerId = SetUser(44002);
        var ids = await SeedBankWithQuestionsAsync("upd4402", 2);
        var task = await SeedTaskAsync(ownerId, [ids[0], ids[1]], bankId: "upd4402");
        await SeedAssignmentAsync(task.Id, 47002, consumed: ids[0], progress: 50);

        var newDeadline = DateTime.UtcNow.AddDays(7);
        var svc = User.Use<UpdateTaskService>();
        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            Title = "新标题",
            Description = "新说明",
            DeadlineAt = newDeadline,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(result.QuestionSetChanged);
        Assert.Equal(0, result.RecalculatedAssignments);

        var tasksDs = User.Use<TasksDataService>();
        var updatedTask = await tasksDs.EntityGetAsync(x => x.UId == task.UId, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedTask);
        Assert.Equal("新标题", updatedTask.Title);
        Assert.Equal("新说明", updatedTask.Description);
        Assert.Equal(newDeadline, updatedTask.DeadlineAt);

        var assignmentsDs = User.Use<TaskAssignmentsDataService>();
        var a = await assignmentsDs.EntityGetAsync(x => x.TaskId == task.Id && x.UserId == 47002, TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(50, a.Progress); // 进度不变
        Assert.Equal(ids[0], a.ConsumedQuestionIds); // 集合不变
        Assert.Equal(AssignmentStatus.InProgress, a.Status);
    }

    /// <summary>BR-26：非 Owner 更新 → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsForbidden()
    {
        var task = await SeedTaskAsync(999901, ["Q-upd4403-1", "Q-upd4403-2"]);
        SetUser(44003); // 非任务 Owner
        var svc = User.Use<UpdateTaskService>();

        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            Title = "越权标题",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-26：任务不存在 → TASK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownTask_ReturnsTaskNotFound()
    {
        SetUser(44004);
        var svc = User.Use<UpdateTaskService>();

        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = "no-such-task",
            Title = "任务",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.TaskNotFound, result.ErrorCode);
    }

    /// <summary>BR-27：题集内容一致（SetEquals，乱序）→ 幂等跳过重算，Progress 不变</summary>
    [Fact]
    public async Task ExecuteAsync_SameSet_NoRecompute()
    {
        var ownerId = SetUser(44005);
        var ids = await SeedBankWithQuestionsAsync("upd4405", 2);
        var task = await SeedTaskAsync(ownerId, [ids[0], ids[1]], bankId: "upd4405");
        await SeedAssignmentAsync(task.Id, 47005, consumed: ids[0], progress: 50);
        var svc = User.Use<UpdateTaskService>();

        // 同集合不同顺序 → SetEquals 相等 → 不重算
        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            QuestionIds = [ids[1], ids[0]],
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(result.QuestionSetChanged);
        Assert.Equal(0, result.RecalculatedAssignments);
        Assert.Equal(2, result.QuestionCount);

        var assignmentsDs = User.Use<TaskAssignmentsDataService>();
        var a = await assignmentsDs.EntityGetAsync(x => x.TaskId == task.Id && x.UserId == 47005, TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(50, a.Progress); // 进度不变（未重算）
        Assert.Equal(ids[0], a.ConsumedQuestionIds); // 集合不变
    }

    /// <summary>BR-27（对齐 BR-04）：新题集含题库外题目 → QUESTION_NOT_IN_BANK</summary>
    [Fact]
    public async Task ExecuteAsync_QuestionNotInBank_ReturnsQuestionNotInBank()
    {
        var ownerId = SetUser(44006);
        var ids = await SeedBankWithQuestionsAsync("upd4406", 2);
        var task = await SeedTaskAsync(ownerId, [ids[0], ids[1]], bankId: "upd4406");
        await SeedAssignmentAsync(task.Id, 47006, consumed: ids[0], progress: 50);
        var svc = User.Use<UpdateTaskService>();

        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            QuestionIds = [ids[0], "Q-EXTERNAL-4406"], // 题库外题目
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.QuestionNotInBank, result.ErrorCode);
    }

    /// <summary>BR-28 状态迁移：题集收缩后已消费全覆盖新集 → ≥100 → Completed + CompletedAt</summary>
    [Fact]
    public async Task ExecuteAsync_SetShrink_CompletesAssignment()
    {
        var ownerId = SetUser(44007);
        var ids = await SeedBankWithQuestionsAsync("upd4407", 2);
        var task = await SeedTaskAsync(ownerId, [ids[0], ids[1]], bankId: "upd4407");
        await SeedAssignmentAsync(task.Id, 47007, consumed: $"{ids[0]},{ids[1]}", progress: 100);
        var session = await SeedSessionAsync(47007, task.Id);
        await SeedAttemptAsync(47007, session.Id, ids[0], JudgmentResult.Correct);
        await SeedAttemptAsync(47007, session.Id, ids[1], JudgmentResult.Correct);
        var svc = User.Use<UpdateTaskService>();

        // 收缩为单题：已消费 ∩ 新集 = {Q-1} → 1/1 = 100 → Completed
        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            QuestionIds = [ids[0]],
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, result.QuestionCount);

        var assignmentsDs = User.Use<TaskAssignmentsDataService>();
        var a = await assignmentsDs.EntityGetAsync(x => x.TaskId == task.Id && x.UserId == 47007, TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(100, a.Progress);
        Assert.Equal(ids[0], a.ConsumedQuestionIds); // 移出题从集合剔除
        Assert.Equal(AssignmentStatus.Completed, a.Status);
        Assert.NotNull(a.CompletedAt);
    }

    /// <summary>BR-28 Completed 幂等：题集扩张不改变已 Completed 分配（BR-24 语义）</summary>
    [Fact]
    public async Task ExecuteAsync_CompletedAssignment_NotRecalculated()
    {
        var ownerId = SetUser(44008);
        var ids = await SeedBankWithQuestionsAsync("upd4408", 3);
        var task = await SeedTaskAsync(ownerId, [ids[0]], bankId: "upd4408");
        await SeedAssignmentAsync(task.Id, 47008, status: AssignmentStatus.Completed, progress: 100, consumed: ids[0]);
        var svc = User.Use<UpdateTaskService>();

        // 扩张：新集 3 题（含 2 个新题）——Completed 分配保持幂等
        var result = await svc.ExecuteAsync(new UpdateTaskReqDto
        {
            TaskUid = task.UId,
            QuestionIds = [ids[0], ids[1], ids[2]],
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.QuestionSetChanged);
        Assert.Equal(0, result.RecalculatedAssignments); // Completed 分配被跳过
        Assert.Equal(3, result.QuestionCount);

        var assignmentsDs = User.Use<TaskAssignmentsDataService>();
        var a = await assignmentsDs.EntityGetAsync(x => x.TaskId == task.Id && x.UserId == 47008, TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(AssignmentStatus.Completed, a.Status); // 幂等不变
        Assert.Equal(100, a.Progress);
        Assert.Equal(ids[0], a.ConsumedQuestionIds); // 集合不变
    }
}
