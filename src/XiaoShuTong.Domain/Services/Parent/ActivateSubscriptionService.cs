using TKW.Framework.Domain;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.4：订阅（支付成功回调，Callee）
/// </summary>
/// <remarks>
/// CROSS：订阅激活 + 状态迁移。
/// BR-08 授权链校验 → 8002 | BR-09 重复回调幂等 | BR-10 Plan=Month +1 月 / Year +1 年
/// 微信支付回调签名验证对接（切片验证：直接调用）。
/// </remarks>
[Transactional]
internal class ActivateSubscriptionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    /// <summary>
    /// 支付回调激活订阅（置 Active + PeriodEndAt 推进）
    /// </summary>
    public async Task<ActivateSubscriptionResDto> ExecuteAsync(ActivateSubscriptionReqDto request, CancellationToken ct = default)
    {
        // BR-08：授权链校验 → 8002
        var relation = await RelationsDs.EntityGetAsync(
            x => x.ParentId == request.ParentId && x.StudentId == request.StudentId, ct);
        if (relation == null)
            return new ActivateSubscriptionResDto { Success = false, ErrorCode = ParentErrorCodes.ParentStudentNotAuthorized };

        var plan = Enum.TryParse<SubscriptionPlan>(request.Plan, true, out var parsed) ? parsed : SubscriptionPlan.Month;

        var subscription = await SubscriptionsDs.EntityGetAsync(
            x => x.ParentId == request.ParentId && x.StudentId == request.StudentId, ct);

        // 无订阅记录 → 创建（直接开通场景）
        if (subscription == null)
        {
            subscription = await SubscriptionsDs.EntityCreateAsync(new Subscriptions
            {
                UId = XiaoShuTong.Tools.UidGenerator.NewId(),
                ParentId = request.ParentId,
                StudentId = request.StudentId,
                Plan = plan,
                Status = SubscriptionStatus.Active,
                PeriodEndAt = AdvancePeriod(DateTime.UtcNow, plan),
            }, ct);
            return new ActivateSubscriptionResDto { Success = true, SubscriptionUid = subscription.UId };
        }

        // BR-09：重复回调幂等——已 Active 且周期未过期 → 不重复推进
        if (subscription.Status == SubscriptionStatus.Active
            && subscription.PeriodEndAt is { } end && end > DateTime.UtcNow)
            return new ActivateSubscriptionResDto { Success = true, SubscriptionUid = subscription.UId };

        // BR-10：置 Active + PeriodEndAt 推进（月 +1 月 / 年 +1 年）
        subscription.Status = SubscriptionStatus.Active;
        var baseTime = subscription.PeriodEndAt is { } existingEnd && existingEnd > DateTime.UtcNow
            ? existingEnd : DateTime.UtcNow;
        subscription.PeriodEndAt = AdvancePeriod(baseTime, plan);
        await SubscriptionsDs.EntityUpdateAsync(subscription, ct);

        return new ActivateSubscriptionResDto { Success = true, SubscriptionUid = subscription.UId };
    }

    private static DateTime AdvancePeriod(DateTime from, SubscriptionPlan plan)
        => plan == SubscriptionPlan.Month ? from.AddMonths(1) : from.AddYears(1);
}

/// <summary>订阅激活请求 DTO（支付回调）</summary>
public sealed record ActivateSubscriptionReqDto
{
    /// <summary>家长 Id</summary>
    public long ParentId { get; init; }

    /// <summary>孩子 Id</summary>
    public long StudentId { get; init; }

    /// <summary>方案（Month/Year）</summary>
    public string Plan { get; init; } = string.Empty;
}

/// <summary>订阅激活响应 DTO</summary>
public sealed record ActivateSubscriptionResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>订阅外部键</summary>
    public string SubscriptionUid { get; init; } = string.Empty;
}