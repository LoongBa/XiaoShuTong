using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.5：记忆状态查询
/// </summary>
/// <remarks>
/// BR-36 仅当前用户（RLS）| BR-37 过滤条件（bankId/state）可空 | BR-38 分页参数校验（page≥1、size 1~100）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetMemoryStatesService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private MemoryStatesDataService? _statesDs;
    private MemoryStatesDataService StatesDs => _statesDs ??= User.Use<MemoryStatesDataService>();

    /// <summary>
    /// 分页查询当前用户题目记忆状态（按题库/状态过滤）
    /// </summary>
    public async Task<GetMemoryStatesResDto> ExecuteAsync(GetMemoryStatesReqDto request, CancellationToken ct = default)
    {
        // BR-38：分页参数校验
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        if (request.PageSize is < 1 or > 100)
            return new GetMemoryStatesResDto { Success = false, ErrorCode = LearningErrorCodes.ParamInvalid };
        var pageSize = request.PageSize;

        var userId = User.UserInfo?.Id ?? 0;

        // BR-36/37：按 UserId + 可选过滤（bankId/state）分页
        System.Linq.Expressions.Expression<Func<MemoryStates, bool>> predicate = x =>
            x.UserId == userId
            && (string.IsNullOrWhiteSpace(request.BankId) || x.BankId == request.BankId)
            && (!request.State.HasValue || x.State == request.State.Value);

        // 无投影 SelectAsync：自动用 MemoryStatesDto.SelectExpression 做 SQL 级投影
        var items = await StatesDs.SelectAsync(
            predicate,
            (pageIndex - 1) * pageSize,
            pageSize,
            q => q.OrderByDescending(x => x.NextReviewAt),
            ct);
        var totalCount = await StatesDs.CountAsync(predicate, ct);

        return new GetMemoryStatesResDto
        {
            Success = true,
            Items = items,
            TotalCount = (int)totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }
}

/// <summary>记忆状态查询请求 DTO</summary>
public sealed record GetMemoryStatesReqDto
{
    /// <summary>题库过滤</summary>
    public string? BankId { get; init; }

    /// <summary>状态过滤</summary>
    public MemoryState? State { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页数（1~100，默认 20）</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>记忆状态查询响应 DTO</summary>
public sealed record GetMemoryStatesResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>状态列表</summary>
    public List<MemoryStatesDto> Items { get; init; } = [];

    /// <summary>总记录数</summary>
    public int TotalCount { get; init; }

    /// <summary>页码</summary>
    public int PageIndex { get; init; }

    /// <summary>每页数</summary>
    public int PageSize { get; init; }
}
