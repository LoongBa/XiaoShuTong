using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.5 我的订阅列表（ListSubscriptionsService）Contract 测试
/// 覆盖 BR：BR-11 无订阅空列表 | BR-12 仅当前家长（RLS）+ Plan/Status/TrialEndAt 透传
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListSubscriptionsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Subscriptions> SeedSubscriptionAsync(long parentId, long studentId, SubscriptionPlan plan, SubscriptionStatus status, DateTime? trialEndAt = null)
    {
        var ds = User.Use<SubscriptionsDataService>();
        return await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = plan,
            Status = status,
            TrialEndAt = trialEndAt,
            PeriodEndAt = status == SubscriptionStatus.Trialing ? null : DateTime.UtcNow.AddDays(30),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-11：无订阅 → 空列表 + Success</summary>
    [Fact]
    public async Task Execute_NoSubscriptions_ReturnsEmpty()
    {
        SetUser(48601);
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-12：仅当前家长的订阅（RLS）+ Plan/Status 透传</summary>
    [Fact]
    public async Task Execute_OnlyOwnSubscriptions()
    {
        var parentId = SetUser(48602);
        var own = await SeedSubscriptionAsync(parentId, 48711, SubscriptionPlan.Month, SubscriptionStatus.Active);
        await SeedSubscriptionAsync(489904, 48712, SubscriptionPlan.Year, SubscriptionStatus.Active); // 他人家长订阅
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(own.UId, item.SubscriptionUid);
        Assert.Equal("48711", item.StudentUid);
        Assert.Equal("Month", item.Plan);
        Assert.Equal("Active", item.Status);
    }

    /// <summary>试用中订阅透传：Trialing + TrialEndAt</summary>
    [Fact]
    public async Task Execute_TrialSubscription_PassesThroughTrialEndAt()
    {
        var parentId = SetUser(48603);
        var trialEnd = DateTime.UtcNow.AddDays(7);
        await SeedSubscriptionAsync(parentId, 48721, SubscriptionPlan.Month, SubscriptionStatus.Trialing, trialEnd);
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("Trialing", item.Status);
        Assert.Equal(trialEnd, item.TrialEndAt);
    }
}
