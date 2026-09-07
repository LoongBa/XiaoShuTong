using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;
using XiaoShuTong.Services.Shared;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.7：错题本查询（跨域引用统计域路由，本域 Service 提供数据）
/// </summary>
/// <remarks>
/// BR-43 空错题本正常返回 | BR-44 仅当前用户（RLS）| BR-45 Mastered/Subject 过滤可空 | BR-46 参数校验
/// 错题写入与"连续 2 次 → 已掌握"置位发生在 UC-4.2（BR-23）；本 UC 仅列表查询。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetWrongQuestionsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private WrongQuestionsDataService? _wrongDs;
    private WrongQuestionsDataService WrongDs => _wrongDs ??= User.Use<WrongQuestionsDataService>();

    /// <summary>
    /// 分页查询当前用户错题（按已掌握分组 + 学科过滤）
    /// </summary>
    public async Task<GetWrongQuestionsResDto> ExecuteAsync(GetWrongQuestionsReqDto request, CancellationToken ct = default)
    {
        // BR-46：参数校验（page≥1）
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

        // BR-43：空列表正常返回
        return new GetWrongQuestionsResDto
        {
            Success = true,
            Items = items
                .Select(x => x.ToDto() with
                {
                    // DTO 最小化：复用 WrongQuestionsDto + 计算字段（IsComputed=KnowledgePoint，Service 赋值）
                    KnowledgePoint = LearningQuestionRegistry.Get(x.QuestionId)?.KnowledgePoint ?? string.Empty,
                })
                .ToList(),
            TotalCount = (int)totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }
}

/// <summary>错题本查询响应 DTO</summary>
public sealed record GetWrongQuestionsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>错题列表（复用 WrongQuestionsDto + KnowledgePoint 计算字段）</summary>
    public List<WrongQuestionsDto> Items { get; init; } = [];

    /// <summary>总记录数</summary>
    public int TotalCount { get; init; }

    /// <summary>页码</summary>
    public int PageIndex { get; init; }

    /// <summary>每页数</summary>
    public int PageSize { get; init; }
}
