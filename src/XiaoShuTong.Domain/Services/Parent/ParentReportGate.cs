using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// 家长报告订阅门控（BR-15/17/20/25 共享校验）
/// </summary>
internal static class ParentReportGate
{
    /// <summary>
    /// 校验家长-孩子报告访问权益
    /// </summary>
    /// <param name="allowPreview">true = 无订阅返回预览模式（dashboard）；false = 无订阅直接拒绝</param>
    /// <returns>Allowed=false 时 ErrorCode 为 8002/8003/8001</returns>
    public static async Task<(bool Allowed, string? ErrorCode, bool Locked, Subscriptions? Subscription)> CheckAsync(
        ParentStudentRelationsDataService relationsDs,
        SubscriptionsDataService subscriptionsDs,
        long parentId, long studentId, bool allowPreview, CancellationToken ct)
    {
        // 授权链（8002）
        var relation = await relationsDs.EntityGetAsync(
            x => x.ParentId == parentId && x.StudentId == studentId, ct);
        if (relation == null)
            return (false, ParentErrorCodes.ParentStudentNotAuthorized, true, null);

        var subscription = await subscriptionsDs.EntityGetAsync(
            x => x.ParentId == parentId && x.StudentId == studentId, ct);
        var now = DateTime.UtcNow;

        // 无订阅记录：dashboard → 预览模式（locked）；progress/weakness → 8001
        if (subscription == null)
            return allowPreview
                ? (true, null, true, null)
                : (false, ParentErrorCodes.SubscriptionRequired, true, null);

        // 试用过期 → 8003
        if (subscription.Status == SubscriptionStatus.Trialing
            && subscription.TrialEndAt is { } trialEnd && trialEnd < now)
            return (false, ParentErrorCodes.TrialExpired, true, subscription);

        // 已过期 → 8001
        if (subscription.Status == SubscriptionStatus.Expired)
            return allowPreview
                ? (true, null, true, subscription)
                : (false, ParentErrorCodes.SubscriptionRequired, true, subscription);

        // 已取消且周期已过 → 8001
        if (subscription.Status == SubscriptionStatus.Cancelled
            && subscription.PeriodEndAt is { } periodEnd && periodEnd < now)
            return allowPreview
                ? (true, null, true, subscription)
                : (false, ParentErrorCodes.SubscriptionRequired, true, subscription);

        // 有效权益（Trialing 有效 / Active / Cancelled 周期内）
        return (true, null, false, subscription);
    }
}
