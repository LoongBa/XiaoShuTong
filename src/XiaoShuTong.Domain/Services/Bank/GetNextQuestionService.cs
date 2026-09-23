using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.5：防爬取题（背一题取一题，Callee 被学习域调用）
/// </summary>
/// <remarks>
/// BR-17 题库存在且有访问权 | BR-18 题集耗尽返回空 | BR-19 响应不含答案与关键词（防爬 DRM 核心）
/// BR-20 出题按状态机排序（到期复习 → 未掌握 → 新题）
/// 学习-BR-04 混合比（30% 新题 + 70% 复习）：按已答复习占比动态池选择（ADR-009 决策一，Oracle 评审闭环）
/// 状态机排序依赖学习域 MemoryStates（切片 02 数据，跨模块读）；会话已答题目排除（Attempts 跨模块读）。
/// </remarks>
internal class GetNextQuestionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    /// <summary>学习-BR-04 混合比：70% 复习 + 30% 新题（ADR-009 决策一常量起步，Config 化后续）</summary>
    private const double ReviewRatio = 0.7;

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    private MemoryStatesDataService? _statesDs;
    private MemoryStatesDataService StatesDs => _statesDs ??= User.Use<MemoryStatesDataService>();

    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    /// <summary>
    /// 按混合比 + 状态机排序取下一题（不含答案/关键词）
    /// </summary>
    public async Task<GetNextQuestionResDto> ExecuteAsync(GetNextQuestionReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-17：题库存在且有访问权
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new GetNextQuestionResDto { Success = false, ErrorCode = BankErrorCodes.BankNotFound };
        if (bank.Privacy == BankPrivacy.Private && bank.OwnerId != userId)
            return new GetNextQuestionResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        // 会话已答题目（跨模块：SessionUid → StudySessions.Id → Attempts）
        List<Attempts> sessionAttempts = [];
        string[]? answeredQuestionIds = null;
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            var session = await SessionsDs.EntityGetAsync(x => x.UId == request.SessionId, ct);
            if (session != null)
            {
                sessionAttempts = await AttemptsDs.EntitySelectAsync(
                    x => x.SessionId == session.Id, ct: ct);
                answeredQuestionIds = sessionAttempts.Select(a => a.QuestionId).ToArray();
            }
        }

        // 题库内可用题目（题型过滤；⚠️ 知识点过滤移内存层——G8：FreeSql SQLite provider 无法翻译
        // string[] .Contains()（PG jsonb @> 无对应，SQL 方言边界，框架文档明示）。生产 PG 同样走
        // 内存过滤（先查全量再 Where）——单题库题量 < 1000，量级合理，语义一致。）
        var questions = await QuestionsDs.EntitySelectAsync(
            x => x.BankId == request.BankId && x.Status == QuestionStatus.Active
                 && (string.IsNullOrWhiteSpace(request.Type) || x.QType.ToString() == request.Type),
            ct: ct);
        if (!string.IsNullOrWhiteSpace(request.KnowledgePoint))
            questions = questions.Where(q => q.KnowledgePoints.Contains(request.KnowledgePoint)).ToList();

        var available = questions
            .Where(q => answeredQuestionIds == null || !answeredQuestionIds.Contains(q.QuestionId))
            .ToList();

        // BR-18：题集耗尽 → 空结果（会话结束信号）
        if (available.Count == 0)
            return new GetNextQuestionResDto { Success = true };

        var now = DateTime.UtcNow;

        // 无会话/直接 Callee 调用（SessionId 空）：不分池，保持原 BR-20 状态机排序（兼容既有行为）
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            var ordered = await OrderByStatePriorityAsync(available, userId, now, ct);
            var next = ordered.First();
            return BuildResponse(next);
        }

        // 学习-BR-04 混合比：分池 + 按已答复习占比动态池选择（无状态，Oracle 评审闭环）
        // states 单次查询覆盖 available ∪ 已答题（供已答复习占比稳定分类）
        var stateMap = await LoadStateMapAsync(available, answeredQuestionIds, userId, ct);

        // 复习题判定集合（状态机：有状态 + 到期 + 非熟练；跨 available ∪ 已答，稳定分类）
        var reviewQuestionIds = stateMap
            .Where(kv => kv.Value.NextReviewAt <= now && kv.Value.State != MemoryState.Proficient)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.Ordinal);

        var reviewPool = available.Where(q => reviewQuestionIds.Contains(q.QuestionId)).ToList();
        var newPool = available.Where(q => !stateMap.ContainsKey(q.QuestionId)).ToList();

        // 两池合计空（available 均为"有状态但未到期/熟练"）→ BR-18 空结果（会话结束信号）
        if (reviewPool.Count == 0 && newPool.Count == 0)
            return new GetNextQuestionResDto { Success = true };

        // 已答复习占比（复用已查 attempts；已答 0 视为 0 → 首题必进复习池）
        var answeredTotal = sessionAttempts.Count;
        var reviewAnswered = sessionAttempts.Count(a => reviewQuestionIds.Contains(a.QuestionId));
        var reviewRatio = answeredTotal == 0 ? 0d : (double)reviewAnswered / answeredTotal;

        var preferReviewPool = reviewRatio < ReviewRatio;
        Questions chosenQuestion;
        if (preferReviewPool)
        {
            chosenQuestion = reviewPool.Count > 0
                ? SortByStatePriority(reviewPool, stateMap, now).First()
                : newPool.OrderBy(q => q.Id).First();   // 目标池空 → 另一池补齐
        }
        else
        {
            chosenQuestion = newPool.Count > 0
                ? newPool.OrderBy(q => q.Id).First()
                : SortByStatePriority(reviewPool, stateMap, now).First();   // 目标池空 → 另一池补齐
        }

        return BuildResponse(chosenQuestion);
    }

    private GetNextQuestionResDto BuildResponse(Questions next)
    {
        var content = StripAnswerFromContent(next.Content, next.QType);
        return new GetNextQuestionResDto
        {
            Success = true,
            QuestionId = next.QuestionId,
            Type = next.QType.ToString(),
            Content = content,
            KnowledgePoint = next.KnowledgePoints.FirstOrDefault(),
            KnowledgeCardId = ExtractCardId(content),
        };
    }

    /// <summary>查询 states（覆盖 available ∪ 已答，供混合比稳定分类；单次查询共享）</summary>
    private async Task<Dictionary<string, MemoryStates>> LoadStateMapAsync(
        List<Questions> questions, string[]? answeredQuestionIds, long userId, CancellationToken ct)
    {
        var bankId = questions.FirstOrDefault()?.BankId ?? string.Empty;
        var ids = questions.Select(q => q.QuestionId)
            .Concat(answeredQuestionIds ?? [])
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, MemoryStates>(StringComparer.Ordinal);

        var states = await StatesDs.EntitySelectAsync(
            x => x.UserId == userId && x.BankId == bankId && ids.Contains(x.QuestionId),
            ct: ct);
        return states.ToDictionary(s => s.QuestionId, s => s);
    }

    private async Task<List<Questions>> OrderByStatePriorityAsync(
        List<Questions> questions, long userId, DateTime now, CancellationToken ct)
    {
        var stateMap = await LoadStateMapAsync(questions, null, userId, ct);
        return SortByStatePriority(questions, stateMap, now);
    }

    private static List<Questions> SortByStatePriority(
        List<Questions> questions, Dictionary<string, MemoryStates> stateMap, DateTime now)
    {
        return questions
            .OrderByDescending(q => stateMap.TryGetValue(q.QuestionId, out var s)
                && s.NextReviewAt <= now && s.State != MemoryState.Proficient)  // 到期复习优先
            .ThenBy(q => stateMap.TryGetValue(q.QuestionId, out var s2) ? StatePriority(s2.State) : 100) // 未掌握→模糊 优先于新题
            .ThenBy(q => q.Id)
            .ToList();
    }

    private static int StatePriority(MemoryState state)
        => state switch
        {
            MemoryState.NotMastered => 0,
            MemoryState.Fuzzy => 1,
            MemoryState.Mastered => 2,
            MemoryState.Proficient => 3,
            _ => 4,
        };

    /// <summary>
    /// 题库-BR-19 防爬：按题型白名单保留展示字段、剔除答案侧字段（answer/keywords/correct_option）
    /// R1/R2/R3a/R3b → question+cardId；R4 → question+answer+cardId（answer=卡片正文展示载荷特例）；
    /// O1/O2/O3 → question+options+cardId；O5/O4 → question；非法 JSON 原样透传（容错）
    /// </summary>
    private static string StripAnswerFromContent(string content, QuestionType qType)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "{}";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return content;  // 非对象 JSON → 原样透传（容错，正常不触发）

            var keep = qType switch
            {
                QuestionType.R4 => new[] { "question", "answer", "cardId" },
                QuestionType.O1 or QuestionType.O2 or QuestionType.O3 => new[] { "question", "options", "cardId" },
                QuestionType.O5 or QuestionType.O4 => new[] { "question" },
                _ => new[] { "question", "cardId" },   // R1/R2/R3a/R3b 及未知题型默认
            };

            var obj = doc.RootElement;
            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var key in keep)
            {
                if (obj.TryGetProperty(key, out var value))
                {
                    if (!first)
                        sb.Append(',');
                    sb.Append(System.Text.Json.JsonSerializer.Serialize(key));
                    sb.Append(':');
                    sb.Append(value.GetRawText());
                    first = false;
                }
            }
            sb.Append('}');
            return sb.ToString();
        }
        catch (System.Text.Json.JsonException)
        {
            return content;  // 非法 JSON → 原样透传（容错）
        }
    }

    /// <summary>从内容 JSON 提取知识卡片 ID（无则 null）</summary>
    private static string? ExtractCardId(string content)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("cardId", out var card) && card.ValueKind == System.Text.Json.JsonValueKind.String
                ? card.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

/// <summary>取下一题请求 DTO</summary>
public sealed record GetNextQuestionReqDto
{
    /// <summary>会话外部键</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>题型过滤</summary>
    public string? Type { get; init; }

    /// <summary>知识点过滤</summary>
    public string? KnowledgePoint { get; init; }
}

/// <summary>取下一题响应 DTO（不含答案与关键词）</summary>
public sealed record GetNextQuestionResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>题型</summary>
    public string? Type { get; init; }

    /// <summary>题目展示内容（不含答案）</summary>
    public string Content { get; init; } = "{}";

    /// <summary>知识点</summary>
    public string? KnowledgePoint { get; init; }

    /// <summary>关联知识卡片</summary>
    public string? KnowledgeCardId { get; init; }
}