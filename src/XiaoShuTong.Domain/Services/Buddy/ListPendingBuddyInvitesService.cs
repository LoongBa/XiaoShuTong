using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.2/8.3 前置：待收搭子邀请列表（V0.6.18 闭环）
/// </summary>
/// <remarks>
/// 搭子-BR-35：InviteeId=me 且 Status=Pending 且未过期（ExpiresAt &gt;= now，惰性过期不落库——与 accept/reject 6002 的 ExpiresAt &lt; now 判断互补一致）；
/// 副作用：惰性过期致 BR-17 重复邀请判断（只看 Status 不看 ExpiresAt）仍命中已过期 Pending（→6002）阻塞对方重邀——本轮不修正（待收查询只读不触碰写路径），后续迭代修正 BR-17 查询条件。
/// 搭子-BR-36：只读无处理动作；展示 GroupMembers.Nickname（null 兜底 学生{userId}）+ InvitedAt/ExpiresAt；按 InvitedAt 倒序（最新优先处理）；
/// 无分页（量级有限：受 BR-15 发起方当日 ≤10 + BR-18 +7 天过期约束，与 ListBuddies/ListBuddyCandidates 同范式）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListPendingBuddyInvitesService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    /// <summary>
    /// 当前用户收到的待处理搭子邀请（无参，当前用户隐式；BR-35/36）
    /// </summary>
    public async Task<ListPendingBuddyInvitesResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var meId = User.UserInfo?.Id ?? 0;
        var now = DateTime.UtcNow;

        // BR-35：InviteeId=me 且 Pending 且未过期（ExpiresAt >= now，惰性过期过滤不落库）
        var invites = await BuddiesDs.EntitySelectAsync(
            x => x.InviteeId == meId && x.Status == BuddyStatus.Pending && x.ExpiresAt >= now, ct: ct);

        // BR-36：无待收 → 空列表（前端隐藏区块）
        if (invites.Count == 0)
            return new ListPendingBuddyInvitesResDto { Success = true, Items = [] };

        // BR-36：昵称富化（GroupMembers.Nickname，一次 IN 查询防 N+1；多群同人 GroupBy 取 First）
        var inviterIds = invites.Select(i => i.InviterId).Distinct().ToArray();
        var members = await MembersDs.EntitySelectAsync(x => inviterIds.Contains(x.UserId), ct: ct);
        var nicknameByUser = members
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.First().Nickname);

        // BR-36：InvitedAt 倒序（最新优先处理）；avatar 账户域空串桩（同 ListBuddiesService 范式）
        var items = invites
            .OrderByDescending(i => i.InvitedAt)
            .Select(i => new PendingBuddyInviteItemDto
            {
                InviteId = i.UId,
                InviterUserId = i.InviterId,
                Nickname = nicknameByUser.GetValueOrDefault(i.InviterId) ?? $"学生{i.InviterId}",
                AvatarUrl = string.Empty,
                InvitedAt = i.InvitedAt,
                ExpiresAt = i.ExpiresAt,
            })
            .ToList();

        return new ListPendingBuddyInvitesResDto { Success = true, Items = items };
    }
}

/// <summary>待收搭子邀请响应 DTO</summary>
public sealed record ListPendingBuddyInvitesResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>待处理邀请列表（InviteeId=me 的 Pending 未过期邀请，按 InvitedAt 倒序）</summary>
    public List<PendingBuddyInviteItemDto> Items { get; init; } = [];
}

/// <summary>待收邀请项 DTO</summary>
public sealed record PendingBuddyInviteItemDto
{
    /// <summary>邀请记录 Uid（accept/reject 入参 InviteId）</summary>
    public string InviteId { get; init; } = string.Empty;

    /// <summary>邀请人用户 Id</summary>
    public long InviterUserId { get; init; }

    /// <summary>邀请人昵称（GroupMembers.Nickname；null 兜底 学生{userId}）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>头像 URL（账户域，切片为空串）</summary>
    public string AvatarUrl { get; init; } = string.Empty;

    /// <summary>邀请时间</summary>
    public DateTime InvitedAt { get; init; }

    /// <summary>过期时间（InvitedAt + 7 天）</summary>
    public DateTime ExpiresAt { get; init; }
}
