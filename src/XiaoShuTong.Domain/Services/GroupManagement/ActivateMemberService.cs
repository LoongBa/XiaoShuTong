using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.10：成员凭码激活加入群组
/// </summary>
/// <remarks>
/// 事务范围：CROSS（写 GroupMembers + 绑定码 + 标记 used），成员写入与码状态更新须原子。
/// BR-26 码必须未使用未过期 → 5202 | BR-27 同码重复激活幂等 | BR-28 码过期 → 5202
/// BR-29 后四位一致 → 5203 | BR-30 绑定微信 ID | BR-31 同群组同用户同角色唯一 → 5002
/// 角色由名单预设（student/parent）；切片未存角色列，默认 Student（DS 未覆盖）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class ActivateMemberService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private OneTimeInviteCodesDataService? _codesDs;
    private OneTimeInviteCodesDataService CodesDs => _codesDs ??= User.Use<OneTimeInviteCodesDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 校验一次性码 + 后四位 → 写入成员 → 绑定并置码 used
    /// </summary>
    public async Task<ActivateMemberResDto> ExecuteAsync(ActivateMemberReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-05：内测未开放 → 5205
        if (!BetaAccessSettings.IsBetaOpen)
            return new ActivateMemberResDto { Success = false, ErrorCode = GroupErrorCodes.BetaNotOpen };

        // BR-26/BR-28：码必须存在、未使用、未过期 → 5202
        var code = await CodesDs.EntityGetAsync(x => x.Code == request.OneTimeCode, ct);
        if (code == null)
            return new ActivateMemberResDto { Success = false, ErrorCode = GroupErrorCodes.OneTimeCodeInvalid };

        if (code.Status == OneTimeCodeStatus.Expired || code.ExpiresAt is { } exp && exp <= DateTime.UtcNow)
            return new ActivateMemberResDto { Success = false, ErrorCode = GroupErrorCodes.OneTimeCodeInvalid };

        // BR-27：同码重复激活幂等——码已 Used 且绑定当前用户 → 返回原结果
        if (code.Status == OneTimeCodeStatus.Used)
        {
            if (code.BoundUserId == userId)
                return await BuildIdempotentResultAsync(code, ct);
            return new ActivateMemberResDto { Success = false, ErrorCode = GroupErrorCodes.OneTimeCodeInvalid };
        }

        // BR-29：后四位与名单 PhoneLast4 一致 → 5203
        if (!string.Equals(request.PhoneLast4, code.PhoneLast4, StringComparison.Ordinal))
            return new ActivateMemberResDto { Success = false, ErrorCode = GroupErrorCodes.PhoneLast4Mismatch };

        // BR-31：同一群组同一用户同一角色唯一 → 5002
        var memberRole = MemberRole.Student; // 名单预设角色（DS 未定义角色存储列，切片默认 Student）
        var existingMember = await MembersDs.EntityGetAsync(
            x => x.GroupId == code.GroupId && x.UserId == userId && x.Role == memberRole, ct);
        if (existingMember != null)
            return new ActivateMemberResDto { Success = false, ErrorCode = GroupErrorCodes.AlreadyInGroup };

        // 写入 GroupMembers（InviteCodeId 溯源）
        var member = await MembersDs.EntityCreateAsync(new GroupMembers
        {
            UId = UidGenerator.NewId(),
            GroupId = code.GroupId,
            UserId = userId,
            Role = memberRole,
            InviteCodeId = code.Id,
            JoinedAt = DateTime.UtcNow,
        }, ct);

        // BR-26/BR-30：码绑定 BoundUserId + 置 Used + UsedAt
        code.BoundUserId = userId;
        code.Status = OneTimeCodeStatus.Used;
        code.UsedAt = DateTime.UtcNow;
        await CodesDs.EntityUpdateAsync(code, ct);

        var group = await GroupsDs.EntityGetAsync(x => x.Id == code.GroupId, ct);

        return new ActivateMemberResDto
        {
            Success = true,
            GroupId = code.GroupId,
            GroupName = group?.Name ?? string.Empty,
            Role = memberRole.ToString(),
            MemberId = member.Id,
        };
    }

    /// <summary>
    /// 幂等返回：码已绑定当前用户时返回原激活结果（BR-27）
    /// </summary>
    private async Task<ActivateMemberResDto> BuildIdempotentResultAsync(
        OneTimeInviteCodes code, CancellationToken ct)
    {
        var member = await MembersDs.EntityGetAsync(
            x => x.InviteCodeId == code.Id, ct);
        var group = await GroupsDs.EntityGetAsync(x => x.Id == code.GroupId, ct);

        return new ActivateMemberResDto
        {
            Success = true,
            GroupId = code.GroupId,
            GroupName = group?.Name ?? string.Empty,
            Role = member?.Role.ToString() ?? string.Empty,
            MemberId = member?.Id ?? 0,
        };
    }
}

/// <summary>成员激活请求 DTO</summary>
public sealed record ActivateMemberReqDto
{
    /// <summary>一次性邀请码（8 位）</summary>
    public string OneTimeCode { get; init; } = string.Empty;

    /// <summary>手机号后四位（4 位）</summary>
    public string PhoneLast4 { get; init; } = string.Empty;
}

/// <summary>成员激活响应 DTO</summary>
public sealed record ActivateMemberResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>加入的群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>群组名</summary>
    public string GroupName { get; init; } = string.Empty;

    /// <summary>成员角色（student/parent）</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>GroupMembers Id</summary>
    public long MemberId { get; init; }
}
