using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Services.Rank;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.5：查看搭子排名详情
/// </summary>
/// <remarks>
/// BR-28 非 accepted 搭子 → 6004 | BR-29 仅返回排名与数值（不暴露答题明细）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetBuddyRankService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    private RankSnapshotsDataService? _snapshotsDs;
    private RankSnapshotsDataService SnapshotsDs => _snapshotsDs ??= User.Use<RankSnapshotsDataService>();

    /// <summary>
    /// 搭子排名详情（最新快照：排名/数值/趋势）
    /// </summary>
    public async Task<GetBuddyRankResDto> ExecuteAsync(GetBuddyRankReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        if (!Enum.TryParse<RankScopeType>(request.ScopeType, true, out var scopeType)
            || string.IsNullOrWhiteSpace(request.ScopeId))
            return new GetBuddyRankResDto { Success = false, ErrorCode = RankErrorCodes.ParamInvalid };

        // BR-28：与目标为 accepted 搭子 → 否则 6004
        var buddy = await BuddiesDs.EntityGetAsync(x => x.UId == request.BuddyId, ct);
        if (buddy == null
            || buddy.Status != BuddyStatus.Accepted
            || (buddy.InviterId != userId && buddy.InviteeId != userId))
            return new GetBuddyRankResDto { Success = false, ErrorCode = BuddyErrorCodes.NotBuddy };

        var otherId = buddy.InviterId == userId ? buddy.InviteeId : buddy.InviterId;
        var subject = string.IsNullOrWhiteSpace(request.Subject) ? "All" : request.Subject;

        // 对方最新快照（按范围 + 学科）
        var snapshot = (await SnapshotsDs.EntitySelectAsync(
                x => x.UserId == otherId && x.ScopeType == scopeType && x.ScopeId == request.ScopeId
                     && x.Subject == subject, ct: ct))
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefault();
        if (snapshot == null)
            return new GetBuddyRankResDto { Success = true };

        // BR-29：仅排名与数值（不暴露答题明细）
        return new GetBuddyRankResDto
        {
            Success = true,
            Rank = snapshot.Rank,
            MetricValue = snapshot.MetricValue,
            Trend = "Flat",
            SnapshotDate = snapshot.SnapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        };
    }
}

/// <summary>搭子排名详情请求 DTO</summary>
public sealed record GetBuddyRankReqDto
{
    /// <summary>搭子关系 Uid</summary>
    public string BuddyId { get; init; } = string.Empty;

    /// <summary>范围类型（Group/Grade）</summary>
    public string ScopeType { get; init; } = string.Empty;

    /// <summary>群组 id 或年级 key</summary>
    public string ScopeId { get; init; } = string.Empty;

    /// <summary>学科筛选</summary>
    public string? Subject { get; init; }
}

/// <summary>搭子排名详情响应 DTO</summary>
public sealed record GetBuddyRankResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>排名</summary>
    public int Rank { get; init; }

    /// <summary>战力/战绩值</summary>
    public decimal MetricValue { get; init; }

    /// <summary>趋势（Up/Down/Flat）</summary>
    public string Trend { get; init; } = "Flat";

    /// <summary>快照日期</summary>
    public DateTime SnapshotDate { get; init; }
}