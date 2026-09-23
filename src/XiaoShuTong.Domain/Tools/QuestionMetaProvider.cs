using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;

namespace XiaoShuTong.Tools;

/// <summary>题目元数据（真实数据源读取产物：Questions + Banks；仅供 QuestionMetaProvider 与学习域消费点内部使用）</summary>
/// <param name="QuestionId">题目业务键（Questions.QuestionId 直接）</param>
/// <param name="BankId">题库业务键（Questions.BankId，BR-26 校验 q.BankId == session.BankId）</param>
/// <param name="Subject">学科（Banks.Subject，经 BankId join）</param>
/// <param name="KnowledgePoint">知识点（Questions.KnowledgePoints[0]，FirstOrDefault）</param>
/// <param name="QType">题型（Questions.QType.ToString()，判题/Attempts 消费）</param>
/// <param name="KeywordsJson">判题关键词（Questions.Keywords JSON 原文，KeywordGroup[]，不平铺——Weight/Required/Aliases 保留）</param>
/// <param name="Hint">背景钩子提示（Questions.Hint，可空 → 空串不致命）</param>
internal sealed record QuestionMeta(
    string QuestionId,
    string BankId,
    string Subject,
    string KnowledgePoint,
    string QType,
    string KeywordsJson,
    string? Hint);

/// <summary>
/// 题目元数据提供器（只读封装：QuestionsDataService + BanksDataService 真实读取，ADR-008 决策二 A 方案）
/// </summary>
/// <remarks>
/// 替代 LearningQuestionRegistry 静态桩，供 5 消费点（SubmitAttempt / GetHint / GetSessionResult /
/// GetWrongQuestions / KnowledgeMasteryAggregationJob）复用真实题库数据。
/// KeywordsJson **原样传递**（KeywordGroup[] JSON 原文，Weight/Required/Aliases 保留，不平铺重包——
/// 平铺会 Weight 全 1 / Required 全 false / 同义词组拆散，BR-32 required 强制 partial 与 BR-33 组命中失效；
/// JudgingEngineService.ParseKeywordGroups 可直接反序列化）。
/// BankId→Subject 映射**批量预取**（BanksDs.EntitySelectAsync 一次取全量构 Dictionary，避免逐条 join 的 N+1）。
/// </remarks>
internal class QuestionMetaProvider(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    /// <summary>按题目业务键取真实元数据（题目不存在返回 null，BR-26/BR-28 存在性校验用）</summary>
    public async Task<QuestionMeta?> GetAsync(string questionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(questionId))
            return null;

        var question = await QuestionsDs.EntityGetAsync(x => x.QuestionId == questionId, ct);
        if (question == null)
            return null;

        var subjectMap = await LoadSubjectMapAsync(ct);
        return ToMeta(question, subjectMap);
    }

    /// <summary>批量取真实元数据（一次 IN 查询 + BankId→Subject 批量预取，防 N+1 join）</summary>
    public async Task<Dictionary<string, QuestionMeta>> GetManyAsync(IEnumerable<string> questionIds, CancellationToken ct = default)
    {
        var map = new Dictionary<string, QuestionMeta>(StringComparer.Ordinal);
        var ids = questionIds?.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToArray() ?? [];
        if (ids.Length == 0)
            return map;

        var questions = await QuestionsDs.EntitySelectAsync(x => ids.Contains(x.QuestionId), ct: ct);
        var subjectMap = await LoadSubjectMapAsync(ct);
        foreach (var question in questions)
            map[question.QuestionId] = ToMeta(question, subjectMap);
        return map;
    }

    private static QuestionMeta ToMeta(Questions question, Dictionary<string, string> subjectMap)
        => new(
            QuestionId: question.QuestionId,
            BankId: question.BankId,
            Subject: subjectMap.GetValueOrDefault(question.BankId) ?? string.Empty,
            KnowledgePoint: question.KnowledgePoints.FirstOrDefault() ?? string.Empty,
            QType: question.QType.ToString(),
            KeywordsJson: question.Keywords ?? "[]",
            Hint: question.Hint);

    /// <summary>批量预取 BankId→Subject 映射（一次全量查询，避免逐条 join 的 N+1；后台 Job 量大时仍应规避）</summary>
    private async Task<Dictionary<string, string>> LoadSubjectMapAsync(CancellationToken ct)
    {
        var banks = await BanksDs.EntitySelectAsync(x => true, ct: ct);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var bank in banks)
            map[bank.BankId] = bank.Subject.ToString();
        return map;
    }
}
