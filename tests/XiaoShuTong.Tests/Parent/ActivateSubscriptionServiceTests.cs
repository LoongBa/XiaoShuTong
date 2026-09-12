using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.4 订阅回调 / UC-10.5 订阅列表 / UC-10.6 取消续费 Contract 测试
/// 覆盖 BR：BR-08 授权 | BR-09 回调幂等 | BR-10 周期推进 | BR-11/12 列表 RLS | BR-13 非本人 | BR-14 权益保留
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ActivateSubscriptionServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
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

    /// <summary>主流程 + BR-10：Month 订阅激活 → PeriodEndAt +1 月</summary>
    [Fact]
    public async Task Activate_MonthPlan_AdvancesOneMonth()
    {
        await SeedRelationAsync(20101, 20111);
        var svc = User.Use<ActivateSubscriptionService>();

        var result = await svc.ExecuteAsync(new ActivateSubscriptionReqDto
        {
            ParentId = 20101,
            StudentId = 20111,
            Plan = "Month",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);

        var ds = User.Use<SubscriptionsDataService>();
        var sub = await ds.EntityGetAsync(x => x.UId == result.SubscriptionUid, TestContext.Current.CancellationToken);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.InRange(sub.PeriodEndAt!.Value, DateTime.UtcNow.AddDays(28), DateTime.UtcNow.AddDays(33)); // +1 月
    }

    /// <summary>BR-08：未授权 → 8002</summary>
    [Fact]
    public async Task Activate_NotAuthorized_Returns8002()
    {
        var svc = User.Use<ActivateSubscriptionService>();

        var result = await svc.ExecuteAsync(new ActivateSubscriptionReqDto
        {
            ParentId = 20102,
            StudentId = 20211, // 未授权
            Plan = "Year",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.ParentStudentNotAuthorized, result.ErrorCode);
    }

    /// <summary>BR-09：重复回调幂等（不重复推进周期）</summary>
    [Fact]
    public async Task Activate_DuplicateCallback_Idempotent()
    {
        await SeedRelationAsync(20103, 20311);
        var svc = User.Use<ActivateSubscriptionService>();
        var request = new ActivateSubscriptionReqDto { ParentId = 20103, StudentId = 20311, Plan = "Month" };

        var first = await svc.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.True(second.Success);
        Assert.Equal(first.SubscriptionUid, second.SubscriptionUid);
    }

    /// <summary>UC-10.6 BR-13/14：取消续费 → Cancelled + PeriodEndAt 不变；他人订阅 → 1004</summary>
    [Fact]
    public async Task Cancel_OwnSubscription_CancelsWithPeriodRetained()
    {
        SetUser(20105);
        await SeedRelationAsync(20105, 20511);
        // 先激活
        var activate = User.Use<ActivateSubscriptionService>();
        var activated = await activate.ExecuteAsync(new ActivateSubscriptionReqDto { ParentId = 20105, StudentId = 20511, Plan = "Month" }, TestContext.Current.CancellationToken);

        var ds = User.Use<SubscriptionsDataService>();
        var before = await ds.EntityGetAsync(x => x.UId == activated.SubscriptionUid, TestContext.Current.CancellationToken);
        Assert.NotNull(before);

        var svc = User.Use<CancelSubscriptionService>();
        var result = await svc.ExecuteAsync(new CancelSubscriptionReqDto { SubscriptionUid = activated.SubscriptionUid }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var after = await ds.EntityGetAsync(x => x.UId == activated.SubscriptionUid, TestContext.Current.CancellationToken);
        Assert.NotNull(after);
        Assert.Equal(SubscriptionStatus.Cancelled, after.Status);
        Assert.Equal(before.PeriodEndAt, after.PeriodEndAt); // BR-14：权益保留至周期末
    }

    /// <summary>BR-13：他人订阅 → 1004</summary>
    [Fact]
    public async Task Cancel_OthersSubscription_ReturnsForbidden()
    {
        SetUser(20106);
        await SeedRelationAsync(999901, 20611);
        var activate = User.Use<ActivateSubscriptionService>();
        var activated = await activate.ExecuteAsync(new ActivateSubscriptionReqDto { ParentId = 999901, StudentId = 20611, Plan = "Month" }, TestContext.Current.CancellationToken);
        var svc = User.Use<CancelSubscriptionService>();

        var result = await svc.ExecuteAsync(new CancelSubscriptionReqDto { SubscriptionUid = activated.SubscriptionUid }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>UC-10.5 BR-11/12：订阅列表仅当前家长</summary>
    [Fact]
    public async Task ListSubscriptions_OnlyOwn()
    {
        SetUser(20107);
        await SeedRelationAsync(20107, 20711);
        await SeedRelationAsync(999902, 20712);
        var activate = User.Use<ActivateSubscriptionService>();
        await activate.ExecuteAsync(new ActivateSubscriptionReqDto { ParentId = 20107, StudentId = 20711, Plan = "Month" }, TestContext.Current.CancellationToken);
        await activate.ExecuteAsync(new ActivateSubscriptionReqDto { ParentId = 999902, StudentId = 20712, Plan = "Month" }, TestContext.Current.CancellationToken);
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("20711", item.StudentUid); // 仅当前家长订阅
    }
}