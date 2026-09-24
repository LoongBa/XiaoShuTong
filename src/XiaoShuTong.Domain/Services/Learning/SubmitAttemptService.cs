using System.Security.Cryptography;
using System.Text;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.Judging;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.2：提交作答（判题 + 状态迁移 + 派生同步）★核心
/// </summary>
/// <remarks>
/// CROSS：Attempts + MemoryStates + DailyStats + WrongQuestions 同生共死（事实源单一原则）。
/// BR-06 会话归属 | BR-07 答案格式 | BR-09 判题降级（本地引擎）| BR-10 三分判题
/// BR-11~21 四阶状态机 | BR-22 DailyStats | BR-23 WrongQuestions | BR-25 迁移结果 | BR-26 题目归属 | BR-27 幂等
/// 跨模块（本切片桩）：判题服务（LocalJudgmentEngine）、TaskAssignments（切片 04）。
/// 题目元数据经 QuestionMetaProvider 真实读取（ADR-008 决策二，注册表降级测试专用）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class SubmitAttemptService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int HistoryWindow = 20;

    /// <summary>引导/重试上限：达上限后 showAnswer=true（方案 §5.1，防死循环）</summary>
    private const int MaxAttempts = 2;

    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    private MemoryStatesDataService? _statesDs;
    private MemoryStatesDataService StatesDs => _statesDs ??= User.Use<MemoryStatesDataService>();

    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    private WrongQuestionsDataService? _wrongDs;
    private WrongQuestionsDataService WrongDs => _wrongDs ??= User.Use<WrongQuestionsDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private QuestionMetaProvider? _questionMetaProvider;
    private QuestionMetaProvider QuestionMeta => _questionMetaProvider ??= User.Use<QuestionMetaProvider>();

    /// <summary>
    /// 提交作答：判题 → 状态迁移 → 写入事实源 → 同步派生表
    /// </summary>
    public async Task<SubmitAttemptResDto> ExecuteAsync(SubmitAttemptReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-06：会话必须存在且属于当前用户 → 3001
        var session = await SessionsDs.EntityGetAsync(x => x.UId == request.SessionUid, ct);
        if (session == null || session.UserId != userId)
            return Fail(LearningErrorCodes.SessionNotFound);

        // BR-07：答案文本必须合法（非空）
        if (string.IsNullOrWhiteSpace(request.UserAnswer))
            return Fail(LearningErrorCodes.AnswerFormatInvalid);

        // BR-26：题目必须存在且属于会话题库（真实题库读取，ADR-008 决策二）
        var question = await QuestionMeta.GetAsync(request.QuestionId, ct);
        if (question == null || question.BankId != session.BankId)
            return Fail(LearningErrorCodes.QuestionNotInBank);

        // BR-27：幂等——同会话同题**同答案**已记录则返回已有结果（防网络重发/双击；用户改答案重试放行，idx_attempts_idem 唯一约束兜底）
        var answerHash = ComputeAnswerHash(request.UserAnswer);
        var existingAttempt = await AttemptsDs.EntityGetAsync(
            x => x.SessionId == session.Id && x.QuestionId == request.QuestionId && x.AnswerHash == answerHash, ct);
        if (existingAttempt != null)
            return await BuildFromExistingAsync(existingAttempt, userId, ct);

        // HintLevel 校验
        if (!Enum.TryParse<HintLevel>(request.HintLevel, true, out var hintLevel))
            return Fail(LearningErrorCodes.ParamInvalid);

        // 当前记忆状态（一人一题一行）
        var state = await StatesDs.EntityGetAsync(
            x => x.UserId == userId && x.QuestionId == request.QuestionId, ct);
        var preState = state?.State ?? MemoryState.NotMastered;
        var preCc = state?.ConsecutiveCorrect ?? 0;

        // BR-10：判题——接入 JudgingEngineService（五键契约：本地规则 + PreferLlm 交统一 AI 网关，BR-32~36）
        // 关键词原样传递：Questions.Keywords（KeywordGroup[] JSON 原文，Weight/Required/Aliases 保留，ADR-008 决策二）
        var judging = User.Use<JudgingEngineService>();
        var verdict = await judging.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = request.QuestionId,
            QType = question.QType,
            Keywords = question.KeywordsJson,
            UserAnswer = request.UserAnswer,
            HintLevel = request.HintLevel,
            PreferLlm = true,
        }, ct);
        var result = verdict.Result switch
        {
            "Correct" => JudgmentResult.Correct,
            "Partial" => JudgmentResult.Partial,
            _ => JudgmentResult.Wrong,
        };
        var confidence = verdict.Confidence;
        var isDegraded = verdict.IsDegraded; // 降级标记（方案 §五：isDegraded 透出）

        // 统计本题本会话累计作答次数（幂等检查之后：幂等命中已提前返回，此处为首次/重试路径）
        var prevAttempts = await AttemptsDs.EntitySelectAsync(
            x => x.SessionId == session.Id && x.QuestionId == request.QuestionId,
            ct: ct);
        var attemptCount = prevAttempts.Count + 1; // 含本次

        // BR-16：直接看答案（Full）——不迁移状态、不计入作答记录
        if (hintLevel == HintLevel.Full)
        {
            return new SubmitAttemptResDto
            {
                Success = true,
                Result = result.ToString(),
                Confidence = confidence,
                MatchedKeywords = [], // Full 已看答案：不展示组命中明细（早退路径空数组，方案 §四 T3）
                MissingKeywords = [],
                Hint = TruncateHint(question.Hint, 20),
                PreState = preState.ToString(),
                PostState = preState.ToString(),
                NextReviewAt = state?.NextReviewAt ?? DateTime.UtcNow,
                NeedsGuidance = false,
                AttemptCount = 1,
                MaxAttempts = MaxAttempts,
                ShowAnswer = true, // 已看答案（方案决策表 #7）
                IsDegraded = verdict.IsDegraded,
            };
        }

        // BR-21：Play 场景（isolated）——不影响记忆状态、不入 Attempts
        if (session.Scenario is LearningScenario.PlayPk or LearningScenario.PlayDaily)
        {
            return new SubmitAttemptResDto
            {
                Success = true,
                Result = result.ToString(),
                Confidence = confidence,
                MatchedKeywords = verdict.MatchedKeywords, // 组感知 "alias1/alias2"（判题引擎，Oracle P1）
                MissingKeywords = verdict.MissingKeywords,
                Hint = hintLevel == HintLevel.Partial ? TruncateHint(question.Hint, 20) : string.Empty,
                PreState = preState.ToString(),
                PostState = preState.ToString(),
                NextReviewAt = state?.NextReviewAt ?? DateTime.UtcNow,
                NeedsGuidance = false, // Play 场景不引导（PK 答错直接下一题，方案决策表 #9 附加）
                AttemptCount = 1,
                MaxAttempts = MaxAttempts,
                ShowAnswer = false,
                IsDegraded = verdict.IsDegraded,
            };
        }

        // BR-11~20：状态迁移
        var (postState, postCc) = MemoryStateMachine.Migrate(preState, preCc, result, hintLevel, session.Scenario);

        // BR-17/18/19：复习间隔（新题无历史正确率 → 中性 0.8 → 系数 1.0，保证 BR-18 首次答错 30min）
        var now = DateTime.UtcNow;
        var historyAccuracy = state?.HistoryAccuracy ?? 0.8;
        var nextReviewAt = MemoryStateMachine.CalcNextReviewAt(postState, historyAccuracy, postCc, now);

        // 写入 Attempts（唯一事实源）
        var attempt = await AttemptsDs.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = session.Id,
            QuestionId = request.QuestionId,
            BankId = session.BankId,
            Scenario = session.Scenario,
            QType = question.QType,
            PreState = preState,
            PostState = postState,
            Result = result,
            Confidence = confidence,
            HintLevel = hintLevel,
            TimeCostMs = request.TimeCostMs,
            AnswerHash = answerHash,
            AnsweredAt = now,
        }, ct);

        // 更新/创建 MemoryStates（一人一题一行）
        var newAccuracy = await CalcHistoryAccuracyAsync(userId, request.QuestionId, attempt.Id, ct);
        if (state == null)
        {
            await StatesDs.EntityCreateAsync(new MemoryStates
            {
                UId = UidGenerator.NewId(),
                UserId = userId,
                QuestionId = request.QuestionId,
                BankId = session.BankId,
                State = postState,
                ConsecutiveCorrect = postCc,
                HistoryAccuracy = newAccuracy,
                EaseFactor = 2.5,
                NextReviewAt = nextReviewAt,
                LastAttemptId = attempt.Id,
                LastHintLevel = hintLevel,
            }, ct);
        }
        else
        {
            state.State = postState;
            state.ConsecutiveCorrect = postCc;
            state.HistoryAccuracy = newAccuracy;
            state.NextReviewAt = nextReviewAt;
            state.LastAttemptId = attempt.Id;
            state.LastHintLevel = hintLevel;
            await StatesDs.EntityUpdateAsync(state, ct);
        }

        // BR-22：同步 DailyStats（当日累加）
        await SyncDailyStatsAsync(userId, attempt, postState, preState, ct);

        // BR-23：同步 WrongQuestions（归集/连续 2 次 Mastered）
        await SyncWrongQuestionsAsync(userId, request.QuestionId, question.Subject, result, attempt, ct);

        // BR-24：TaskAssignments.Progress 同步派生（ADR-010：增量集合 + 近完成全量校验；任务域切片 04 落地）
        await SyncTaskProgressAsync(session, userId, request.QuestionId, ct);

        // 决策：引导/兜底（方案 §六 步骤 2 + 决策表 #2-#8）
        var showAnswer = (result is JudgmentResult.Wrong or JudgmentResult.Partial) && attemptCount >= MaxAttempts; // 达上限展示答案（决策表 #4 partial / #8 wrong 均兜底，防引导死循环）
        var needsGuidance = (result is JudgmentResult.Partial or JudgmentResult.Wrong) && !showAnswer;

        // BR-25：返回迁移结果
        return new SubmitAttemptResDto
        {
            Success = true,
            Result = result.ToString(),
            Confidence = confidence,
            MatchedKeywords = verdict.MatchedKeywords, // 组感知 "alias1/alias2"（判题引擎 L72-73，Oracle P1）
            MissingKeywords = verdict.MissingKeywords,
            Hint = (needsGuidance || hintLevel == HintLevel.Partial) ? TruncateHint(question.Hint, 20) : string.Empty, // 引导分支必带线索（决策表 #2/#5，BR-29 ≤20 字）
            PreState = preState.ToString(),
            PostState = postState.ToString(),
            NextReviewAt = nextReviewAt,
            NeedsGuidance = needsGuidance,
            AttemptCount = attemptCount,
            MaxAttempts = MaxAttempts,
            ShowAnswer = showAnswer,
            IsDegraded = isDegraded,
        };
    }

    /// <summary>近 20 次正确率：Correct=1 / Partial=0.5 / Wrong=0 的均值</summary>
    private async Task<double> CalcHistoryAccuracyAsync(long userId, string questionId, long latestAttemptId, CancellationToken ct)
    {
        var recent = await AttemptsDs.EntitySelectAsync(
            x => x.UserId == userId && x.QuestionId == questionId,
            orderBy: q => q.OrderByDescending(x => x.AnsweredAt),
            limit: HistoryWindow,
            ct: ct);

        if (recent.Count == 0)
            return 0d;

        return Math.Round(recent.Average(a => AttemptAccuracy(a.Result)), 4);
    }

    private static double AttemptAccuracy(JudgmentResult result)
        => result switch
        {
            JudgmentResult.Correct => 1d,
            JudgmentResult.Partial => 0.5d,
            _ => 0d,
        };

    /// <summary>BR-22：DailyStats 当日累加（LearnedCount/StarredCount/ReviewCount/Accuracy/StudySeconds）</summary>
    private async Task SyncDailyStatsAsync(long userId, Attempts attempt, MemoryState postState, MemoryState preState, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var bizDate = DateOnly.FromDateTime(now.AddHours(8));
        var dayStartUtc = now.AddHours(8).Date.AddHours(-8);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var daily = await DailyDs.EntityGetAsync(x => x.UserId == userId && x.StatDate == bizDate, ct);
        var todayAttempts = await AttemptsDs.EntitySelectAsync(
            x => x.UserId == userId && x.AnsweredAt >= dayStartUtc && x.AnsweredAt < dayEndUtc,
            ct: ct);

        var learnedCount = todayAttempts.Count;
        var starredCount = todayAttempts.Count(a => a.PostState == MemoryState.Proficient && a.PreState != MemoryState.Proficient);
        var reviewCount = todayAttempts.Count(a => a.PreState != MemoryState.NotMastered);
        var accuracy = learnedCount == 0 ? (double?)null : Math.Round(todayAttempts.Average(a => AttemptAccuracy(a.Result)), 4);
        var studySeconds = todayAttempts.Sum(a => a.TimeCostMs ?? 0) / 1000;

        if (daily == null)
        {
            await DailyDs.EntityCreateAsync(new DailyStats
            {
                UId = UidGenerator.NewId(),
                UserId = userId,
                StatDate = bizDate,
                LearnedCount = learnedCount,
                StarredCount = starredCount,
                ReviewCount = reviewCount,
                Accuracy = accuracy,
                StudySeconds = studySeconds,
            }, ct);
        }
        else
        {
            daily.LearnedCount = learnedCount;
            daily.StarredCount = starredCount;
            daily.ReviewCount = reviewCount;
            daily.Accuracy = accuracy;
            daily.StudySeconds = studySeconds;
            await DailyDs.EntityUpdateAsync(daily, ct);
        }
    }

    /// <summary>BR-23：WrongQuestions 归集（Wrong/Partial 计入；连续 2 次 Correct → Mastered）</summary>
    private async Task SyncWrongQuestionsAsync(
        long userId, string questionId, string subject, JudgmentResult result, Attempts attempt, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var wrong = await WrongDs.EntityGetAsync(
            x => x.UserId == userId && x.QuestionId == questionId, ct);

        if (result is JudgmentResult.Wrong or JudgmentResult.Partial)
        {
            if (wrong == null)
            {
                await WrongDs.EntityCreateAsync(new WrongQuestions
                {
                    UId = UidGenerator.NewId(),
                    UserId = userId,
                    QuestionId = questionId,
                    BankId = attempt.BankId,
                    Subject = subject,
                    WrongCount = 1,
                    LastWrongAt = now,
                    Mastered = false,
                }, ct);
            }
            else
            {
                wrong.WrongCount++;
                wrong.LastWrongAt = now;
                wrong.Mastered = false;
                await WrongDs.EntityUpdateAsync(wrong, ct);
            }
        }
        else if (result == JudgmentResult.Correct && wrong != null)
        {
            // 连续 2 次答对（跨会话）→ Mastered=true
            var recent = await AttemptsDs.EntitySelectAsync(
                x => x.UserId == userId && x.QuestionId == questionId,
                orderBy: q => q.OrderByDescending(x => x.AnsweredAt),
                limit: 2,
                ct: ct);
            wrong.Mastered = recent.Count >= 2 && recent.All(a => a.Result == JudgmentResult.Correct);
            await WrongDs.EntityUpdateAsync(wrong, ct);
        }
    }

    /// <summary>
    /// BR-24：TaskAssignments.Progress 同步派生（ADR-010 决策一~五 + V0.6.1 实现路径增补）
    /// 增量式（非全量重算）：每次作答仅按 QuestionId 查本题跨会话 attempts，判定"新消费"
    /// 后增量入持久化集合 ConsumedQuestionIds；Progress = 集合大小 / Tasks.QuestionCount；
    /// 近完成（集合数 ≥ total-1）触发一次全量重算校验防漂移（Oracle 闭环#4，漂移缺题致
    /// Progress 永不达 100 时仍能触发）。判定口径（Correct 或达 MaxAttempts 计消费）、
    /// 状态迁移、Completed 幂等、个人会话（TaskId=null）不推进——全部不变。
    /// </summary>
    private async Task SyncTaskProgressAsync(StudySessions session, long userId, string questionId, CancellationToken ct)
    {
        // 个人/自由会话（无任务归属）→ 不推进（ADR-010 使用场景 S5）
        if (session.TaskId is null or <= 0)
            return;

        // 任务分配定位（BR-05：TaskId+UserId 唯一）
        var assignment = await AssignmentsDs.EntityGetAsync(
            x => x.TaskId == session.TaskId && x.UserId == userId, ct);
        if (assignment == null)
            return;

        // Completed 幂等：不再变更（AllowRedo 重做不改变，对齐 BR-23）
        if (assignment.Status == AssignmentStatus.Completed)
            return;

        var now = DateTime.UtcNow;

        // 三预留字段接线（Oracle 评审闭环：SessionId 仅作最新会话指针，不参与派生）
        assignment.SessionId = session.Id;
        assignment.StartedAt ??= now;

        // 增量核心（V0.6.1）：解析持久化集合（损坏/空串 → 空集合，Oracle 闭环#6a）
        var consumedSet = ParseConsumedSet(assignment.ConsumedQuestionIds);

        // 跨会话桥接保留（会话数少、代价小；Attempts 无 TaskId，两步查询经 StudySessions）
        var taskSessions = await SessionsDs.EntitySelectAsync(
            x => x.TaskId == session.TaskId && x.UserId == userId, ct: ct);
        var sessionIds = taskSessions.Select(s => s.Id).ToHashSet();
        if (sessionIds.Count == 0)
            sessionIds.Add(session.Id);

        // 只查本题（关键优化：全任务 attempts 扫描 → 单题过滤；含本次已写入 L178-195）
        var questionAttempts = await AttemptsDs.EntitySelectAsync(
            a => a.UserId == userId && a.QuestionId == questionId
                && a.SessionId != null && sessionIds.Contains(a.SessionId.Value),
            ct: ct);

        // 消费判定（口径不变，ADR-010 决策二勘误）：Correct 或该题尝试数 ≥ MaxAttempts
        if (questionAttempts.Any(a => a.Result == JudgmentResult.Correct)
            || questionAttempts.Count >= MaxAttempts)
        {
            consumedSet.Add(questionId);
        }

        // 分母 = 任务题集规模（Tasks.QuestionCount）
        var task = await TasksDs.EntityGetAsync(x => x.Id == session.TaskId.Value, ct);
        var totalCount = task?.QuestionCount ?? consumedSet.Count;
        if (totalCount <= 0)
            return;

        // 近完成校验（Oracle 闭环#4：set.Count >= total-1 即触发，防漂移漏触发）
        if (consumedSet.Count >= totalCount - 1)
            await RebuildIfDriftedAsync(assignment, session, userId, consumedSet, ct);

        // 重算 Progress（百分比制，DOMAIN_MAP L447；集合幂等 Contains 防重复）
        var progress = (int)Math.Round((double)consumedSet.Count / totalCount * 100);
        assignment.Progress = Math.Clamp(progress, 0, 100);
        assignment.ConsumedQuestionIds = string.Join(",", consumedSet.OrderBy(x => x, StringComparer.Ordinal));

        // 状态迁移（ADR-010 决策四）：Pending→InProgress（首次推进）；全部消费→Completed
        if (assignment.Status == AssignmentStatus.Pending)
            assignment.Status = AssignmentStatus.InProgress;
        if (assignment.Progress >= 100)
        {
            assignment.Status = AssignmentStatus.Completed;
            assignment.CompletedAt = now;
        }

        await AssignmentsDs.EntityUpdateAsync(assignment, ct);
    }

    /// <summary>
    /// 解析持久化消费集合（逗号分隔；损坏/空串/未知元素 → 安全降级为空集合，Oracle 闭环#6a）
    /// </summary>
    private static HashSet<string> ParseConsumedSet(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return new HashSet<string>(StringComparer.Ordinal);

        return serialized
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 近完成全量校验（Oracle 闭环#4）：全量重算消费集合与增量集合比对，
    /// 不一致 → 以全量结果重建（防并发/遗留/增量漂移，保留 ADR-010 决策一"抗漂移"）。
    /// 仅近完成触发一次，不构成热路径。
    /// </summary>
    private async Task RebuildIfDriftedAsync(
        TaskAssignments assignment, StudySessions session, long userId,
        HashSet<string> consumedSet, CancellationToken ct)
    {
        var taskSessions = await SessionsDs.EntitySelectAsync(
            x => x.TaskId == session.TaskId && x.UserId == userId, ct: ct);
        var sessionIds = taskSessions.Select(s => s.Id).ToHashSet();
        if (sessionIds.Count == 0)
            sessionIds.Add(session.Id);
        var taskAttempts = await AttemptsDs.EntitySelectAsync(
            a => a.UserId == userId && a.SessionId != null && sessionIds.Contains(a.SessionId.Value), ct: ct);

        var actualConsumed = taskAttempts
            .GroupBy(a => a.QuestionId)
            .Where(g => g.Any(a => a.Result == JudgmentResult.Correct) || g.Count() >= MaxAttempts)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (!actualConsumed.SetEquals(consumedSet))
        {
            consumedSet.Clear();
            consumedSet.UnionWith(actualConsumed);
        }
    }

    /// <summary>BR-27：幂等返回——复用已有作答记录 + 当前记忆状态</summary>
    private async Task<SubmitAttemptResDto> BuildFromExistingAsync(Attempts attempt, long userId, CancellationToken ct)
    {
        var state = await StatesDs.EntityGetAsync(
            x => x.UserId == userId && x.QuestionId == attempt.QuestionId, ct);

        return new SubmitAttemptResDto
        {
            Success = true,
            Result = attempt.Result.ToString(),
            Confidence = attempt.Confidence,
            MatchedKeywords = [],
            MissingKeywords = [],
            Hint = string.Empty,
            PreState = attempt.PreState.ToString(),
            PostState = attempt.PostState.ToString(),
            NextReviewAt = state?.NextReviewAt ?? DateTime.UtcNow,
            NeedsGuidance = false, // 幂等：已有结果不再引导
            AttemptCount = 1,      // 同会话同题唯一（BR-27），已有 1 条
            MaxAttempts = MaxAttempts,
            ShowAnswer = false,
            IsDegraded = false,
        };
    }

    /// <summary>作答内容 SHA-256 哈希（hex，幂等键区分重发 vs 重试）</summary>
    private static string ComputeAnswerHash(string answer)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(answer)));

    /// <summary>提示 ≤20 字（BR-29：严禁直接给答案，提示从知识卡片提取；可空 → 空串）</summary>
    internal static string TruncateHint(string? hint, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return string.Empty;
        return hint.Length <= maxLength ? hint : hint[..maxLength];
    }

    private static SubmitAttemptResDto Fail(string errorCode)
        => new() { Success = false, ErrorCode = errorCode };
}

