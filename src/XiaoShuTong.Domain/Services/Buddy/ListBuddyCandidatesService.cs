using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.1 配合：可邀搭子候选列表（V0.6.16 好友发现前置）
/// </summary>
/// <remarks>
/// 搭子-BR-33：候选 = 同群组成员（Role=Student）排除自己/已有搭子关系（Pending/Accepted）/Parent；
/// 展示 GroupMembers.Nickname + Group.Name；多群同人去重。
/// 搭子-BR-34：候选查询只读无邀请动作；权限=当前用户群成员身份（非群主专属，区别于 ManageGroupMembersService BR-08）。
/// 好友发现源：InviteBuddyService BR-16 同群组/同年级（OR）——同群成员是充分子集，邀请不会触发 6006。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListBuddyCandidatesService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    /// <summary>
    /// 获取当前用户可邀搭子候选（无参，当前用户隐式）
    /// </summary>
    public async Task<ListBuddyCandidatesResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var meId = User.UserInfo?.Id ?? 0;

        // BR-33 Step1：我的群（GroupMembers 反查 UserId == me）
        var myMemberships = await MembersDs.EntitySelectAsync(
            x => x.UserId == meId, ct: ct);
        var myGroupIds = myMemberships.Select(m => m.GroupId).ToHashSet();
        if (myGroupIds.Count == 0)
            return new ListBuddyCandidatesResDto { Success = true, Items = [] }; // 无群 → 空候选（BR-33 空列表正常）

        // BR-33 Step2：同群其他学生成员（Role=Student，排除自己）
        var sameGroupMembers = await MembersDs.EntitySelectAsync(
            x => myGroupIds.Contains(x.GroupId) && x.Role == MemberRole.Student && x.UserId != meId, ct: ct);
        if (sameGroupMembers.Count == 0)
            return new ListBuddyCandidatesResDto { Success = true, Items = [] };

        // BR-33 Step3：排除已有搭子关系（Pending/Accepted，双向）
        var candidateUserIds = sameGroupMembers
            .Select(m => m.UserId)
            .Distinct()
            .ToArray();
        var existingPairs = await BuddiesDs.EntitySelectAsync(
            x => (x.InviterId == meId && candidateUserIds.Contains(x.InviteeId))
                 || (x.InviteeId == meId && candidateUserIds.Contains(x.InviterId))
                 || (candidateUserIds.Contains(x.InviterId) && x.InviteeId == meId),
            ct: ct);
        var excludedIds = existingPairs
            .Where(b => b.Status is BuddyStatus.Pending or BuddyStatus.Accepted)
            .Select(b => b.InviterId == meId ? b.InviteeId : b.InviterId)
            .ToHashSet();

        // Step4：群名映射（防 N+1——一次取所有相关群）
        var groupIds = sameGroupMembers.Select(m => m.GroupId).Distinct().ToArray();
        var groups = groupIds.Length == 0
            ? new List<Groups>()
            : await GroupsDs.EntitySelectAsync(x => groupIds.Contains(x.Id), ct: ct);
        var groupNameById = groups.ToDictionary(g => g.Id, g => g.Name);

        // Step5：多群同人去重（GROUP BY UserId；groupName 拼接）
        var candidateByUser = sameGroupMembers
            .Where(m => !excludedIds.Contains(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => new BuddyCandidateItemDto
            {
                UserId = g.Key,
                Nickname = g.First().Nickname ?? $"学生{g.Key}", // 空串兜底（同 V0.6.15 范式）
                GroupId = g.First().GroupId,
                GroupName = groupNameById.GetValueOrDefault(g.First().GroupId) ?? string.Empty,
            })
            .OrderBy(c => c.Nickname, StringComparer.Ordinal)
            .ToList();

        return new ListBuddyCandidatesResDto { Success = true, Items = candidateByUser };
    }
}

/// <summary>可邀搭子候选响应 DTO</summary>
public sealed record ListBuddyCandidatesResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>候选列表（同群学生成员，排除自己/已有搭子/Parent；多群去重）</summary>
    public List<BuddyCandidateItemDto> Items { get; init; } = [];
}

/// <summary>候选搭子项 DTO</summary>
public sealed record BuddyCandidateItemDto
{
    /// <summary>候选用户 Id（inviteBuddy_Execute inviteeUserId 入参）</summary>
    public long UserId { get; init; }

    /// <summary>群内昵称（GroupMembers.Nickname；空串兜底 学生{userId}）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>所属群 Id</summary>
    public long GroupId { get; init; }

    /// <summary>群名（Group.Name）</summary>
    public string GroupName { get; init; } = string.Empty;
}