using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;
using XiaoShuTong.Services.Shared;

namespace XiaoShuTong.Services.Stats;

/// <summary>
/// UC-6.4：查看错题本（只读 + 题目摘要关联）
/// </summary>
/// <remarks>
/// BR-12 空错题本正常返回 | BR-13 参数校验 | BR-14 仅当前学生（RLS）| BR-15 本域只读（Mastered 置位在学习域）
/// 跨表按行富化（WrongQuestions × Questions 题目摘要）——VEntity 设计规格：内存 join（不建视图）。
/// 注：ControllerName 消歧——切片 02 学习域已有 GetWrongQuestionsService（UC-4.7）生成同名控制器。
/// </remarks>
[GenerateController(ControllerName = "StatsWrongQuestions")]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetWrongQuestionsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private WrongQuestionsDataService? _wrongDs;
    private WrongQuestionsDataService WrongDs => _wrongDs ??= User.Use<WrongQuestionsDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 分页查询当前学生错题（Mastered 分组 + 学科过滤 + 题目摘要）
    /// </summary>
    public async Task<StatsGetWrongQuestionsResDto> ExecuteAsync(GetWrongQuestionsReqDto request, CancellationToken ct = default)
    {
        // BR-13：参数校验（page≥1、size 1~100）
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        if (request.PageSize is < 1 or > 100)
            return new StatsGetWrongQuestionsResDto { Success = false, ErrorCode = StatsErrorCodes.ParamInvalid };
        var pageSize = request.PageSize;

        var userId = User.UserInfo?.Id ?? 0;

        // BR-14/BR-15：当前学生 + Mastered 分组 + Subject 过滤（只读）
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

        // 关联题目摘要（题库域，跨模块按行富化）
        var questionIds = items.Select(i => i.QuestionId).ToArray();
        var questionMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var qid in questionIds)
        {
            var question = await QuestionsDs.EntityGetAsync(x => x.QuestionId == qid, ct);
            if (question != null)
                questionMap[qid] = ExtractStem(question.Content);
        }

        // BR-12：空列表正常返回
        return new StatsGetWrongQuestionsResDto
        {
            Success = true,
            Items = items
                .Select(x => x.ToDto() with
                {
                    // DTO 最小化：复用 WrongQuestionsDto + 计算字段（IsComputed=Summary，Service 赋值）
                    Summary = questionMap.GetValueOrDefault(x.QuestionId) ?? string.Empty,
                })
                .ToList(),
            Total = (int)totalCount,
        };
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

/// <summary>错题本查询响应 DTO（Stats 版——重命名消 GraphQL 跨域同名冲突，对齐 ControllerName=StatsWrongQuestions）</summary>
public sealed record StatsGetWrongQuestionsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>错题列表（复用 WrongQuestionsDto + Summary 计算字段）</summary>
    public List<WrongQuestionsDto> Items { get; init; } = [];

    /// <summary>总数</summary>
    public int Total { get; init; }
}