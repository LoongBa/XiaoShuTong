using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Shared;

namespace XiaoShuTong.Services.Stats;

/// <summary>
/// UC-6.3：查看学习报告（周/月）
/// </summary>
/// <remarks>
/// BR-08 无数据返回占位（码 0，非错误）| BR-09 Period ∈ Week/Month | BR-10 报告无群组正确率排名（合规）| BR-11 个人正确率
/// 周期为动态区间（前端换算），Service 层编排聚合（VEntity 设计规格：不建视图）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetPeriodReportService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    /// <summary>
    /// 周期学习报告（学习量/个人正确率/★数/薄弱点）
    /// </summary>
    public async Task<GetPeriodReportResDto> ExecuteAsync(GetPeriodReportReqDto request, CancellationToken ct = default)
    {
        // BR-09：Period 合法（Week/Month）
        if (!Enum.TryParse<ReportPeriod>(request.Period, true, out var period))
            return new GetPeriodReportResDto { Success = false, ErrorCode = StatsErrorCodes.ParamInvalid };

        var userId = User.UserInfo?.Id ?? 0;

        // 周期区间（Week = 近 7 天含今日；Month = 近 30 天）
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var start = period == ReportPeriod.Week ? today.AddDays(-6) : today.AddDays(-29);

        var dailyStats = await DailyDs.EntitySelectAsync(
            x => x.UserId == userId && x.StatDate >= start && x.StatDate <= today, ct: ct);

        // BR-08：无周期数据 → 字段为空 + 码 0（前端渲染占位）
        if (dailyStats.Count == 0)
        {
            return new GetPeriodReportResDto
            {
                Success = true,
                LearnedCount = 0,
                Accuracy = null,
                StarredCount = 0,
                WeakPoints = [],
            };
        }

        // BR-11：个人正确率 = 有记录日的正确率均值；学习量/★ 累加
        var learnedCount = dailyStats.Sum(d => d.LearnedCount);
        var starredCount = dailyStats.Sum(d => d.StarredCount);
        var accuracies = dailyStats.Where(d => d.Accuracy.HasValue).Select(d => d.Accuracy!.Value).ToList();
        var accuracy = accuracies.Count == 0 ? (double?)null : Math.Round(accuracies.Average(), 4);

        // 薄弱点（掌握度正确率升序，取前 5）——BR-10：非群组排名，合规
        var weakPoints = (await MasteryDs.EntitySelectAsync(
                x => x.UserId == userId, ct: ct))
            .OrderBy(m => m.Accuracy)
            .Take(5)
            .Select(m => new WeakPointDto
            {
                KnowledgePoint = m.KnowledgePoint,
                Accuracy = m.Accuracy,
            })
            .ToList();

        return new GetPeriodReportResDto
        {
            Success = true,
            LearnedCount = learnedCount,
            Accuracy = accuracy,
            StarredCount = starredCount,
            WeakPoints = weakPoints,
        };
    }
}

/// <summary>报告周期枚举</summary>
public enum ReportPeriod
{
    /// <summary>周报</summary>
    Week = 0,

    /// <summary>月报</summary>
    Month = 1,
}

/// <summary>学习报告请求 DTO</summary>
public sealed record GetPeriodReportReqDto
{
    /// <summary>周期（Week/Month）</summary>
    public string Period { get; init; } = string.Empty;
}

/// <summary>学习报告响应 DTO</summary>
public sealed record GetPeriodReportResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>周期学习题数</summary>
    public int LearnedCount { get; init; }

    /// <summary>周期正确率（个人）</summary>
    public double? Accuracy { get; init; }

    /// <summary>点亮 ★ 数</summary>
    public int StarredCount { get; init; }

    /// <summary>薄弱知识点（正确率升序）</summary>
    public List<WeakPointDto> WeakPoints { get; init; } = [];
}