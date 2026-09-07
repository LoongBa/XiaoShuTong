using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.4：复习队列查询
/// </summary>
/// <remarks>
/// BR-31 空队列正常返回 | BR-32 队列 = NextReviewAt ≤ Date 且 State ≠ Proficient
/// BR-33 逾期置顶 + △ 优先于 ○ | BR-34 响应不含答案正文 | BR-35 参数校验（date/PageSize 1~100）
/// DS01 ⑥：视图不建实体，Service 层直接按 MemoryStates 条件筛选（内存 DAC 切片验证）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetReviewQueueService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private MemoryStatesDataService? _statesDs;
    private MemoryStatesDataService StatesDs => _statesDs ??= User.Use<MemoryStatesDataService>();

    /// <summary>
    /// 查询到期复习队列（逾期置顶、状态色排序、不含答案）
    /// </summary>
    public async Task<GetReviewQueueResDto> ExecuteAsync(GetReviewQueueReqDto request, CancellationToken ct = default)
    {
        // BR-35：参数校验（PageSize 1~100）
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        if (request.PageSize is < 1 or > 100)
            return new GetReviewQueueResDto { Success = false, ErrorCode = LearningErrorCodes.ParamInvalid };
        var pageSize = request.PageSize;

        var userId = User.UserInfo?.Id ?? 0;

        // BR-32：NextReviewAt ≤ Date 且 State ≠ Proficient（当前用户）
        var due = await StatesDs.EntitySelectAsync(
            x => x.UserId == userId
                 && x.NextReviewAt <= request.Date
                 && x.State != MemoryState.Proficient,
            ct: ct);

        // BR-33：逾期（NextReviewAt < 今日）置顶，状态越差越前，其次按复习时间升序
        var todayStart = request.Date.Date;
        var ordered = due
            .OrderByDescending(x => x.NextReviewAt < todayStart)   // 逾期在前
            .ThenBy(x => StateSeverity(x.State))                   // ✕ → △ → ○ 越差越前
            .ThenBy(x => x.NextReviewAt)
            .ToList();

        var overdueCount = ordered.Count(x => x.NextReviewAt < todayStart);

        // 分页 + 截断（保留内存排序：StateSeverity 为方法调用，FreeSql 无法 SQL 翻译）
        // DTO 最小化：复用自动生成 MemoryStatesDto（BR-34 不含答案正文——MemoryStates 实体本身无答案字段）
        var paged = ordered
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.ToDto())
            .ToList();

        return new GetReviewQueueResDto
        {
            Success = true,
            Items = paged,
            OverdueCount = overdueCount,
        };
    }

    private static int StateSeverity(MemoryState state)
        => state switch
        {
            MemoryState.NotMastered => 0,
            MemoryState.Fuzzy => 1,
            MemoryState.Mastered => 2,
            _ => 3,
        };
}

/// <summary>复习队列查询请求 DTO</summary>
public sealed record GetReviewQueueReqDto
{
    /// <summary>查询日期</summary>
    public DateTime Date { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页条数（1~100，默认 20）</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>复习队列查询响应 DTO</summary>
public sealed record GetReviewQueueResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>到期卡片列表（不含答案）</summary>
    public List<MemoryStatesDto> Items { get; init; } = [];

    /// <summary>逾期数</summary>
    public int OverdueCount { get; init; }
}
