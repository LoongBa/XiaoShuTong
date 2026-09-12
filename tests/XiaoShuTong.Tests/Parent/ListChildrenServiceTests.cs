using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.2 我的孩子列表（ListChildrenService）Contract 测试
/// 覆盖 BR：BR-03 无关联孩子空列表 | BR-04 仅当前家长的孩子（RLS）+ HasSubscription 派生
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListChildrenServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedRelationAsync(long parentId, long studentId)
    {
        var ds = User.Use<ParentStudentRelationsDataService>();
        await ds.EntityCreateAsync(new ParentStudentRelations
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Relation = ParentRelation.Parent,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedSubscriptionAsync(long parentId, long studentId, SubscriptionStatus status)
    {
        var ds = User.Use<SubscriptionsDataService>();
        await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = SubscriptionPlan.Month,
            Status = status,
            TrialEndAt = null,
            PeriodEndAt = DateTime.UtcNow.AddDays(30),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-03：无关联孩子 → 空列表 + Success</summary>
    [Fact]
    public async Task Execute_NoChildren_ReturnsEmpty()
    {
        SetUser(48401);
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-04：仅返回当前家长的孩子（RLS），他人孩子不可见</summary>
    [Fact]
    public async Task Execute_OnlyOwnChildren()
    {
        var parentId = SetUser(48402);
        await SeedRelationAsync(parentId, 48511);
        await SeedRelationAsync(489903, 48512); // 他人家长的孩子
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.Equal("48511", item.StudentUid);
        Assert.False(item.HasSubscription); // 无订阅
    }

    /// <summary>HasSubscription 派生：存在非过期订阅 → true</summary>
    [Fact]
    public async Task Execute_HasSubscription_TrueWhenActive()
    {
        var parentId = SetUser(48403);
        await SeedRelationAsync(parentId, 48521);
        await SeedSubscriptionAsync(parentId, 48521, SubscriptionStatus.Active);
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.True(item.HasSubscription);
    }

    /// <summary>HasSubscription 派生：仅过期订阅 → false（Expired 不计入）</summary>
    [Fact]
    public async Task Execute_HasSubscription_FalseWhenExpired()
    {
        var parentId = SetUser(48404);
        await SeedRelationAsync(parentId, 48531);
        await SeedSubscriptionAsync(parentId, 48531, SubscriptionStatus.Expired);
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.False(item.HasSubscription);
    }
}
