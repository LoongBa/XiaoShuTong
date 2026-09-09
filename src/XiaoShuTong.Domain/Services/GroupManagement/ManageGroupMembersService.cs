using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.3：群组详情与成员管理（查询成员列表 / 移除成员）
/// </summary>
/// <remarks>
/// BR-07 群组不存在 → 1003 | BR-08 仅群主可管理 | BR-09 非本群成员/无权限 → 5003
/// BR-10 移除成员不删学习数据（仅删 GroupMembers 行） | BR-11 同一群组同一用户同一角色唯一 → 5002
/// 任务完成度（Progress）依赖模块 2（学习Session域），切片暂返回 0。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ManageGroupMembersService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    /// <summary>
    /// 查询群组成员列表（含任务完成度）
    /// </summary>
    public async Task<GroupDetailResDto> GetMembersAsync(GetMembersReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new GroupDetailResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        var members = await MembersDs.EntitySelectAsync(
            x => x.GroupId == request.GroupId,
            orderBy: q => q.OrderBy(x => x.JoinedAt),
            ct: ct);

        var memberItems = members.Select(m => new MemberItemDto
        {
            UserId = m.UserId,
            Nickname = m.Nickname,
            Role = m.Role.ToString(),
            JoinedAt = m.JoinedAt,
            Progress = 0, // 依赖模块 2（学习Session域），切片暂不实现
        }).ToList();

        return new GroupDetailResDto
        {
            Success = true,
            GroupId = group.Id,
            Members = memberItems,
        };
    }

    /// <summary>
    /// 移除群组成员（仅删成员行，不删学习数据）
    /// </summary>
    public async Task<RemoveMemberResDto> RemoveMemberAsync(RemoveMemberReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new RemoveMemberResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        // BR-09：非本群成员 → 5003
        var member = await MembersDs.EntityGetAsync(
            x => x.GroupId == request.GroupId && x.UserId == request.UserId, ct);
        if (member == null)
            return new RemoveMemberResDto { Success = false, ErrorCode = GroupErrorCodes.NotGroupMember };

        // BR-10：删除成员行（软删除=否 → 硬删除；学习数据不删）
        var deleted = await MembersDs.EntityDeleteBatchAsync(new[] { member.Id }, ct);
        var removed = deleted > 0;
        return new RemoveMemberResDto { Success = removed, Removed = removed };
    }
}

/// <summary>查询成员列表请求 DTO</summary>
public sealed record GetMembersReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }
}

/// <summary>移除成员请求 DTO</summary>
public sealed record RemoveMemberReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>移除目标成员用户 Id</summary>
    public long UserId { get; init; }
}

/// <summary>群组详情响应 DTO</summary>
public sealed record GroupDetailResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>成员列表</summary>
    public List<MemberItemDto> Members { get; init; } = [];
}

/// <summary>成员项 DTO</summary>
public sealed record MemberItemDto
{
    /// <summary>成员用户 Id</summary>
    public long UserId { get; init; }

    /// <summary>群内昵称</summary>
    public string? Nickname { get; init; }

    /// <summary>成员角色（student/parent）</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>加入时间</summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>任务完成度（依赖模块 2，切片为 0）</summary>
    public int Progress { get; init; }
}

/// <summary>移除成员响应 DTO</summary>
public sealed record RemoveMemberResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>移除结果</summary>
    public bool Removed { get; init; }
}
