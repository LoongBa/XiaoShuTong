using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.1 关联 / UC-10.2 孩子列表 / UC-10.3 开通试用 Contract 测试
/// 覆盖 BR：BR-02 幂等 | BR-03/04 孩子列表 RLS | BR-05 授权链 8002 | BR-06 试用幂等 | BR-07 试用 7 天
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class StartTrialServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
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

    /// <summary>UC-10.1 BR-02：重复关联 → 幂等返回原记录</summary>
    [Fact]
    public async Task CreateRelation_Duplicate_Idempotent()
    {
        var parentId = SetUser(10101);
        var svc = User.Use<CreateParentRelationService>();

        var first = await svc.ExecuteAsync(new CreateParentRelationReqDto { StudentId = 10111 }, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(new CreateParentRelationReqDto { StudentId = 10111 }, TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.RelationUid, second.RelationUid); // 幂等返回

        var ds = User.Use<ParentStudentRelationsDataService>();
        var relations = await ds.EntitySelectAsync(x => x.ParentId == parentId, ct: TestContext.Current.CancellationToken);
        Assert.Single(relations);
    }

    /// <summary>UC-10.2 BR-03/04：仅当前家长孩子列表 + 无孩子空列表</summary>
    [Fact]
    public async Task ListChildren_OnlyOwnChildren()
    {
        var parentId = SetUser(10102);
        await SeedRelationAsync(parentId, 10211);
        await SeedRelationAsync(999901, 10212); // 他人家长
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.Equal("10211", item.StudentUid);
        Assert.False(item.HasSubscription);
    }

    /// <summary>主流程 + BR-07：开通试用 → Trialing + TrialEndAt=+7 天</summary>
    [Fact]
    public async Task StartTrial_ValidRelation_CreatesTrial()
    {
        var parentId = SetUser(10103);
        await SeedRelationAsync(parentId, 10311);
        var before = DateTime.UtcNow;
        var svc = User.Use<StartTrialService>();

        var result = await svc.ExecuteAsync(new StartTrialReqDto { StudentId = 10311 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Trialing", result.Status);
        Assert.InRange(result.TrialEndAt!.Value, before.AddDays(6.9), DateTime.UtcNow.AddDays(7.1)); // +7 天
    }

    /// <summary>BR-05：未授权关联 → 8002</summary>
    [Fact]
    public async Task StartTrial_NotAuthorized_Returns8002()
    {
        SetUser(10104);
        var svc = User.Use<StartTrialService>();

        var result = await svc.ExecuteAsync(new StartTrialReqDto { StudentId = 10411 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.ParentStudentNotAuthorized, result.ErrorCode);
    }

    /// <summary>BR-06：已开通幂等 → 返回原订阅</summary>
    [Fact]
    public async Task StartTrial_Duplicate_Idempotent()
    {
        var parentId = SetUser(10105);
        await SeedRelationAsync(parentId, 10511);
        var svc = User.Use<StartTrialService>();

        var first = await svc.ExecuteAsync(new StartTrialReqDto { StudentId = 10511 }, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(new StartTrialReqDto { StudentId = 10511 }, TestContext.Current.CancellationToken);

        Assert.True(second.Success);
        Assert.Equal(first.SubscriptionUid, second.SubscriptionUid); // 幂等
    }
}