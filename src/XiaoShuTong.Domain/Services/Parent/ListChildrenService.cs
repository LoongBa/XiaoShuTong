using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.2：我的孩子列表
/// </summary>
/// <remarks>
/// BR-03 无关联孩子空列表 | BR-04 仅当前家长的孩子（RLS）
/// 全量返回（决策：数据量小不分页）；昵称/班级依赖账户域（切片为空串）；HasSubscription 由订阅状态派生。
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

    /// <summary>
    /// 当前家长的孩子列表（含订阅状态）
    /// </summary>
    public async Task<ListChildrenResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-04：仅当前家长的孩子
        var relations = await RelationsDs.EntitySelectAsync(
            x => x.ParentId == parentId, ct: ct);

        var items = new List<ChildItemDto>();
        foreach (var relation in relations)
        {
            // 订阅状态（HasSubscription：存在非过期订阅即视为有）
            var hasSubscription = await SubscriptionsDs.EntityGetAsync(
                x => x.ParentId == parentId && x.StudentId == relation.StudentId
                     && x.Status != SubscriptionStatus.Expired, ct) != null;

            items.Add(new ChildItemDto
            {
                StudentUid = relation.StudentId.ToString(), // 账户域 Uid 未实施，透传 Id
                Nickname = string.Empty, // 账户域（跨模块），切片为空串
                ClassName = string.Empty,
                HasSubscription = hasSubscription,
            });
        }

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
    /// <summary>孩子外部键（账户域 Uid 未实施，透传 Id）</summary>
    public string StudentUid { get; init; } = string.Empty;

    /// <summary>昵称（账户域，切片为空）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>班级（账户域/群组域，切片为空）</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>是否有订阅</summary>
    public bool HasSubscription { get; init; }
}