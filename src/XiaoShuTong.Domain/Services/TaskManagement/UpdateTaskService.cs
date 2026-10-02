using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.1：任务编辑（标题/说明/截止时间/题集变更）
/// </summary>
/// <remarks>
/// BR-26 任务存在（5101）+ 仅 Owner 可编辑 | BR-27 题集变更须 ⊆ 关联题库 + SetEquals 幂等跳过重算
/// BR-28 题集变更后重算非 Completed 分配 Progress（消费判定对齐 学习-BR-24：Correct 或尝试数 ≥ MaxAttempts；
/// 数据源与 ProgressDriftAuditJob 一致——Attempts 经 StudySessions.TaskId 桥接）；标题/说明/截止为纯更新不触进度。
/// BankId 不可变更；QuestionCount 同步为新题集长度。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class UpdateTaskService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    /// <summary>消费判定阈值：该题尝试数 ≥ MaxAttempts 计消费（对齐 SubmitAttemptService L35 / ProgressDriftAuditJob L24，学习-BR-24）</summary>
    private const int MaxAttempts = 2;

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 更新任务标题/说明/截止时间/题集；题集变更时重算全部非 Completed 成员 Progress
    /// </summary>
    public async Task<UpdateTaskResDto> ExecuteAsync(UpdateTaskReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-26：任务必须存在 → 5101（对齐 BR-07）
        var task = await TasksDs.EntityGetAsync(x => x.UId == request.TaskUid, ct);
        if (task == null)
            return new UpdateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.TaskNotFound, TaskUid = request.TaskUid };

        // BR-26：仅任务 Owner（群主）可编辑 → FORBIDDEN（对齐 BR-01）
        if (task.OwnerId != ownerId)
            return new UpdateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.Forbidden, TaskUid = request.TaskUid };

        var recalcCount = 0;
        var questionSetChanged = false;

        // 题集变更（BR-27）：仅显式传入 QuestionIds 且内容变更（SetEquals）时触发校验 + 进度重算；内容一致 → 幂等跳过重算
        if (request.QuestionIds != null)
        {
            var newIds = request.QuestionIds;
            var newSet = newIds.ToHashSet(StringComparer.Ordinal);
            questionSetChanged = !newSet.SetEquals(task.QuestionIds);

            if (questionSetChanged)
            {
                // BR-27（对齐 BR-04）：新题集须 ⊆ 任务关联题库（BankId 为空 = 自由编排无题库校验，同 CreateTask）
                if (!string.IsNullOrWhiteSpace(task.BankId))
                {
                    var bankQuestionIds = (await QuestionsDs.EntitySelectAsync(
                        x => x.BankId == task.BankId, ct: ct))
                        .Select(q => q.QuestionId)
                        .ToHashSet(StringComparer.Ordinal);
                    if (newIds.Any(q => !bankQuestionIds.Contains(q)))
                        return new UpdateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.QuestionNotInBank, TaskUid = request.TaskUid };
                }

                // BR-28：重算全部非 Completed 分配 Progress（消费口径对齐 学习-BR-24 / ProgressDriftAuditJob）
                recalcCount = await RecalcProgressAsync(task, newIds, ct);

                // QuestionCount 同步更新（分母 = 新题集长度）
                task.QuestionIds = newIds;
                task.QuestionCount = newIds.Length;
            }
            else
            {
                // 题集内容一致（SetEquals）→ 同步题集/题数，不重算（幂等）
                task.QuestionIds = newIds;
                task.QuestionCount = newIds.Length;
            }
        }

        // 纯更新：标题/说明/截止时间（null = 不修改；Description 空串 = 清空）——不影响进度
        if (request.Title != null)
            task.Title = request.Title;
        if (request.Description != null)
            task.Description = request.Description;
        if (request.DeadlineAt != null)
            task.DeadlineAt = request.DeadlineAt;

        await TasksDs.EntityUpdateAsync(task, ct);

        return new UpdateTaskResDto
        {
            Success = true,
            TaskUid = task.UId,
            QuestionSetChanged = questionSetChanged,
            RecalculatedAssignments = recalcCount,
            QuestionCount = task.QuestionCount,
        };
    }

    /// <summary>
    /// BR-28：题集变更后重算进度——非 Completed 分配以（已消费题集 ∩ 新题集）/ 新题数 × 100 重算。
    /// 消费判定与数据源完全对齐 ProgressDriftAuditJob/SubmitAttemptService.RebuildIfDriftedAsync：
    /// 经 StudySessions.TaskId 桥接取该成员任务会话 → Attempts 按 (UserId, SessionId∈) 全量聚合 → Correct 或尝试数 ≥ MaxAttempts 计消费。
    /// </summary>
    private async Task<int> RecalcProgressAsync(Tasks task, string[] newQuestionIds, CancellationToken ct)
    {
        var newSet = newQuestionIds.ToHashSet(StringComparer.Ordinal);
        var totalCount = newQuestionIds.Length;
        if (totalCount <= 0)
            return 0;

        // 一次查该任务全部分配（含 Overdue；Completed 幂等跳过）
        var assignments = await AssignmentsDs.EntitySelectAsync(x => x.TaskId == task.Id, ct: ct);
        var now = DateTime.UtcNow;
        var recalcCount = 0;

        foreach (var assignment in assignments)
        {
            // BR-24：Completed 幂等（题集变更不改变已完成分配；AllowRedo 重做亦不改变）
            if (assignment.Status == AssignmentStatus.Completed)
                continue;

            // 数据源对齐 ProgressDriftAuditJob：该成员的任务会话 → SessionId 集合
            var taskSessions = await SessionsDs.EntitySelectAsync(
                x => x.TaskId == task.Id && x.UserId == assignment.UserId, ct: ct);
            var sessionIds = taskSessions.Select(s => s.Id).ToHashSet();

            var consumed = new HashSet<string>(StringComparer.Ordinal);
            if (sessionIds.Count > 0)
            {
                var taskAttempts = await AttemptsDs.EntitySelectAsync(
                    a => a.UserId == assignment.UserId && a.SessionId != null && sessionIds.Contains(a.SessionId.Value), ct: ct);

                // 消费判定（对齐学习-BR-24：Correct 或该题尝试数 ≥ MaxAttempts）
                var actualConsumed = taskAttempts
                    .GroupBy(a => a.QuestionId)
                    .Where(g => g.Any(a => a.Result == JudgmentResult.Correct) || g.Count() >= MaxAttempts)
                    .Select(g => g.Key);
                consumed.UnionWith(actualConsumed);
            }

            // 已消费题集 ∩ 新题集（移出题不计入；新增题默认未消费）
            consumed.IntersectWith(newSet);

            var progress = Math.Clamp((int)Math.Round((double)consumed.Count / totalCount * 100), 0, 100);
            assignment.Progress = progress;
            assignment.ConsumedQuestionIds = string.Join(",", consumed.OrderBy(x => x, StringComparer.Ordinal));

            // 状态迁移（对齐 ProgressDriftAuditJob / 学习-BR-24）：Pending→InProgress（Progress>0）；≥100 → Completed（Pending/InProgress）
            if (assignment.Status == AssignmentStatus.Pending && progress > 0)
                assignment.Status = AssignmentStatus.InProgress;
            if (progress >= 100 && assignment.Status is AssignmentStatus.Pending or AssignmentStatus.InProgress)
            {
                assignment.Status = AssignmentStatus.Completed;
                assignment.CompletedAt = now;
            }

            await AssignmentsDs.EntityUpdateAsync(assignment, ct);
            recalcCount++;
        }

        return recalcCount;
    }
}

/// <summary>任务编辑请求 DTO（null 字段 = 不修改）</summary>
public sealed record UpdateTaskReqDto
{
    /// <summary>任务外部键</summary>
    public string TaskUid { get; init; } = string.Empty;

    /// <summary>新标题（null = 不修改）</summary>
    public string? Title { get; init; }

    /// <summary>新说明（null = 不修改；空串 = 清空）</summary>
    public string? Description { get; init; }

    /// <summary>新截止时间（null = 不修改）</summary>
    public DateTime? DeadlineAt { get; init; }

    /// <summary>新题集（null = 不修改；非空数组 = 整体替换）</summary>
    public string[]? QuestionIds { get; init; }
}

/// <summary>任务编辑响应 DTO</summary>
public sealed record UpdateTaskResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>任务外部键</summary>
    public string TaskUid { get; init; } = string.Empty;

    /// <summary>本次是否发生题集变更（触发进度重算）</summary>
    public bool QuestionSetChanged { get; init; }

    /// <summary>进度重算的分配数（题集变更时）</summary>
    public int RecalculatedAssignments { get; init; }

    /// <summary>更新后题数</summary>
    public int QuestionCount { get; init; }
}
