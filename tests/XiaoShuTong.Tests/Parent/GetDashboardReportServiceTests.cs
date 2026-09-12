using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.7 成长总览（GetDashboardReportService）Contract 测试
/// 覆盖 BR：BR-15 订阅门控（前 2 项 + locked）| BR-17 试用过期 | BR-18 合规无排名 | BR-19 链式聚合
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetDashboardReportServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
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

    private async Task SeedSubscriptionAsync(long parentId, long studentId, SubscriptionStatus status, DateTime? trialEnd = null, DateTime? periodEnd = null)
    {
        var ds = User.Use<SubscriptionsDataService>();
        await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = SubscriptionPlan.Month,
            Status = status,
            TrialEndAt = trialEnd,
            PeriodEndAt = periodEnd,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedDailyAsync(long studentId, DateOnly date, int learned = 5, double? accuracy = 0.8)
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

    private async Task SeedMasteryAsync(long studentId, string subject, double accuracy)
    {
        var ds = User.Use<KnowledgeMasteryDataService>();
        await ds.EntityCreateAsync(new KnowledgeMastery
        {
            UId = UidGenerator.NewId(),
            UserId = studentId,
            Subject = subject,
            KnowledgePoint = $"KP-{Guid.NewGuid():N}"[..8],
            State = MemoryState.Fuzzy,
            Accuracy = accuracy,
            AttemptCount = 1,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-15：未订阅 → 仅前 2 项 + locked=true</summary>
    [Fact]
    public async Task ExecuteAsync_NoSubscription_PreviewOnly()
    {
        var parentId = SetUser(10701);
        await SeedRelationAsync(parentId, 17011);
        await SeedDailyAsync(17011, Today);
        var svc = User.Use<GetDashboardReportService>();

        var result = await svc.ExecuteAsync(new GetDashboardReportReqDto { StudentId = 17011 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.Locked);                       // 完整内容锁定
        Assert.NotNull(result.StreakDays);                // 前 2 项可看
        Assert.Null(result.WeekProgress);                 // 完整项为空
        Assert.Empty(result.SubjectsMastery);
    }

    /// <summary>主流程 + BR-19：已订阅 → 完整聚合（坚持天数/本周进度/学科掌握度）</summary>
    /// <remarks>
    /// 种子用"今天 + 昨天"，但**昨天跨周则跳过**（Service 窗口 = [weekStart, today]，周一运行时
    /// 昨天=上周日被排除）——既保证 StreakCalculator anchor（today/today-1）命中，又避免跨周。
    /// 断言按 hasTwoDays 动态：周一仅今天（LearnedCount=5/Streak=1），其余两天（8/2）。
    /// </remarks>
    [Fact]
    public async Task ExecuteAsync_Subscribed_FullAggregation()
    {
        var parentId = SetUser(10702);
        await SeedRelationAsync(parentId, 17021);
        await SeedSubscriptionAsync(parentId, 17021, SubscriptionStatus.Active, periodEnd: DateTime.UtcNow.AddDays(20));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7); // 本周一（周一起点）
        var hasTwoDays = today.AddDays(-1) >= weekStart;               // 昨天在本周内（非周一）
        await SeedDailyAsync(17021, today, learned: 5);
        if (hasTwoDays)
            await SeedDailyAsync(17021, today.AddDays(-1), learned: 3);
        await SeedMasteryAsync(17021, "chinese", 0.5);
        await SeedMasteryAsync(17021, "math", 0.9);
        var svc = User.Use<GetDashboardReportService>();

        var result = await svc.ExecuteAsync(new GetDashboardReportReqDto { StudentId = 17021 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(result.Locked);
        Assert.Equal(hasTwoDays ? 2 : 1, result.StreakDays);          // 连续 2 天（周一仅 1 天）
        Assert.NotNull(result.WeekProgress);
        Assert.Equal(hasTwoDays ? 8 : 5, result.WeekProgress!.LearnedCount); // 3+5（周一仅 5）
        Assert.Equal(2, result.SubjectsMastery.Count);                // 两学科
    }

    /// <summary>BR-17：试用过期 → 8003</summary>
    [Fact]
    public async Task ExecuteAsync_TrialExpired_Returns8003()
    {
        var parentId = SetUser(10703);
        await SeedRelationAsync(parentId, 17031);
        await SeedSubscriptionAsync(parentId, 17031, SubscriptionStatus.Trialing, trialEnd: DateTime.UtcNow.AddDays(-1));
        var svc = User.Use<GetDashboardReportService>();

        var result = await svc.ExecuteAsync(new GetDashboardReportReqDto { StudentId = 17031 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.TrialExpired, result.ErrorCode);
    }

    /// <summary>BR-18：响应合规——无正确率排名字段</summary>
    [Fact]
    public async Task ExecuteAsync_NoRankingFields()
    {
        var parentId = SetUser(10704);
        await SeedRelationAsync(parentId, 17041);
        await SeedSubscriptionAsync(parentId, 17041, SubscriptionStatus.Active, periodEnd: DateTime.UtcNow.AddDays(20));
        await SeedDailyAsync(17041, Today);
        var svc = User.Use<GetDashboardReportService>();

        var result = await svc.ExecuteAsync(new GetDashboardReportReqDto { StudentId = 17041 }, TestContext.Current.CancellationToken);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("rank", json, StringComparison.OrdinalIgnoreCase);
    }
}