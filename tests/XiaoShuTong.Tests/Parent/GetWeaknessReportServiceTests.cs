using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.9 薄弱知识点（GetWeaknessReportService）Contract 测试
/// 覆盖 BR：BR-25 订阅门控 8001 | BR-26 按需聚合 | BR-27 状态映射
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetWeaknessReportServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
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

    private async Task SeedSubscriptionAsync(long parentId, long studentId, SubscriptionStatus status = SubscriptionStatus.Active)
    {
        var ds = User.Use<SubscriptionsDataService>();
        await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = SubscriptionPlan.Month,
            Status = status,
            PeriodEndAt = DateTime.UtcNow.AddDays(20),
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedMasteryAsync(long studentId, string kp, double accuracy, MemoryState state)
    {
        var ds = User.Use<KnowledgeMasteryDataService>();
        await ds.EntityCreateAsync(new KnowledgeMastery
        {
            UId = UidGenerator.NewId(),
            UserId = studentId,
            Subject = "chinese",
            KnowledgePoint = kp,
            State = state,
            Accuracy = accuracy,
            AttemptCount = 1,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-27：薄弱点按正确率升序 + 状态映射</summary>
    [Fact]
    public async Task ExecuteAsync_Subscribed_ReturnsWeakPointsAsc()
    {
        var parentId = SetUser(10901);
        await SeedRelationAsync(parentId, 19011);
        await SeedSubscriptionAsync(parentId, 19011);
        await SeedMasteryAsync(19011, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedMasteryAsync(19011, "观沧海", 0.9, MemoryState.Proficient);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19011 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.WeakPoints.Count);
        Assert.Equal("岳阳楼记", result.WeakPoints[0].KnowledgePoint); // 0.3 最薄弱在前
        Assert.Equal("未掌握", result.WeakPoints[0].StateText);       // BR-27 状态映射
        Assert.Equal("熟练", result.WeakPoints[1].StateText);
    }

    /// <summary>BR-25：未订阅 → 8001</summary>
    [Fact]
    public async Task ExecuteAsync_NoSubscription_Returns8001()
    {
        var parentId = SetUser(10902);
        await SeedRelationAsync(parentId, 19021);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19021 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.SubscriptionRequired, result.ErrorCode);
    }

    /// <summary>BR-26：无掌握度数据且无作答 → 4001（按需聚合无源数据）</summary>
    [Fact]
    public async Task ExecuteAsync_NoMasteryData_Returns4001()
    {
        var parentId = SetUser(10903);
        await SeedRelationAsync(parentId, 19031);
        await SeedSubscriptionAsync(parentId, 19031);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19031 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.NoStatsData, result.ErrorCode);
    }
}