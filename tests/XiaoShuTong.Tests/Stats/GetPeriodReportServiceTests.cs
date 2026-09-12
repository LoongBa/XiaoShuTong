using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Stats;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Stats;

/// <summary>
/// UC-6.3 查看学习报告（GetPeriodReportService）Contract 测试
/// 覆盖 BR：BR-08 无数据占位 | BR-09 Period 校验 | BR-10 合规无排名 | BR-11 个人正确率
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetPeriodReportServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedDailyAsync(long userId, DateOnly date, int learned, int starred, double? accuracy)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            StatDate = date,
            LearnedCount = learned,
            StarredCount = starred,
            Accuracy = accuracy,
            StudySeconds = 300,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedMasteryAsync(long userId, string kp, double accuracy)
    {
        var ds = User.Use<KnowledgeMasteryDataService>();
        await ds.EntityCreateAsync(new KnowledgeMastery
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Subject = "chinese",
            KnowledgePoint = kp,
            State = MemoryState.Fuzzy,
            Accuracy = accuracy,
            AttemptCount = 1,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-11：周期聚合（学习量/★/个人正确率均值）+ 薄弱点升序</summary>
    [Fact]
    public async Task ExecuteAsync_WeekPeriod_AggregatesStats()
    {
        var userId = SetUser(63001);
        await SeedDailyAsync(userId, Today.AddDays(-1), learned: 10, starred: 2, accuracy: 0.8);
        await SeedDailyAsync(userId, Today, learned: 5, starred: 1, accuracy: 1.0);
        await SeedMasteryAsync(userId, "知识点A", 0.3);
        await SeedMasteryAsync(userId, "知识点B", 0.9);
        var svc = User.Use<GetPeriodReportService>();

        var result = await svc.ExecuteAsync(new GetPeriodReportReqDto { Period = "Week" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(15, result.LearnedCount);          // 10+5
        Assert.Equal(3, result.StarredCount);           // 2+1
        Assert.Equal(0.9, result.Accuracy);             // (0.8+1.0)/2 个人正确率
        Assert.Equal("知识点A", result.WeakPoints[0].KnowledgePoint); // 0.3 最薄弱在前
        Assert.Equal("知识点B", result.WeakPoints[1].KnowledgePoint);
    }

    /// <summary>BR-08：无周期数据 → 字段为空 + 码 0（非错误）</summary>
    [Fact]
    public async Task ExecuteAsync_NoData_ReturnsEmptyFields()
    {
        SetUser(63002);
        var svc = User.Use<GetPeriodReportService>();

        var result = await svc.ExecuteAsync(new GetPeriodReportReqDto { Period = "Month" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.LearnedCount);
        Assert.Null(result.Accuracy);
        Assert.Equal(0, result.StarredCount);
        Assert.Empty(result.WeakPoints);
    }

    /// <summary>BR-09：非法周期 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPeriod_ReturnsParamInvalid()
    {
        SetUser(63003);
        var svc = User.Use<GetPeriodReportService>();

        var result = await svc.ExecuteAsync(new GetPeriodReportReqDto { Period = "Year" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(StatsErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-10：响应无群组正确率排名（合规红线）</summary>
    [Fact]
    public async Task ExecuteAsync_ResponseHasNoRanking()
    {
        var userId = SetUser(63004);
        await SeedDailyAsync(userId, Today, learned: 5, starred: 1, accuracy: 0.8);
        var svc = User.Use<GetPeriodReportService>();

        var result = await svc.ExecuteAsync(new GetPeriodReportReqDto { Period = "Week" }, TestContext.Current.CancellationToken);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("rank", json, StringComparison.OrdinalIgnoreCase); // 无排名字段
    }
}