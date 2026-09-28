using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.1 配合：可邀搭子候选列表（V0.6.16 好友发现前置；2026-09-29 同年级跨群候选扩展）
/// </summary>
/// <remarks>
/// 搭子-BR-33：候选 = 同群组 或 同年级成员（Role=Student，OR 语义对齐 BR-16）排除自己/已有搭子关系（Pending/Accepted）/Parent；
/// 展示 GroupMembers.Nickname + Group.Name；多群同人去重（跨群合并）。
/// 搭子-BR-34：候选查询只读无邀请动作；权限=当前用户群成员身份（非群主专属，区别于 ManageGroupMembersService BR-08）。
/// 好友发现源：InviteBuddyService BR-16 同群组/同年级（OR）——候选集合恰好覆盖可邀范围，邀请不会触发 6006。
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

        // BR-33 Step2：我的年级（Groups.Grade 去重，经 GroupMembers→Groups 推导；群未设年级(Grade=null)不参与同年级推导）
        var myGroups = await GroupsDs.EntitySelectAsync(x => myGroupIds.Contains(x.Id), ct: ct);
        var myGrades = myGroups
            .Select(g => g.Grade)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct()
            .ToHashSet();

        // BR-33 Step3：同年级群集收窄（先 Groups(Grade ∈ myGrades) 避免全库 GroupMembers 扫描）
        // 候选群集 = 同群(myGroupIds) ∪ 同年级群(gradeGroupIds)——Oracle M-5/M-6 确认的 OR 语义，对齐 BR-16
        var gradeGroups = new List<Groups>();
        if (myGrades.Count > 0)
            gradeGroups = await GroupsDs.EntitySelectAsync(x => myGrades.Contains(x.Grade), ct: ct);
        var candidateGroupIds = gradeGroups.Select(g => g.Id).Union(myGroupIds).ToHashSet();

        // BR-33 Step4：候选群集内学生成员（Role=Student，排除自己；同群 OR 同年级一次批量取，防逐人 N+1）
        var candidateMembers = await MembersDs.EntitySelectAsync(
            x => candidateGroupIds.Contains(x.GroupId) && x.Role == MemberRole.Student && x.UserId != meId, ct: ct);
        if (candidateMembers.Count == 0)
            return new ListBuddyCandidatesResDto { Success = true, Items = [] };

        // BR-33 Step5：排除已有搭子关系（accepted 恒排 / pending 仅未过期排；过期 pending 惰性豁免恢复候选，双向，V0.6.19 与 BR-17 同口径）
        var candidateUserIds = candidateMembers
            .Select(m => m.UserId)
            .Distinct()
            .ToArray();
        var existingPairs = await BuddiesDs.EntitySelectAsync(
            x => (x.InviterId == meId && candidateUserIds.Contains(x.InviteeId))
                 || (x.InviteeId == meId && candidateUserIds.Contains(x.InviterId))
                 || (candidateUserIds.Contains(x.InviterId) && x.InviteeId == meId),
            ct: ct);
        var excludedIds = existingPairs
            .Where(b => b.Status == BuddyStatus.Accepted
                || (b.Status == BuddyStatus.Pending && b.ExpiresAt >= DateTime.UtcNow))
            .Select(b => b.InviterId == meId ? b.InviteeId : b.InviterId)
            .ToHashSet();

        // Step6：群名映射（防 N+1——myGroups ∪ gradeGroups 已全量覆盖候选成员所在群，无需再查）
        var groupNameById = myGroups
            .Concat(gradeGroups)
            .GroupBy(g => g.Id)
            .ToDictionary(grp => grp.Key, grp => grp.First().Name);

        // Step7：多群同人去重（GROUP BY UserId；groupName 取该成员所在群，跨群候选天然携带正确来源）
        var candidateByUser = candidateMembers
            .Where(m => !excludedIds.Contains(m.UserId))
            .OrderBy(m => m.GroupId) // 稳定化 GroupBy 取首行（同人多群时取 GroupId 最小者，确定性输出）
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

    /// <summary>候选列表（同群/同年级学生成员，排除自己/已有搭子/Parent；多群去重）</summary>
    public List<BuddyCandidateItemDto> Items { get; init; } = [];
}

/// <summary>候选搭子项 DTO</summary>
public sealed record BuddyCandidateItemDto
{
    /// <summary>候选用户 Id（inviteBuddy_Execute inviteeUserId 入参）</summary>
    public long UserId { get; init; }

    /// <summary>群内昵称（GroupMembers.Nickname；空串兜底 学生{userId}）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>所属群 Id（同群/同年级候选均天然携带来源群）</summary>
    public long GroupId { get; init; }

    /// <summary>群名（Group.Name）</summary>
    public string GroupName { get; init; } = string.Empty;
}