/// <summary>提交作答请求 DTO</summary>
public sealed record SubmitAttemptReqDto
{
    /// <summary>会话外部键</summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>用户作答文本</summary>
    public string UserAnswer { get; init; } = string.Empty;

    /// <summary>求助档位（None/Partial/Full）</summary>
    public string HintLevel { get; init; } = string.Empty;

    /// <summary>作答耗时（毫秒，服务端可选）</summary>
    public int? TimeCostMs { get; init; }
}

/// <summary>提交作答响应 DTO</summary>
public sealed record SubmitAttemptResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>判题结果（Correct/Partial/Wrong）</summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>判题置信度</summary>
    public double? Confidence { get; init; }

    /// <summary>命中关键词</summary>
    public string[] MatchedKeywords { get; init; } = [];

    /// <summary>缺失关键词</summary>
    public string[] MissingKeywords { get; init; } = [];

    /// <summary>引导线索（hintLevel=Partial 时必给）</summary>
    public string Hint { get; init; } = string.Empty;

    /// <summary>迁移前状态（MemoryState）</summary>
    public string PreState { get; init; } = string.Empty;

    /// <summary>迁移后状态（MemoryState）</summary>
    public string PostState { get; init; } = string.Empty;

    /// <summary>下次复习时间</summary>
    public DateTime NextReviewAt { get; init; }

    /// <summary>是否应提供引导（result=Partial/Wrong 且未达上限）——前端据此决定是否给"再试一次"</summary>
    public bool NeedsGuidance { get; init; }

    /// <summary>本题本会话累计作答次数（含本次）</summary>
    public int AttemptCount { get; init; }

    /// <summary>引导/重试上限（达上限后 showAnswer=true，防死循环）</summary>
    public int MaxAttempts { get; init; } = 2;

    /// <summary>是否应展示标准答案（达上限或已看答案）——前端展示后必进下一题</summary>
    public bool ShowAnswer { get; init; }

    /// <summary>判题是否降级（LLM 失败→本地规则，UI 提示"判题可能不精确"）</summary>
    public bool IsDegraded { get; init; }
}
