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
/// UC-10.8 进度趋势（GetProgressReportService）Contract 测试
/// 覆盖 BR：BR-20 订阅门控 8001 | BR-21 Period 校验 | BR-22 无数据 4001 | BR-23 合规无排名 | BR-24 vsLastWeek
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetProgressReportServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

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

    private async Task SeedDailyAsync(long studentId, DateOnly date, int learned, double accuracy)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = UidGenerator.NewId(),
            UserId = studentId,
            StatDate = date,
            LearnedCount = learned,
            StarredCount = 1,
            Accuracy = accuracy,
            StudySeconds = 300,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-24：本周 5 上周 2 → learnedDelta=3（仅与自己比）</summary>
    [Fact]
    public async Task ExecuteAsync_WeekPeriod_ComputesVsLastWeek()
    {
        var parentId = SetUser(10801);
        await SeedRelationAsync(parentId, 18011);
        await SeedSubscriptionAsync(parentId, 18011);
        await SeedDailyAsync(18011, Today, learned: 3, accuracy: 0.8);
        await SeedDailyAsync(18011, Today.AddDays(-1), learned: 2, accuracy: 0.9);  // 本周
        await SeedDailyAsync(18011, Today.AddDays(-7), learned: 2, accuracy: 0.7);  // 上周
        var svc = User.Use<GetProgressReportService>();

        var result = await svc.ExecuteAsync(new GetProgressReportReqDto { StudentId = 18011, Period = "Week" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Trend.Count); // -7 天在周窗口外，仅上周对比计入
        Assert.Equal(3, result.VsLastWeek!.LearnedDelta); // 本周 5 − 上周 2
    }

    /// <summary>BR-20：未订阅 → 8001</summary>
    [Fact]
    public async Task ExecuteAsync_NoSubscription_Returns8001()
    {
        var parentId = SetUser(10802);
        await SeedRelationAsync(parentId, 18021);
        var svc = User.Use<GetProgressReportService>();

        var result = await svc.ExecuteAsync(new GetProgressReportReqDto { StudentId = 18021, Period = "Week" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.SubscriptionRequired, result.ErrorCode);
    }

    /// <summary>BR-21：非法周期 → 1002</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPeriod_Returns1002()
    {
        var parentId = SetUser(10803);
        await SeedRelationAsync(parentId, 18031);
        await SeedSubscriptionAsync(parentId, 18031);
        var svc = User.Use<GetProgressReportService>();

        var result = await svc.ExecuteAsync(new GetProgressReportReqDto { StudentId = 18031, Period = "Year" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-22：无周期数据 → 4001</summary>
    [Fact]
    public async Task ExecuteAsync_NoData_Returns4001()
    {
        var parentId = SetUser(10804);
        await SeedRelationAsync(parentId, 18041);
        await SeedSubscriptionAsync(parentId, 18041);
        var svc = User.Use<GetProgressReportService>();

        var result = await svc.ExecuteAsync(new GetProgressReportReqDto { StudentId = 18041, Period = "Week" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.NoStatsData, result.ErrorCode);
    }

    /// <summary>BR-23：合规——无群组正确率排名</summary>
    [Fact]
    public async Task ExecuteAsync_NoRankingFields()
    {
        var parentId = SetUser(10805);
        await SeedRelationAsync(parentId, 18051);
        await SeedSubscriptionAsync(parentId, 18051);
        await SeedDailyAsync(18051, Today, learned: 2, accuracy: 0.8);
        var svc = User.Use<GetProgressReportService>();

        var result = await svc.ExecuteAsync(new GetProgressReportReqDto { StudentId = 18051, Period = "Week" }, TestContext.Current.CancellationToken);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("rank", json, StringComparison.OrdinalIgnoreCase);
    }
}