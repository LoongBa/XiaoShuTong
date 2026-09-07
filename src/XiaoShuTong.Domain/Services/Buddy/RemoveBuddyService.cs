using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.Entities.Buddy;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.6：解除搭子
/// </summary>
/// <remarks>
/// BR-30 关系不存在/非当事人 → 6001 | BR-31 解除后历史保留（Status=Removed）| BR-32 重复解除（已 removed）→ 6001
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class RemoveBuddyService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    /// <summary>
    /// 解除搭子（任一方可发起，关系置 Removed 保留历史）
    /// </summary>
    public async Task<RemoveBuddyResDto> ExecuteAsync(RemoveBuddyReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-30：关系存在且为当事人（否则 6001）
        var buddy = await BuddiesDs.EntityGetAsync(x => x.UId == request.BuddyId, ct);
        if (buddy == null
            || (buddy.InviterId != userId && buddy.InviteeId != userId)
            || buddy.Status != BuddyStatus.Accepted)
            return new RemoveBuddyResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteNotFound };

        // BR-31：置 Removed（历史保留）；BR-32：二次解除已被上面状态校验拦截 → 6001
        buddy.Status = BuddyStatus.Removed;
        await BuddiesDs.EntityUpdateAsync(buddy, ct);

        return new RemoveBuddyResDto { Success = true };
    }
}

/// <summary>解除搭子请求 DTO</summary>
public sealed record RemoveBuddyReqDto
{
    /// <summary>搭子关系 Uid</summary>
    public string BuddyId { get; init; } = string.Empty;
}

/// <summary>解除搭子响应 DTO</summary>
public sealed record RemoveBuddyResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }
}