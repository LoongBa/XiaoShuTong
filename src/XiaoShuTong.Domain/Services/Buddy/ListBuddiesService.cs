using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Entities.Rank.DTOs;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.4：搭子列表（含排名互看）
/// </summary>
/// <remarks>
/// BR-24 无搭子空列表 | BR-25 仅 accepted（UNION 双向）| BR-26 rank = 对方最新快照（仅排名与数值）| BR-27 不暴露答题明细
/// 连续打卡天数 = DailyStats 当前连击（跨模块）；昵称/头像依赖账户域（桩为空串）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListBuddiesService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    private RankSnapshotsDataService? _snapshotsDs;
    private RankSnapshotsDataService SnapshotsDs => _snapshotsDs ??= User.Use<RankSnapshotsDataService>();

    /// <summary>
    /// 当前用户全部 accepted 搭子（双向 UNION 语义）+ 各搭子最新排名快照
    /// </summary>
    public async Task<ListBuddiesResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-25：仅 accepted（InviterId=me OR InviteeId=me）
        var buddies = await BuddiesDs.EntitySelectAsync(
            x => (x.InviterId == userId || x.InviteeId == userId) && x.Status == BuddyStatus.Accepted, ct: ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var items = new List<BuddyListItemDto>();
        foreach (var buddy in buddies)
        {
            var otherId = buddy.InviterId == userId ? buddy.InviteeId : buddy.InviterId;

            // 连续打卡天数（跨模块）
            var dailyStats = await DailyDs.EntitySelectAsync(
                x => x.UserId == otherId, ct: ct);
            var streakDays = StreakCalculator.CalcCurrentStreak(
                dailyStats.Select(d => d.StatDate).ToList(), today);

            // 对方最新排名快照（BR-26/27：仅排名与数值）
            var rankSnapshot = await LatestRankSnapshotAsync(otherId, ct);

            items.Add(new BuddyListItemDto
            {
                BuddyId = buddy.UId,
                UserId = otherId,
                Nickname = string.Empty, // 账户域（跨模块），切片为空串
                AvatarUrl = string.Empty,
                StreakDays = streakDays,
                Status = buddy.Status.ToString(),
                Rank = rankSnapshot,
            });
        }

        // BR-24：无搭子空列表
        return new ListBuddiesResDto { Success = true, Items = items };
    }

    private async Task<RankSnapshotsDto?> LatestRankSnapshotAsync(long otherId, CancellationToken ct)
    {
        var snapshots = await SnapshotsDs.EntitySelectAsync(
            x => x.UserId == otherId, ct: ct);
        var latest = snapshots.OrderByDescending(s => s.SnapshotDate).FirstOrDefault();
        if (latest == null)
            return null;

        // DTO 最小化：复用自动生成 RankSnapshotsDto（SnapshotDate DateOnly 契约变更，列类型即 DateOnly）
        return latest.ToDto();
    }
}

/// <summary>搭子列表响应 DTO</summary>
public sealed record ListBuddiesResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>搭子列表</summary>
    public List<BuddyListItemDto> Items { get; init; } = [];
}

/// <summary>搭子列表项 DTO</summary>
public sealed record BuddyListItemDto
{
    /// <summary>搭子关系 Uid</summary>
    public string BuddyId { get; init; } = string.Empty;

    /// <summary>搭子用户 Id</summary>
    public long UserId { get; init; }

    /// <summary>昵称（账户域，切片为空）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>头像 URL（账户域，切片为空）</summary>
    public string AvatarUrl { get; init; } = string.Empty;

    /// <summary>连续打卡天数</summary>
    public int StreakDays { get; init; }

    /// <summary>关系状态</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>对方最新排名快照（仅排名与数值，不暴露答题明细）</summary>
    public RankSnapshotsDto? Rank { get; init; }
}