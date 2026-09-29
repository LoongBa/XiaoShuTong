using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.6：任务进度漂移周期巡检（BackgroundJob，无对外接口；V0.6.1 审核 §七-2 落地）
/// </summary>
/// <remarks>
/// 调度：Hangfire 每日定时巡检（切片验证：测试直接调用）。
/// 背景：TaskAssignments.Progress 为增量维护（BR-24，SubmitAttemptService.SyncTaskProgressAsync）——
/// 近完成（集合数 ≥ total-1）才触发全量重算校验（RebuildIfDriftedAsync）。漂移极端：consumedSet 含缺失/多余
/// 元素（并发/遗留/增量 bug）→ Progress 卡 &lt;100 且用户不再作答时永不触发校验 → "卡 &lt;100 永不触发"。
/// 本 Job 巡检兜底：扫描 Pending/InProgress 分配 → 全量重算消费集合 vs 增量集合比对 → 不一致校正。
/// 幂等：一致不写（无副作用）；单分配失败不中断整批；Completed/Overdue 不巡检（已完成幂等）。
/// </remarks>
internal class ProgressDriftAuditJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    /// <summary>消费判定阈值：该题尝试数 ≥ MaxAttempts 计消费（Oracle C3：对齐 SubmitAttemptService L35）</summary>
    private const int MaxAttempts = 2;

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    /// <summary>
    /// 扫描未完成任务分配 → 全量重算消费集合比对校正（杜绝"卡 &lt;100 永不触发"极端）
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // 一次查全部未完成任务分配（Pending/InProgress；Completed 幂等 + Overdue 语义自洽不巡检）
        var assignments = await AssignmentsDs.EntitySelectAsync(
            x => x.Status == AssignmentStatus.Pending || x.Status == AssignmentStatus.InProgress, ct: ct);
        if (assignments.Count == 0)
            return;

        foreach (var assignment in assignments)
        {
            try
            {
                await AuditOneAsync(assignment, ct);
            }
            catch
            {
                // 单分配失败继续（对齐 TaskOverdueScanJob BR-21 模式）
            }
        }
    }

    private async Task AuditOneAsync(TaskAssignments assignment, CancellationToken ct)
    {
        // 1. 该分配关联的任务会话
        var taskSessions = await SessionsDs.EntitySelectAsync(
            x => x.TaskId == assignment.TaskId && x.UserId == assignment.UserId, ct: ct);
        var sessionIds = taskSessions.Select(s => s.Id).ToHashSet();
        if (sessionIds.Count == 0)
            return; // 无会话 → 无作答记录，不巡检

        // 2. 全量重算消费集合（对齐 BR-24 判定口径：Correct 或 ≥MaxAttempts）
        //    Oracle C3：算法副本溯源——对齐 SubmitAttemptService.RebuildIfDriftedAsync L487-491；
        //    判定口径权威：Business.md 学习-BR-24；MaxAttempts=2（SubmitAttemptService L35）
        var taskAttempts = await AttemptsDs.EntitySelectAsync(
            a => a.UserId == assignment.UserId && a.SessionId != null && sessionIds.Contains(a.SessionId.Value), ct: ct);
        // Oracle C1：无作答记录不巡检（Job 是巡检非主路径，不应创造"有作答但未消费"的迁移副作用）
        if (taskAttempts.Count == 0)
            return;

        var actualConsumed = taskAttempts
            .GroupBy(a => a.QuestionId)
            .Where(g => g.Any(a => a.Result == JudgmentResult.Correct) || g.Count() >= MaxAttempts)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        // 3. 与增量集合比对 → 不一致则校正（对齐 RebuildIfDriftedAsync SetEquals 语义）
        var persisted = ParseConsumedSet(assignment.ConsumedQuestionIds);
        if (actualConsumed.SetEquals(persisted))
        {
            // Oracle C4：SetEquals 一致但序列化不一致（重复/空格/乱序损坏）→ 仍归一化写回修复损坏数据
            var normalized = string.Join(",", actualConsumed.OrderBy(x => x, StringComparer.Ordinal));
            if (persisted.Count == actualConsumed.Count && assignment.ConsumedQuestionIds == normalized)
                return; // 完全一致 → 不写（幂等）
            // 落入此处：内容一致但序列化/归一化不一致 → 写回归一化集合（修复损坏）
            assignment.ConsumedQuestionIds = normalized;
        }
        else
        {
            assignment.ConsumedQuestionIds = string.Join(",", actualConsumed.OrderBy(x => x, StringComparer.Ordinal));
        }

        var task = await TasksDs.EntityGetAsync(x => x.Id == assignment.TaskId, ct);
        var totalCount = task?.QuestionCount ?? actualConsumed.Count;
        if (totalCount <= 0)
            return;

        // 重算 Progress（百分比制，对齐 BR-24 / DOMAIN_MAP L447）
        assignment.Progress = Math.Clamp((int)Math.Round((double)actualConsumed.Count / totalCount * 100), 0, 100);
        // Oracle C1：状态迁移对齐 SubmitAttempt L445-446（无 Count>0 条件——taskAttempts.Count>0 已在上方保证有作答记录）
        if (assignment.Status == AssignmentStatus.Pending)
            assignment.Status = AssignmentStatus.InProgress;
        if (assignment.Progress >= 100)
        {
            assignment.Status = AssignmentStatus.Completed;
            assignment.CompletedAt = DateTime.UtcNow;
        }

        await AssignmentsDs.EntityUpdateAsync(assignment, ct);
    }

    /// <summary>解析持久化消费集合（对齐 SubmitAttemptService.ParseConsumedSet L459-468：损坏/空串 → 空集合）</summary>
    private static HashSet<string> ParseConsumedSet(string serialized)
        => string.IsNullOrWhiteSpace(serialized)
            ? new HashSet<string>(StringComparer.Ordinal)
            : serialized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
}
