using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.6：取消续费
/// </summary>
/// <remarks>
/// BR-13 订阅不存在/非本人 → 1003/1004 | BR-14 取消后权益保留至周期末（PeriodEndAt 不变）| BR-28 重复取消幂等
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class CancelSubscriptionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    /// <summary>
    /// 取消续费（置 Cancelled，权益至周期末）
    /// </summary>
    public async Task<CancelSubscriptionResDto> ExecuteAsync(CancelSubscriptionReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-13：订阅存在且为本人
        var subscription = await SubscriptionsDs.EntityGetAsync(x => x.UId == request.SubscriptionUid, ct);
        if (subscription == null)
            return new CancelSubscriptionResDto { Success = false, ErrorCode = ParentErrorCodes.NotFound };
        if (subscription.ParentId != parentId)
            return new CancelSubscriptionResDto { Success = false, ErrorCode = ParentErrorCodes.Forbidden };

        // BR-28：已取消幂等
        if (subscription.Status == SubscriptionStatus.Cancelled)
            return new CancelSubscriptionResDto { Success = true };

        // BR-14：置 Cancelled（PeriodEndAt 不变，权益保留至周期末）
        subscription.Status = SubscriptionStatus.Cancelled;
        await SubscriptionsDs.EntityUpdateAsync(subscription, ct);

        return new CancelSubscriptionResDto { Success = true };
    }
}

/// <summary>取消续费请求 DTO</summary>
public sealed record CancelSubscriptionReqDto
{
    /// <summary>订阅外部键</summary>
    public string SubscriptionUid { get; init; } = string.Empty;
}

/// <summary>取消续费响应 DTO</summary>
public sealed record CancelSubscriptionResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }
}