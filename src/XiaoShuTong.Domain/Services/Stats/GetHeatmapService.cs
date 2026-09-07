using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;

namespace XiaoShuTong.Services.Stats;

/// <summary>
/// UC-6.1：查看记忆热力图
/// </summary>
/// <remarks>
/// BR-01 无数据空数组 | BR-02 start ≤ end | BR-03 仅当前学生（RLS）| BR-04 按日聚合
/// 单实体查询（DailyStats 写入时已按日聚合），Service 层编排（VEntity 设计规格：不建视图）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetHeatmapService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    /// <summary>
    /// 按日期区间返回每日 ★ 数与学习题数
    /// </summary>
    public async Task<GetHeatmapResDto> ExecuteAsync(GetHeatmapReqDto request, CancellationToken ct = default)
    {
        // BR-02：start ≤ end
        if (request.Start > request.End)
            return new GetHeatmapResDto { Success = false, ErrorCode = StatsErrorCodes.ParamInvalid };

        var userId = User.UserInfo?.Id ?? 0;

        // BR-03/BR-04：当前学生 + 日期区间，按日聚合
        var dayStart = DateOnly.FromDateTime(request.Start);
        var dayEnd = DateOnly.FromDateTime(request.End);
        var days = await DailyDs.EntitySelectAsync(
            x => x.UserId == userId && x.StatDate >= dayStart && x.StatDate <= dayEnd,
            orderBy: q => q.OrderBy(x => x.StatDate),
            ct: ct);

        // BR-01：无数据返回空数组
        // DTO 最小化：复用自动生成 DailyStatsDto（字段名 Date→StatDate 契约变更，列名即 StatDate）
        return new GetHeatmapResDto
        {
            Success = true,
            Days = days.Select(d => d.ToDto()).ToList(),
        };
    }
}

/// <summary>热力图请求 DTO</summary>
public sealed record GetHeatmapReqDto
{
    /// <summary>区间起（YYYY-MM-DD）</summary>
    public DateTime Start { get; init; }

    /// <summary>区间止</summary>
    public DateTime End { get; init; }
}

/// <summary>热力图响应 DTO</summary>
public sealed record GetHeatmapResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>每日数据</summary>
    public List<DailyStatsDto> Days { get; init; } = [];
}