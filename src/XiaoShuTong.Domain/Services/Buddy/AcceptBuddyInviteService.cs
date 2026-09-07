using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.Entities.Buddy;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.2：同意搭子邀请
/// </summary>
/// <remarks>
/// CROSS：双方 accepted 数校验（FOR UPDATE 防并发）+ 关系置 Accepted。
/// BR-19 邀请不存在 → 6001 | BR-20 邀请过期/已处理 → 6002 | BR-21 双方 accepted 数 ≤5 → 6003
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class AcceptBuddyInviteService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int MaxBuddies = 5;

    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    /// <summary>
    /// 同意邀请（校验双方 ≤5，关系置 Accepted）
    /// </summary>
    public async Task<AcceptBuddyInviteResDto> ExecuteAsync(AcceptBuddyInviteReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-19：邀请不存在 → 6001
        var buddy = await BuddiesDs.EntityGetAsync(x => x.UId == request.InviteId, ct);
        if (buddy == null)
            return new AcceptBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteNotFound };

        // 权限：当前用户为被邀请人
        if (buddy.InviteeId != userId)
            return new AcceptBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteNotFound };

        // BR-20：邀请过期/已处理 → 6002
        if (buddy.Status != BuddyStatus.Pending || buddy.ExpiresAt < DateTime.UtcNow)
            return new AcceptBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteExpired };

        // BR-21：双方 accepted 数 ≤5（FOR UPDATE 语义——切片 NoAop 路径以顺序校验近似）
        var myAccepted = await BuddiesDs.CountAsync(
            x => (x.InviterId == userId || x.InviteeId == userId) && x.Status == BuddyStatus.Accepted, ct);
        var inviterAccepted = await BuddiesDs.CountAsync(
            x => (x.InviterId == buddy.InviterId || x.InviteeId == buddy.InviterId) && x.Status == BuddyStatus.Accepted, ct);
        if (myAccepted >= MaxBuddies || inviterAccepted >= MaxBuddies)
            return new AcceptBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyLimitReached };

        // 状态置 Accepted + AcceptedAt
        buddy.Status = BuddyStatus.Accepted;
        buddy.AcceptedAt = DateTime.UtcNow;
        await BuddiesDs.EntityUpdateAsync(buddy, ct);

        return new AcceptBuddyInviteResDto { Success = true, BuddyId = buddy.UId };
    }
}

/// <summary>同意搭子邀请请求 DTO</summary>
public sealed record AcceptBuddyInviteReqDto
{
    /// <summary>邀请记录 Uid</summary>
    public string InviteId { get; init; } = string.Empty;
}

/// <summary>同意搭子邀请响应 DTO</summary>
public sealed record AcceptBuddyInviteResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>搭子关系 Uid</summary>
    public string BuddyId { get; init; } = string.Empty;
}