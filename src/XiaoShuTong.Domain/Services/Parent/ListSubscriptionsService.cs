using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Parent;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.5：我的订阅列表
/// </summary>
/// <remarks>
/// BR-11 无订阅空列表 | BR-12 仅当前家长（RLS）；全量返回（决策）。
/// 孩子昵称 = GroupMembers.Nickname（Role=Student 过滤，null 兜底 学生{userId}）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListSubscriptionsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    /// <summary>
    /// 当前家长的各孩子订阅列表
    /// </summary>
    public async Task<ListSubscriptionsResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-12：仅当前家长
        var subscriptions = await SubscriptionsDs.EntitySelectAsync(
            x => x.ParentId == parentId, ct: ct);

        // 孩子昵称富化（GroupMembers.Nickname，Role=Student 过滤防 Role 歧义；一次 IN 查询防 N+1）
        var studentIds = subscriptions.Select(s => s.StudentId).Distinct().ToArray();
        var memberRows = studentIds.Length == 0
            ? new List<GroupMembers>()
            : await MembersDs.EntitySelectAsync(
                x => studentIds.Contains(x.UserId) && x.Role == MemberRole.Student, ct: ct);
        var nicknameByUser = memberRows
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.First().Nickname);

        // BR-11：无订阅空列表
        return new ListSubscriptionsResDto
        {
            Success = true,
            Items = subscriptions.Select(s => new SubscriptionItemDto
            {
                SubscriptionUid = s.UId,
                StudentUid = s.StudentId.ToString(), // 账户域 Uid 未实施，透传 Id
                StudentNickname = nicknameByUser.GetValueOrDefault(s.StudentId) ?? $"学生{s.StudentId}",
                Plan = s.Plan.ToString(),
                Status = s.Status.ToString(),
                TrialEndAt = s.TrialEndAt,
                PeriodEndAt = s.PeriodEndAt,
            }).ToList(),
        };
    }
}

/// <summary>我的订阅列表响应 DTO</summary>
public sealed record ListSubscriptionsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>订阅列表</summary>
    public List<SubscriptionItemDto> Items { get; init; } = [];
}

/// <summary>订阅项 DTO</summary>
public sealed record SubscriptionItemDto
{
    /// <summary>订阅外部键</summary>
    public string SubscriptionUid { get; init; } = string.Empty;

    /// <summary>孩子外部键</summary>
    public string StudentUid { get; init; } = string.Empty;

    /// <summary>孩子昵称（GroupMembers.Nickname；null 兜底 学生{userId}）</summary>
    public string StudentNickname { get; init; } = string.Empty;

    /// <summary>方案（Month/Year）</summary>
    public string Plan { get; init; } = string.Empty;

    /// <summary>状态（Trialing/Active/Expired/Cancelled）</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>试用结束</summary>
    public DateTime? TrialEndAt { get; init; }

    /// <summary>当前计费周期结束</summary>
    public DateTime? PeriodEndAt { get; init; }
}