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
/// 富化：KnowledgePoint（题库注册表）+ Summary（题库域题目摘要，跨域按行富化，批处理防 N+1）。
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

        // 富化：题目摘要（题库域，批处理一次取回防 N+1）+ 知识点（注册表）
        var stemMap = await LoadStemMapAsync(items.Select(i => i.QuestionId).Distinct().ToArray(), ct);

        // BR-43：空列表正常返回
        return new GetWrongQuestionsResDto
        {
            Success = true,
            Items = items
                .Select(x => x.ToDto() with
                {
                    // DTO 最小化：复用 WrongQuestionsDto + 计算字段（Service 赋值）
                    KnowledgePoint = LearningQuestionRegistry.Get(x.QuestionId)?.KnowledgePoint ?? string.Empty,
                    Summary = stemMap.GetValueOrDefault(x.QuestionId) ?? string.Empty,
                })
                .ToList(),
            Total = (int)totalCount,
        };
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