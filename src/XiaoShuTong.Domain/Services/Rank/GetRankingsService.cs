using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Rank;

namespace XiaoShuTong.Services.Rank;

/// <summary>
/// UC-7.1：榜单查询（战力榜/战绩榜）
/// </summary>
/// <remarks>
/// BR-01 无数据空列表 | BR-02 战绩榜 RankEnabled 门控（false → 7001）| BR-03 参数校验
/// BR-04 战力榜不受 RankEnabled 影响 | BR-05 趋势 = 今日 vs 昨日快照（持平=flat）
/// 战力 = streak+volume+pk_wins；战绩 = accuracy+mastery（简单求和）；昵称/头像依赖账户域（桩为空串）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetRankingsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private RankSnapshotsDataService? _snapshotsDs;
    private RankSnapshotsDataService SnapshotsDs => _snapshotsDs ??= User.Use<RankSnapshotsDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 榜单查询（组合指标求和 + 趋势箭头）
    /// </summary>
    public async Task<GetRankingsResDto> ExecuteAsync(GetRankingsReqDto request, CancellationToken ct = default)
    {
        // BR-03：参数校验（ScopeType/ScopeId/Metric 必填合法）
        if (!Enum.TryParse<RankScopeType>(request.ScopeType, true, out var scopeType)
            || string.IsNullOrWhiteSpace(request.ScopeId)
            || !Enum.TryParse<RankBoardType>(request.Metric, true, out var boardType))
            return new GetRankingsResDto { Success = false, ErrorCode = RankErrorCodes.ParamInvalid };

        var subject = string.IsNullOrWhiteSpace(request.Subject) ? "All" : request.Subject;
        var limit = request.Limit is < 1 or > 100 ? 50 : request.Limit;

        // BR-02：战绩榜校验 RankEnabled（群组范围跨模块校验）
        if (boardType == RankBoardType.Performance && scopeType == RankScopeType.Group)
        {
            var group = await GroupsDs.EntityGetAsync(x => x.UId == request.ScopeId, ct);
            if (group == null)
                return new GetRankingsResDto { Success = false, ErrorCode = RankErrorCodes.NotFound };
            if (!group.RankEnabled)
                return new GetRankingsResDto
                {
                    Success = false,
                    ErrorCode = RankErrorCodes.RankPerformanceDisabled,
                    RankEnabled = false,
                };
        }

        // 该榜单覆盖的指标集（BR-11/12：简单求和）
        var metrics = boardType == RankBoardType.Combat
            ? new[] { RankMetricType.Streak, RankMetricType.Volume, RankMetricType.PkWins }
            : new[] { RankMetricType.Accuracy, RankMetricType.Mastery };

        var (snapshotDate, items) = await BuildRankingAsync(scopeType, request.ScopeId, metrics, subject, ct);

        // BR-01：无排名数据 → 空列表
        if (items.Count == 0)
            return new GetRankingsResDto { Success = true, SnapshotDate = snapshotDate, RankEnabled = true, Items = [] };

        return new GetRankingsResDto
        {
            Success = true,
            SnapshotDate = snapshotDate,
            RankEnabled = true,
            Items = items.Take(limit).ToList(),
        };
    }

    /// <summary>组装指定日期榜单（按用户组合指标求和 + 排序 + 与昨日对比趋势）</summary>
    internal async Task<(DateTime Date, List<RankingItemDto> Items)> BuildRankingAsync(
        RankScopeType scopeType, string scopeId, RankMetricType[] metrics, string subject, CancellationToken ct)
    {
        // 最新快照日期
        var latest = await LatestSnapshotDateAsync(scopeType, scopeId, subject, metrics, ct);

        var rows = await SnapshotsDs.EntitySelectAsync(
            x => x.ScopeType == scopeType && x.ScopeId == scopeId && x.Subject == subject
                 && x.SnapshotDate == latest && metrics.Contains(x.MetricType),
            ct: ct);

        // 按用户组合求和（BR-11/12 简单求和）
        var combined = rows
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Value = g.Sum(r => r.MetricValue),
                Rank = g.Min(r => r.Rank),
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        // 昨日对比（BR-05：趋势箭头）
        var yesterday = latest.AddDays(-1);
        var yesterdayRows = await SnapshotsDs.EntitySelectAsync(
            x => x.ScopeType == scopeType && x.ScopeId == scopeId && x.Subject == subject
                 && x.SnapshotDate == yesterday && metrics.Contains(x.MetricType),
            ct: ct);
        var yesterdayRank = yesterdayRows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Min(r => r.Rank));

        var meId = User.UserInfo?.Id ?? 0;
        var items = combined.Select((x, index) =>
        {
            var rank = index + 1;
            var prev = yesterdayRank.GetValueOrDefault(x.UserId, 0);
            var trend = prev == 0 ? "Flat" : rank < prev ? "Up" : rank > prev ? "Down" : "Flat";
            return new RankingItemDto
            {
                Rank = rank,
                UserId = x.UserId,
                Nickname = string.Empty, // 账户域（跨模块），切片为空串
                AvatarUrl = string.Empty,
                Value = x.Value,
                Trend = trend,
                IsMe = x.UserId == meId,
            };
        }).ToList();

        return (latest.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), items);
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

/// <summary>榜单类型（Combat 战力 / Performance 战绩）</summary>
public enum RankBoardType
{
    /// <summary>战力榜（streak+volume+pk_wins）</summary>
    Combat = 0,

    /// <summary>战绩榜（accuracy+mastery）</summary>
    Performance = 1,
}

/// <summary>榜单查询请求 DTO</summary>
public sealed record GetRankingsReqDto
{
    /// <summary>范围类型（Group/Grade）</summary>
    public string ScopeType { get; init; } = string.Empty;

    /// <summary>群组 id 或年级 key</summary>
    public string ScopeId { get; init; } = string.Empty;

    /// <summary>学科筛选（默认 All）</summary>
    public string? Subject { get; init; }

    /// <summary>榜单类型（Combat 战力 / Performance 战绩）</summary>
    public string Metric { get; init; } = string.Empty;

    /// <summary>指定快照日期（默认最新）</summary>
    public DateTime? Date { get; init; }

    /// <summary>返回条数上限（默认 50）</summary>
    public int Limit { get; init; } = 50;
}

/// <summary>榜单查询响应 DTO</summary>
public sealed record GetRankingsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>快照日期</summary>
    public DateTime SnapshotDate { get; init; }

    /// <summary>该群组战绩榜是否开启</summary>
    public bool RankEnabled { get; init; } = true;

    /// <summary>排名列表</summary>
    public List<RankingItemDto> Items { get; init; } = [];
}

/// <summary>排名项 DTO</summary>
public sealed record RankingItemDto
{
    /// <summary>排名</summary>
    public int Rank { get; init; }

    /// <summary>用户 Id</summary>
    public long UserId { get; init; }

    /// <summary>昵称（账户域，切片为空）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>头像 URL（账户域，切片为空）</summary>
    public string AvatarUrl { get; init; } = string.Empty;

    /// <summary>指标值（组合求和）</summary>
    public decimal Value { get; init; }

    /// <summary>趋势（Up/Down/Flat）</summary>
    public string Trend { get; init; } = "Flat";

    /// <summary>是否本人</summary>
    public bool IsMe { get; init; }
}