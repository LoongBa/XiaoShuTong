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
/// 状态机排序依赖学习域 MemoryStates（切片 02 数据，跨模块读）；会话已答题目排除（Attempts 跨模块读）。
/// </remarks>
internal class GetNextQuestionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
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
    /// 按状态机排序取下一题（不含答案/关键词）
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
        string[]? answeredQuestionIds = null;
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            var session = await SessionsDs.EntityGetAsync(x => x.UId == request.SessionId, ct);
            if (session != null)
            {
                var attempts = await AttemptsDs.EntitySelectAsync(
                    x => x.SessionId == session.Id, ct: ct);
                answeredQuestionIds = attempts.Select(a => a.QuestionId).ToArray();
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

        // BR-20：状态机排序——到期复习（NextReviewAt ≤ now 且非熟练）→ 未掌握/模糊 → 新题（无状态）
        var now = DateTime.UtcNow;
        var ordered = await OrderByStatePriorityAsync(available, userId, now, ct);
        var next = ordered.First();

        var content = JsonSafeParseContent(next.Content);
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

    private async Task<List<Questions>> OrderByStatePriorityAsync(
        List<Questions> questions, long userId, DateTime now, CancellationToken ct)
    {
        var bankId = questions.FirstOrDefault()?.BankId ?? string.Empty;
        var states = await StatesDs.EntitySelectAsync(
            x => x.UserId == userId && x.BankId == bankId
                 && questions.Select(q => q.QuestionId).Contains(x.QuestionId),
            ct: ct);
        var stateMap = states.ToDictionary(s => s.QuestionId, s => s);

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

    /// <summary>Content 内容 JSON（不含答案）</summary>
    private static string JsonSafeParseContent(string content)
        => string.IsNullOrWhiteSpace(content) ? "{}" : content;

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