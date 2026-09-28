using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// UC-8.1：发起搭子邀请
/// </summary>
/// <remarks>
/// BR-14 accepted 搭子数 ≥5 → 6003 | BR-15 当日邀请 >10 → 6005 | BR-16 同群组/同年级（OR）→ 6006
/// BR-17 重复邀请（accepted 恒阻塞 / pending 仅未过期阻塞；过期 pending 豁免重邀）→ 6002 | BR-18 ExpiresAt = +7 天
/// 同年级经 GroupMembers → Groups.Grade 交集推导（已决策）；账户域 Uid→Id 映射未实施，InviteeUserId 直接为 long Id。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class InviteBuddyService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int MaxBuddies = 5;
    private const int MaxDailyInvites = 10;

    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 发起搭子邀请（Pending，ExpiresAt+7 天）
    /// </summary>
    public async Task<InviteBuddyResDto> ExecuteAsync(InviteBuddyReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        if (request.InviteeUserId <= 0 || request.InviteeUserId == userId)
            return new InviteBuddyResDto { Success = false, ErrorCode = BuddyErrorCodes.ParamInvalid };

        var now = DateTime.UtcNow;

        // BR-14：accepted 搭子数 ≥5 → 6003（仅计 accepted，按用户全局）
        var acceptedCount = await BuddiesDs.CountAsync(
            x => (x.InviterId == userId || x.InviteeId == userId) && x.Status == BuddyStatus.Accepted, ct);
        if (acceptedCount >= MaxBuddies)
            return new InviteBuddyResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyLimitReached };

        // BR-15：当日邀请 >10 → 6005（Redis 计数桩：按当日 InvitedAt 统计）
        var todayStart = now.AddHours(8).Date.AddHours(-8);
        var todayInvites = await BuddiesDs.CountAsync(
            x => x.InviterId == userId && x.InvitedAt >= todayStart, ct);
        if (todayInvites >= MaxDailyInvites)
            return new InviteBuddyResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteDailyLimit };

        // BR-16：同群组/同年级（OR 语义）→ 6006
        if (!await IsSameGroupOrGradeAsync(userId, request.InviteeUserId, ct))
            return new InviteBuddyResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyGroupGradeRequired };

        // BR-17：重复邀请（accepted 恒阻塞 / pending 仅未过期阻塞；过期 pending 惰性豁免）→ 6002
        var existing = await BuddiesDs.EntitySelectAsync(
            x => (x.InviterId == userId && x.InviteeId == request.InviteeUserId)
                 || (x.InviterId == request.InviteeUserId && x.InviteeId == userId), ct: ct);
        var hasBlocking = existing.Any(b => b.Status == BuddyStatus.Accepted
            || (b.Status == BuddyStatus.Pending && b.ExpiresAt >= now));
        if (hasBlocking)
            return new InviteBuddyResDto { Success = false, ErrorCode = BuddyErrorCodes.BuddyInviteExpired };

        // V0.6.19 惰性过期豁免 + V0.7.0 解除后重邀豁免：同方向过期 Pending 或 Removed 复用复活（不再 6002 阻塞重邀）
        // StudyBuddies 有 UNIQUE(InviterId, InviteeId) 约束，同方向仅一条记录——重邀 = 刷新时间戳复用，不新建
        // Pending 到此处必为过期（未过期已被 hasBlocking 拦截）；Removed 为解除后状态（BR-31 历史保留，重邀复用复活不新建避免 UNIQUE 冲突）
        var staleSameDirection = existing.FirstOrDefault(
            b => b.InviterId == userId && b.InviteeId == request.InviteeUserId
                 && b.Status is BuddyStatus.Pending or BuddyStatus.Removed);
        if (staleSameDirection != null)
        {
            staleSameDirection.InvitedAt = now;
            staleSameDirection.ExpiresAt = now.AddDays(7);
            staleSameDirection.Status = BuddyStatus.Pending;
            await BuddiesDs.EntityUpdateAsync(staleSameDirection, ct);
            return new InviteBuddyResDto { Success = true, InviteId = staleSameDirection.UId, ExpiresAt = staleSameDirection.ExpiresAt };
        }

        // BR-18：创建邀请（Pending，ExpiresAt = +7 天）
        var buddy = await BuddiesDs.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = userId,
            InviteeId = request.InviteeUserId,
            Status = BuddyStatus.Pending,
            InvitedAt = now,
            ExpiresAt = now.AddDays(7),
        }, ct);

        return new InviteBuddyResDto { Success = true, InviteId = buddy.UId, ExpiresAt = buddy.ExpiresAt };
    }

    /// <summary>BR-16：同群组 或 同年级（OR）</summary>
    private async Task<bool> IsSameGroupOrGradeAsync(long meId, long otherId, CancellationToken ct)
    {
        var myMembers = await MembersDs.EntitySelectAsync(x => x.UserId == meId, ct: ct);
        var otherMembers = await MembersDs.EntitySelectAsync(x => x.UserId == otherId, ct: ct);

        // 同群组：GroupId 交集
        var myGroupIds = myMembers.Select(m => m.GroupId).ToHashSet();
        var otherGroupIds = otherMembers.Select(m => m.GroupId).ToHashSet();
        if (myGroupIds.Overlaps(otherGroupIds))
            return true;

        // 同年级：Groups.Grade 交集（经群组成员关系推导）
        var myGrades = (await LoadGradesAsync(myGroupIds, ct)).ToHashSet();
        var otherGrades = (await LoadGradesAsync(otherGroupIds, ct)).ToHashSet();
        return myGrades.Overlaps(otherGrades);
    }

    private async Task<List<string?>> LoadGradesAsync(IEnumerable<long> groupIds, CancellationToken ct)
    {
        var grades = new List<string?>();
        foreach (var groupId in groupIds)
        {
            var group = await GroupsDs.EntityGetAsync(x => x.Id == groupId, ct);
            if (group != null)
                grades.Add(group.Grade);
        }
        return grades;
    }
}

/// <summary>发起搭子邀请请求 DTO</summary>
public sealed record InviteBuddyReqDto
{
    /// <summary>被邀请人 Id（账户域 Uid→Id 映射未实施，直接 long Id）</summary>
    public long InviteeUserId { get; init; }
}

/// <summary>发起搭子邀请响应 DTO</summary>
public sealed record InviteBuddyResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>邀请记录 Uid</summary>
    public string InviteId { get; init; } = string.Empty;

    /// <summary>过期时间（+7 天）</summary>
    public DateTime ExpiresAt { get; init; }
}