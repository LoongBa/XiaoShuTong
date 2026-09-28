using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.9：薄弱知识点
/// </summary>
/// <remarks>
/// BR-25 未订阅 → 8001 | BR-26 数据未聚合时按需触发（KnowledgeMasteryAggregationJob 跨模块）| BR-27 State 映射家长端文案
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetWeaknessReportService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 薄弱知识点矩阵（掌握度正确率升序 + 状态映射）
    /// </summary>
    public async Task<GetWeaknessReportResDto> ExecuteAsync(GetWeaknessReportReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-25：订阅门控（无预览）
        var gate = await ParentReportGate.CheckAsync(RelationsDs, SubscriptionsDs, parentId, request.StudentId, allowPreview: false, ct);
        if (!gate.Allowed)
            return new GetWeaknessReportResDto { Success = false, ErrorCode = gate.ErrorCode };

        // BR-26：数据未聚合时按需触发掌握度聚合（跨模块 Job）
        var rows = await MasteryDs.EntitySelectAsync(
            x => x.UserId == request.StudentId, ct: ct);
        if (rows.Count == 0)
        {
            var job = User.Use<KnowledgeMasteryAggregationJob>();
            await job.ExecuteAsync(ct);
            rows = await MasteryDs.EntitySelectAsync(
                x => x.UserId == request.StudentId, ct: ct);
            if (rows.Count == 0)
                return new GetWeaknessReportResDto { Success = false, ErrorCode = ParentErrorCodes.NoStatsData };
        }

        // C3 下钻：按 (Subject,KnowledgePoint) 反查章节/关联题目（Subject → Banks → Questions → 内存 kp 过滤）
        var drillMap = await LoadDrillMapAsync(rows, ct);

        // BR-27：State 映射家长端文案（0=✕未掌握/1=△模糊/2=○掌握/3=★熟练）
        var weakPoints = rows
            .OrderBy(r => r.Accuracy)
            .Select(r =>
            {
                drillMap.TryGetValue((r.Subject.ToLowerInvariant(), r.KnowledgePoint), out var drill);
                return new ParentWeakPointDto
                {
                    Subject = r.Subject,
                    KnowledgePoint = r.KnowledgePoint,
                    Accuracy = r.Accuracy,
                    StateText = MapStateText(r.State),
                    ChapterId = drill.ChapterId,
                    QuestionIds = drill.QuestionIds ?? [],
                };
            })
            .ToList();

        return new GetWeaknessReportResDto { Success = true, WeakPoints = weakPoints };
    }

    /// <summary>
    /// C3 下钻：按薄弱点 (Subject,KnowledgePoint) 反查章节与关联题目
    /// 收窄链路：Subject → Banks(Subject) → Questions(BankId) → 内存 KnowledgePoints.Contains(kp)
    /// （G8：string[] 列严禁写入表达式树——SQLite 无法翻译，先取全量再内存过滤）
    /// 批量处理防 N+1：一次取相关 Banks（Subject IN）+ 一次取相关 Questions（BankId IN）。
    /// </summary>
    private async Task<Dictionary<(string Subject, string KnowledgePoint), (string? ChapterId, List<string> QuestionIds)>> LoadDrillMapAsync(
        List<KnowledgeMastery> rows, CancellationToken ct)
    {
        var map = new Dictionary<(string Subject, string KnowledgePoint), (string? ChapterId, List<string> QuestionIds)>();
        if (rows.Count == 0) return map;

        // 该学生薄弱知识点集合（RLS 基准：仅本学生 mastery 行内的 (subject,kp) 才可富化）
        var validPoints = rows
            .Select(r => (Subject: r.Subject.ToLowerInvariant(), Point: r.KnowledgePoint))
            .ToHashSet();

        // 收窄 1→2：Subject（字符串）→ Subject 枚举 → Banks（一次 IN）
        var subjectEnums = rows
            .Select(r => r.Subject)
            .Distinct()
            .Select(s => Enum.TryParse<Subject>(s, ignoreCase: true, out var parsed) ? (Subject?)parsed : null)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToArray();
        var banks = subjectEnums.Length == 0
            ? new List<Banks>()
            : await BanksDs.EntitySelectAsync(x => subjectEnums.Contains(x.Subject), ct: ct);
        var bankByBankId = banks.ToDictionary(b => b.BankId);

        // 收窄 3：BankId IN → Questions（一次 IN）
        var bankIds = banks.Select(b => b.BankId).Distinct().ToArray();
        var questions = bankIds.Length == 0
            ? new List<Questions>()
            : await QuestionsDs.EntitySelectAsync(x => bankIds.Contains(x.BankId), ct: ct);

        // 收窄 4：内存层 KnowledgePoints.Contains(kp)（严禁入表达式树）+ 聚合 ChapterId/QuestionIds
        // 按 QuestionId 排序保证"首个非空 ChapterId"语义确定（多题不同章节时取最小编号题目的章节）
        foreach (var question in questions.OrderBy(q => q.QuestionId))
        {
            var bank = bankByBankId.GetValueOrDefault(question.BankId);
            if (bank == null) continue;
            var subjectKey = bank.Subject.ToString().ToLowerInvariant();
            foreach (var kp in question.KnowledgePoints)
            {
                var key = (subjectKey, kp);
                if (!validPoints.Contains(key)) continue; // 非本学生薄弱知识点 → 不混入（RLS）
                if (!map.TryGetValue(key, out var acc))
                    acc = (ChapterId: null, QuestionIds: new List<string>());
                // 多题不同章节：取首个非空
                if (acc.ChapterId is null && !string.IsNullOrWhiteSpace(question.ChapterId))
                    acc.ChapterId = question.ChapterId;
                if (!acc.QuestionIds.Contains(question.QuestionId))
                    acc.QuestionIds.Add(question.QuestionId);
                map[key] = acc;
            }
        }

        return map;
    }

    private static string MapStateText(MemoryState state)
        => state switch
        {
            MemoryState.NotMastered => "未掌握",
            MemoryState.Fuzzy => "模糊",
            MemoryState.Mastered => "掌握",
            MemoryState.Proficient => "熟练",
            _ => "未掌握",
        };
}

/// <summary>薄弱知识点请求 DTO</summary>
public sealed record GetWeaknessReportReqDto
{
    /// <summary>孩子 Id</summary>
    public long StudentId { get; init; }
}

/// <summary>薄弱知识点响应 DTO</summary>
public sealed record GetWeaknessReportResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>薄弱知识点列表（正确率升序）</summary>
    public List<ParentWeakPointDto> WeakPoints { get; init; } = [];
}

/// <summary>薄弱点 DTO（Parent 版——重命名消 GraphQL 跨域同名冲突；含学科/状态文案，与共享版形状不同）</summary>
public sealed record ParentWeakPointDto
{
    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>知识点</summary>
    public string KnowledgePoint { get; init; } = string.Empty;

    /// <summary>聚合正确率</summary>
    public double Accuracy { get; init; }

    /// <summary>状态家长端文案（未掌握/模糊/掌握/熟练）</summary>
    public string StateText { get; init; } = string.Empty;

    /// <summary>章节/单元（该知识点关联题目的 Questions.ChapterId，多题不同章节取首个非空；无关联题 null）</summary>
    public string? ChapterId { get; init; }

    /// <summary>该知识点下关联题目 QuestionId 列表（无关联题空列表）</summary>
    public List<string> QuestionIds { get; init; } = [];
}