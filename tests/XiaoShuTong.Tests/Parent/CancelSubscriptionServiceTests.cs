using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.6 取消续费（CancelSubscriptionService）Contract 测试
/// 覆盖 BR：BR-13 订阅不存在/非本人 → NOT_FOUND/FORBIDDEN | BR-14 取消后权益保留至周期末 | BR-28 重复取消幂等
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CancelSubscriptionServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Subscriptions> SeedSubscriptionAsync(long parentId, long studentId, SubscriptionStatus status, DateTime? periodEndAt = null)
    {
        var ds = User.Use<SubscriptionsDataService>();
        return await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = SubscriptionPlan.Month,
            Status = status,
            TrialEndAt = null,
            PeriodEndAt = periodEndAt ?? DateTime.UtcNow.AddDays(30),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-13/BR-14：本人 Active 订阅取消 → Cancelled，PeriodEndAt 不变（权益至周期末）</summary>
    [Fact]
    public async Task Cancel_OwnActiveSubscription_CancelsWithPeriodRetained()
    {
        var parentId = SetUser(48001);
        var periodEndAt = DateTime.UtcNow.AddDays(30);
        var sub = await SeedSubscriptionAsync(parentId, 48111, SubscriptionStatus.Active, periodEndAt);
        var svc = User.Use<CancelSubscriptionService>();

        var result = await svc.ExecuteAsync(new CancelSubscriptionReqDto { SubscriptionUid = sub.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var ds = User.Use<SubscriptionsDataService>();
        var after = await ds.EntityGetAsync(x => x.UId == sub.UId, TestContext.Current.CancellationToken);
        Assert.NotNull(after);
        Assert.Equal(SubscriptionStatus.Cancelled, after.Status); // BR-14：置 Cancelled
        Assert.Equal(periodEndAt, after.PeriodEndAt); // BR-14：PeriodEndAt 不变
    }

    /// <summary>BR-13：订阅不存在 → NOT_FOUND</summary>
    [Fact]
    public async Task Cancel_UnknownSubscription_ReturnsNotFound()
    {
        SetUser(48002);
        var svc = User.Use<CancelSubscriptionService>();

        var result = await svc.ExecuteAsync(new CancelSubscriptionReqDto { SubscriptionUid = "no-such-sub" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.NotFound, result.ErrorCode);
    }

    /// <summary>BR-13：他人订阅 → FORBIDDEN</summary>
    [Fact]
    public async Task Cancel_OthersSubscription_ReturnsForbidden()
    {
        SetUser(48003);
        var sub = await SeedSubscriptionAsync(489901, 48121, SubscriptionStatus.Active); // 他人家长订阅
        var svc = User.Use<CancelSubscriptionService>();

        var result = await svc.ExecuteAsync(new CancelSubscriptionReqDto { SubscriptionUid = sub.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-28：重复取消（已 Cancelled）→ 幂等返回 Success，状态不变</summary>
    [Fact]
    public async Task Cancel_AlreadyCancelled_Idempotent()
    {
        var parentId = SetUser(48004);
        var sub = await SeedSubscriptionAsync(parentId, 48131, SubscriptionStatus.Cancelled);
        var svc = User.Use<CancelSubscriptionService>();

        var result = await svc.ExecuteAsync(new CancelSubscriptionReqDto { SubscriptionUid = sub.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var ds = User.Use<SubscriptionsDataService>();
        var after = await ds.EntityGetAsync(x => x.UId == sub.UId, TestContext.Current.CancellationToken);
        Assert.NotNull(after);
        Assert.Equal(SubscriptionStatus.Cancelled, after.Status); // 保持 Cancelled
    }
}
