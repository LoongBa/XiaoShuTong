using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Rank;

namespace XiaoShuTong.Services.Rank;

/// <summary>
/// UC-7.2：我的排名 + 趋势
/// </summary>
/// <remarks>
/// BR-06 无快照正常返回 | BR-07 RankChange = 今日 rank − 昨日 rank | BR-08 仅个人（RLS）
/// 排名为组合指标（战力/战绩）在范围内求和的当日名次（与 GetRankingsService 同口径）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetMyRankingService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private RankSnapshotsDataService? _snapshotsDs;
    private RankSnapshotsDataService SnapshotsDs => _snapshotsDs ??= User.Use<RankSnapshotsDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 当前用户排名 + 趋势 + 变化量（组合指标求和口径）
    /// </summary>
    public async Task<GetMyRankingResDto> ExecuteAsync(GetMyRankingReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // 参数校验
        if (!Enum.TryParse<RankScopeType>(request.ScopeType, true, out var scopeType)
            || string.IsNullOrWhiteSpace(request.ScopeId)
            || !Enum.TryParse<RankBoardType>(request.Metric, true, out var boardType))
            return new GetMyRankingResDto { Success = false, ErrorCode = RankErrorCodes.ParamInvalid };

        // 战绩榜门控
        if (boardType == RankBoardType.Performance && scopeType == RankScopeType.Group)
        {
            var group = await GroupsDs.EntityGetAsync(x => x.UId == request.ScopeId, ct);
            if (group == null)
                return new GetMyRankingResDto { Success = false, ErrorCode = RankErrorCodes.NotFound };
            if (!group.RankEnabled)
                return new GetMyRankingResDto { Success = false, ErrorCode = RankErrorCodes.RankPerformanceDisabled, RankEnabled = false };
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject) ? "All" : request.Subject;
        var metrics = boardType == RankBoardType.Combat
            ? new[] { RankMetricType.Streak, RankMetricType.Volume, RankMetricType.PkWins }
            : new[] { RankMetricType.Accuracy, RankMetricType.Mastery };

        // 最新快照日期 + 昨日
        var latest = await LatestSnapshotDateAsync(scopeType, request.ScopeId, subject, metrics, ct);
        var todayRank = await RankOfUserAsync(scopeType, request.ScopeId, subject, metrics, latest, userId, ct);
        var yesterdayRank = await RankOfUserAsync(scopeType, request.ScopeId, subject, metrics, latest.AddDays(-1), userId, ct);

        // BR-06：无快照 → 空响应
        if (todayRank == null)
            return new GetMyRankingResDto { Success = true, RankEnabled = true };

        // BR-07：RankChange = 今日 rank − 昨日 rank（正=上升；今日 3 昨日 5 → +2）
        var rankChange = yesterdayRank is { } yRank ? yRank.Rank - todayRank.Value.Rank : 0;
        var trend = yesterdayRank == null ? "Flat"
            : todayRank.Value.Rank < yesterdayRank.Value.Rank ? "Up"
            : todayRank.Value.Rank > yesterdayRank.Value.Rank ? "Down"
            : "Flat";

        return new GetMyRankingResDto
        {
            Success = true,
            Rank = todayRank.Value.Rank,
            Value = todayRank.Value.Value,
            Trend = trend,
            RankChange = rankChange,
            RankEnabled = true,
        };
    }

    /// <summary>指定日期上当前用户组合求和后的名次</summary>
    private async Task<(int Rank, decimal Value)?> RankOfUserAsync(
        RankScopeType scopeType, string scopeId, string subject, RankMetricType[] metrics,
        DateOnly date, long userId, CancellationToken ct)
    {
        var rows = await SnapshotsDs.EntitySelectAsync(
            x => x.ScopeType == scopeType && x.ScopeId == scopeId && x.Subject == subject
                 && x.SnapshotDate == date && metrics.Contains(x.MetricType),
            ct: ct);
        if (rows.Count == 0)
            return null;

        var combined = rows
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Value = g.Sum(r => r.MetricValue) })
            .OrderByDescending(x => x.Value)
            .ToList();

        var index = combined.FindIndex(c => c.UserId == userId);
        return index < 0 ? null : (index + 1, combined[index].Value);
    }

    private async Task<DateOnly> LatestSnapshotDateAsync(
        RankScopeType scopeType, string scopeId, string subject, RankMetricType[] metrics, CancellationToken ct)
    {
        var rows = await SnapshotsDs.EntitySelectAsync(
            x => x.ScopeType == scopeType && x.ScopeId == scopeId && x.Subject == subject
                 && metrics.Contains(x.MetricType),
            ct: ct);
        return rows.Count == 0 ? DateOnly.FromDateTime(DateTime.UtcNow) : rows.Max(r => r.SnapshotDate);
    }
}

/// <summary>我的排名请求 DTO</summary>
public sealed record GetMyRankingReqDto
{
    /// <summary>范围类型（Group/Grade）</summary>
    public string ScopeType { get; init; } = string.Empty;

    /// <summary>群组 id 或年级 key</summary>
    public string ScopeId { get; init; } = string.Empty;

    /// <summary>学科筛选</summary>
    public string? Subject { get; init; }

    /// <summary>榜单类型（Combat/Performance）</summary>
    public string Metric { get; init; } = string.Empty;
}

/// <summary>我的排名响应 DTO</summary>
public sealed record GetMyRankingResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>当前排名</summary>
    public int Rank { get; init; }

    /// <summary>战力/战绩值</summary>
    public decimal Value { get; init; }

    /// <summary>趋势（Up/Down/Flat）</summary>
    public string Trend { get; init; } = "Flat";

    /// <summary>今日 rank − 昨日 rank（正=上升）</summary>
    public int RankChange { get; init; }

    /// <summary>战绩榜开关</summary>
    public bool RankEnabled { get; init; } = true;
}