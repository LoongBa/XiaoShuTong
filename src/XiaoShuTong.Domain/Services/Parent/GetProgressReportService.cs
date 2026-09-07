using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.8：进度趋势
/// </summary>
/// <remarks>
/// BR-20 未订阅 → 8001 | BR-21 Period ∈ Week/Month | BR-22 无数据 → 4001 | BR-23 合规无排名 | BR-24 vsLastWeek（仅与自己比）
/// 缺口决策：vsLastWeek 仅 learnedDelta + weaknessShift 两字段（D03 定义）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetProgressReportService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    /// <summary>
    /// 进度趋势（周期数据点 + vsLastWeek 相对进步）
    /// </summary>
    public async Task<GetProgressReportResDto> ExecuteAsync(GetProgressReportReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-20：订阅门控（progress 无预览，未订阅 → 8001）
        var gate = await ParentReportGate.CheckAsync(RelationsDs, SubscriptionsDs, parentId, request.StudentId, allowPreview: false, ct);
        if (!gate.Allowed)
            return new GetProgressReportResDto { Success = false, ErrorCode = gate.ErrorCode };

        // BR-21：Period 合法（Week/Month）
        if (!Enum.TryParse<ProgressPeriod>(request.Period, true, out var period))
            return new GetProgressReportResDto { Success = false, ErrorCode = ParentErrorCodes.ParamInvalid };

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var start = period == ProgressPeriod.Week ? today.AddDays(-6) : today.AddDays(-29);

        // 取数窗口：周期起点 往前多取 7 天（支撑 vsLastWeek 本周 vs 上周对比，BR-24）
        var fetchStart = start.AddDays(-7);
        var dailyStats = await DailyDs.EntitySelectAsync(
            x => x.UserId == request.StudentId && x.StatDate >= fetchStart && x.StatDate <= today, ct: ct);

        var periodStats = dailyStats.Where(d => d.StatDate >= start && d.StatDate <= today).ToList();

        // BR-22：无周期数据 → 4001
        if (periodStats.Count == 0)
            return new GetProgressReportResDto { Success = false, ErrorCode = ParentErrorCodes.NoStatsData };

        // 趋势数据点（复用自动生成 DailyStatsDto；字段名 Date→StatDate 契约变更，列名即 StatDate）
        var trend = periodStats
            .OrderBy(d => d.StatDate)
            .Select(d => d.ToDto())
            .ToList();

        // BR-24：vsLastWeek——本周 vs 上周（仅与自己比；上周窗口 = 前 7 天）
        var thisWeek = periodStats.Sum(d => d.LearnedCount);
        var lastWeek = dailyStats
            .Where(d => d.StatDate >= today.AddDays(-13) && d.StatDate < today.AddDays(-6))
            .Sum(d => d.LearnedCount);
        var learnedDelta = thisWeek - lastWeek;

        // weaknessShift：薄弱点迁移（掌握度正确率 < 0.6 的知识点数量本周 vs 上周——切片以当前薄弱点数为基准）
        var masteryRows = await MasteryDs.EntitySelectAsync(
            x => x.UserId == request.StudentId, ct: ct);
        var weaknessCount = masteryRows.Count(m => m.Accuracy < 0.6);

        // BR-23：合规——只呈现相对进步，无群组正确率排名
        return new GetProgressReportResDto
        {
            Success = true,
            Trend = trend,
            VsLastWeek = new VsLastWeekDto
            {
                LearnedDelta = learnedDelta,
                WeaknessShift = weaknessCount,
            },
        };
    }
}

/// <summary>趋势周期枚举</summary>
public enum ProgressPeriod
{
    /// <summary>周报</summary>
    Week = 0,

    /// <summary>月报</summary>
    Month = 1,
}

/// <summary>进度趋势请求 DTO</summary>
public sealed record GetProgressReportReqDto
{
    /// <summary>孩子 Id</summary>
    public long StudentId { get; init; }

    /// <summary>周期（Week/Month）</summary>
    public string Period { get; init; } = string.Empty;
}

/// <summary>进度趋势响应 DTO（合规：无群组正确率排名）</summary>
public sealed record GetProgressReportResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>趋势数据点（复用 DailyStatsDto）</summary>
    public List<DailyStatsDto> Trend { get; init; } = [];

    /// <summary>相对进步（vsLastWeek）</summary>
    public VsLastWeekDto? VsLastWeek { get; init; }
}

/// <summary>相对进步 DTO</summary>
public sealed record VsLastWeekDto
{
    /// <summary>多背 N 篇（本周−上周）</summary>
    public int LearnedDelta { get; init; }

    /// <summary>薄弱点迁移（当前薄弱点数）</summary>
    public int WeaknessShift { get; init; }
}