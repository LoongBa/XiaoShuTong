using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Parent;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.2：我的孩子列表
/// </summary>
/// <remarks>
/// BR-03 无关联孩子空列表 | BR-04 仅当前家长的孩子（RLS）
/// 全量返回（决策：数据量小不分页）；昵称 = GroupMembers.Nickname（Role=Student 过滤，null 兜底 学生{userId}）；班级 = Groups.Name（无群空串）；HasSubscription 由订阅状态派生。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListChildrenService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 当前家长的孩子列表（含订阅状态）
    /// </summary>
    public async Task<ListChildrenResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-04：仅当前家长的孩子
        var relations = await RelationsDs.EntitySelectAsync(
            x => x.ParentId == parentId, ct: ct);

        // 一次 IN 查询替代 foreach N+1：一次取全部非过期订阅，内存判定
        var studentIds = relations.Select(r => r.StudentId).ToArray();
        var subscriptions = studentIds.Length == 0
            ? new List<Subscriptions>()
            : await SubscriptionsDs.EntitySelectAsync(
                x => x.ParentId == parentId
                     && x.Status != SubscriptionStatus.Expired
                     && studentIds.Contains(x.StudentId), ct: ct);
        var subscribedStudentIds = subscriptions.Select(s => s.StudentId).ToHashSet();

        // 昵称富化（GroupMembers.Nickname，Role=Student 过滤防 Role 歧义；一次 IN 查询防 N+1）
        var memberRows = studentIds.Length == 0
            ? new List<GroupMembers>()
            : await MembersDs.EntitySelectAsync(
                x => studentIds.Contains(x.UserId) && x.Role == MemberRole.Student, ct: ct);
        var nicknameByUser = memberRows
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.First().Nickname);
        var groupIdByUser = memberRows
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.First().GroupId);

        // 班级富化（Groups.Name，一次 IN 查询防 N+1）
        var groupIds = groupIdByUser.Values.Distinct().ToArray();
        var groupRows = groupIds.Length == 0
            ? new List<Groups>()
            : await GroupsDs.EntitySelectAsync(x => groupIds.Contains(x.Id), ct: ct);
        var nameByGroup = groupRows.ToDictionary(g => g.Id, g => g.Name);

        var items = relations.Select(relation => new ChildItemDto
        {
            StudentId = relation.StudentId, // 数值主键直通（dashboardReport.studentId 同源，V0.6.7 登记配合项）
            StudentUid = relation.StudentId.ToString(), // 账户域 Uid 未实施，透传 Id
            Nickname = nicknameByUser.GetValueOrDefault(relation.StudentId) ?? $"学生{relation.StudentId}",
            ClassName = groupIdByUser.TryGetValue(relation.StudentId, out var gid)
                ? nameByGroup.GetValueOrDefault(gid) ?? string.Empty
                : string.Empty,
            HasSubscription = subscribedStudentIds.Contains(relation.StudentId),
        }).ToList();

        // BR-03：无孩子空列表
        return new ListChildrenResDto { Success = true, Items = items };
    }
}

/// <summary>我的孩子列表响应 DTO</summary>
public sealed record ListChildrenResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>孩子列表</summary>
    public List<ChildItemDto> Items { get; init; } = [];
}

/// <summary>孩子项 DTO</summary>
public sealed record ChildItemDto
{
    /// <summary>孩子数值主键（DB Id，与 dashboardReport.studentId 同源直通）</summary>
    public long StudentId { get; init; }

    /// <summary>孩子外部键（账户域 Uid 未实施，透传 Id）</summary>
    public string StudentUid { get; init; } = string.Empty;

    /// <summary>昵称（GroupMembers.Nickname；null 兜底 学生{userId}）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>班级（Groups.Name；无群空串）</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>是否有订阅</summary>
    public bool HasSubscription { get; init; }
}