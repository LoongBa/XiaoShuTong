using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.Entities.Buddy;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.3：拒绝搭子邀请
/// </summary>
/// <remarks>
/// BR-22 邀请不存在 → 6001 | BR-23 邀请过期/已处理 → 6002
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class RejectBuddyInviteService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    /// <summary>
    /// 拒绝邀请（关系置 Rejected）
    /// </summary>
    public async Task<RejectBuddyInviteResDto> ExecuteAsync(RejectBuddyInviteReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-22：邀请不存在 → 6001
        var buddy = await BuddiesDs.EntityGetAsync(x => x.UId == request.InviteId, ct);
        if (buddy == null)
            return new RejectBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteNotFound };

        // 权限：当前用户为被邀请人
        if (buddy.InviteeId != userId)
            return new RejectBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteNotFound };

        // BR-23：邀请过期/已处理 → 6002
        if (buddy.Status != BuddyStatus.Pending || buddy.ExpiresAt < DateTime.UtcNow)
            return new RejectBuddyInviteResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteExpired };

        buddy.Status = BuddyStatus.Rejected;
        await BuddiesDs.EntityUpdateAsync(buddy, ct);

        return new RejectBuddyInviteResDto { Success = true };
    }
}

/// <summary>拒绝搭子邀请请求 DTO</summary>
public sealed record RejectBuddyInviteReqDto
{
    /// <summary>邀请记录 Uid</summary>
    public string InviteId { get; init; } = string.Empty;
}

/// <summary>拒绝搭子邀请响应 DTO</summary>
public sealed record RejectBuddyInviteResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }
}