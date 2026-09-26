using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;
using XiaoShuTong.Services.Shared;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.7：错题本查询（统计域路由，本域 Service 提供数据）
/// </summary>
/// <remarks>
/// BR-43 空错题本正常返回 | BR-44 仅当前用户（RLS）| BR-45 Mastered/Subject 过滤可空 | BR-46 参数校验
/// 富化：KnowledgePoint（真实题库读取，QuestionMetaProvider）+ Summary（题库域题目摘要，批处理防 N+1）。
/// 注：2026-09-09 合并 Stats 版 GetWrongQuestionsService（原 UC-6.4 重复实现，StatsWrongQuestions 控制器删除），
/// DTO 对齐学习域 U01 分页决策 {items,total}。
/// 错题写入与"连续 2 次 → 已掌握"置位发生在 UC-4.2（BR-23）；本 UC 仅列表查询。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetWrongQuestionsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private WrongQuestionsDataService? _wrongDs;
    private WrongQuestionsDataService WrongDs => _wrongDs ??= User.Use<WrongQuestionsDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionMetaProvider? _questionMetaProvider;
    private QuestionMetaProvider QuestionMeta => _questionMetaProvider ??= User.Use<QuestionMetaProvider>();

    /// <summary>
    /// 分页查询当前用户错题（Mastered 分组 + 学科过滤 + 富化知识点/题目摘要）
    /// </summary>
    public async Task<GetWrongQuestionsResDto> ExecuteAsync(GetWrongQuestionsReqDto request, CancellationToken ct = default)
    {
        // BR-46：参数校验（page≥1、size 1~100）
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        if (request.PageSize is < 1 or > 100)
            return new GetWrongQuestionsResDto { Success = false, ErrorCode = LearningErrorCodes.ParamInvalid };
        var pageSize = request.PageSize;

        var userId = User.UserInfo?.Id ?? 0;

        // BR-44/45：UserId + Mastered 分组 + Subject 可空过滤
        System.Linq.Expressions.Expression<Func<WrongQuestions, bool>> predicate = x =>
            x.UserId == userId
            && x.Mastered == request.Mastered
            && (string.IsNullOrWhiteSpace(request.Subject) || x.Subject == request.Subject);

        var items = await WrongDs.EntitySelectAsync(
            predicate,
            (pageIndex - 1) * pageSize,
            pageSize,
            q => q.OrderByDescending(x => x.LastWrongAt),
            ct);
        var totalCount = await WrongDs.CountAsync(predicate, ct);

        // 富化：题目摘要（题库域，批处理一次取回防 N+1）+ 知识点（真实题库读取，与摘要同源，ADR-008 决策二）
        var stemMap = await LoadStemMapAsync(items.Select(i => i.QuestionId).Distinct().ToArray(), ct);
        var metaMap = await QuestionMeta.GetManyAsync(items.Select(i => i.QuestionId).Distinct(), ct);
        var answerMap = await LoadAnswerMapAsync(items.Select(i => i.QuestionId).Distinct().ToArray(), ct);

        // BR-43：空列表正常返回
        return new GetWrongQuestionsResDto
        {
            Success = true,
            Items = items
                .Select(x => x.ToDto() with
                {
                    // DTO 最小化：复用 WrongQuestionsDto + 计算字段（Service 赋值）
                    KnowledgePoint = metaMap.GetValueOrDefault(x.QuestionId)?.KnowledgePoint ?? string.Empty,
                    Summary = stemMap.GetValueOrDefault(x.QuestionId) ?? string.Empty,
                    // 答案：内容权威源优先，Keywords 重组兜底（联调种子无内容文件）
                    Answer = answerMap.GetValueOrDefault(x.QuestionId) ?? string.Empty,
                })
                .ToList(),
            Total = (int)totalCount,
        };
    }

    /// <summary>批量取标准答案（内容权威文件优先 → Questions.Keywords 重组兜底，防 N+1）</summary>
    private async Task<Dictionary<string, string>> LoadAnswerMapAsync(string[] questionIds, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (questionIds.Length == 0) return map;

        // 批取题目（含 Keywords 兜底数据）
        var questions = await QuestionsDs.EntitySelectAsync(
            x => questionIds.Contains(x.QuestionId), ct: ct);
        if (questions.Count == 0) return map;

        // 内容权威源：按 BankId 分组一次取 Banks（JsonPath → ContentFileStore）
        var bankIds = questions.Select(q => q.BankId).Distinct().ToArray();
        var banks = bankIds.Length == 0
            ? new List<Banks>()
            : await BanksDs.EntitySelectAsync(x => bankIds.Contains(x.BankId), ct: ct);
        var bankByBankId = banks.ToDictionary(b => b.BankId);

        // 内容文件反查：QuestionId → Answer（权威）
        var authoritative = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var bank in banks)
        {
            var content = ContentFileStore.Read(bank.JsonPath);
            if (content == null) continue;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("questions", out var questionsNode) &&
                    questionsNode.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var qNode in questionsNode.EnumerateArray())
                    {
                        var qid = qNode.TryGetProperty("questionId", out var qidNode)
                            ? qidNode.GetString() : null;
                        var answer = qNode.TryGetProperty("answer", out var ansNode)
                            ? ansNode.GetString() : null;
                        if (qid != null && answer != null)
                            authoritative[qid] = answer;
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // 内容文件损坏：跳过权威源，走 Keywords 兜底
            }
        }

        foreach (var question in questions)
        {
            // 权威源优先
            if (authoritative.TryGetValue(question.QuestionId, out var ans) && !string.IsNullOrWhiteSpace(ans))
            {
                map[question.QuestionId] = ans;
                continue;
            }
            // 兜底：Keywords（KeywordGroup[]）重组——Aliases[0] 主词空格连接
            map[question.QuestionId] = RebuildAnswerFromKeywords(question.Keywords);
        }
        return map;
    }

    /// <summary>Keywords（KeywordGroup[] JSON）重组答案：各分组主词（Aliases[0]）空格连接</summary>
    private static string RebuildAnswerFromKeywords(string keywordsJson)
    {
        if (string.IsNullOrWhiteSpace(keywordsJson)) return string.Empty;
        try
        {
            var groups = System.Text.Json.JsonSerializer.Deserialize<List<KeywordGroup>>(keywordsJson);
            if (groups == null || groups.Count == 0) return string.Empty;
            return string.Join(" ", groups
                .Select(g => g.Aliases.Length > 0 ? g.Aliases[0] : null)
                .Where(a => !string.IsNullOrWhiteSpace(a)));
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>批量取题目题干摘要（一次 IN 查询，防 N+1）</summary>
    private async Task<Dictionary<string, string>> LoadStemMapAsync(string[] questionIds, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (questionIds.Length == 0) return map;

        var questions = await QuestionsDs.EntitySelectAsync(
            x => questionIds.Contains(x.QuestionId), ct: ct);
        foreach (var question in questions)
            map[question.QuestionId] = ExtractStem(question.Content);
        return map;
    }

    /// <summary>从题目 Content 镜像提取题干摘要（不含答案）</summary>
    private static string ExtractStem(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("stem", out var stem))
                return stem.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
        }
        return string.Empty;
    }
}

/// <summary>错题本查询响应 DTO（Items + Total，对齐学习域 U01 分页决策 {items,total}）</summary>
public sealed record GetWrongQuestionsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>错题列表（复用 WrongQuestionsDto + KnowledgePoint/Summary 计算字段）</summary>
    public List<WrongQuestionsDto> Items { get; init; } = [];

    /// <summary>总数</summary>
    public int Total { get; init; }
}