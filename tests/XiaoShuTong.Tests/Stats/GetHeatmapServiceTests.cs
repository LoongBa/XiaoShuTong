using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Stats;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Stats;

/// <summary>
/// UC-6.1 查看记忆热力图（GetHeatmapService）Contract 测试
/// 覆盖 BR：BR-01 空数据 | BR-02 参数校验 | BR-03 RLS | BR-04 按日聚合
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetHeatmapServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedDailyAsync(long userId, DateOnly date, int learned = 5, int starred = 1)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = XiaoShuTong.Tools.UidGenerator.NewId(),
            UserId = userId,
            StatDate = date,
            LearnedCount = learned,
            StarredCount = starred,
            Accuracy = 0.8,
            StudySeconds = 600,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-04：两日数据 → 两条记录（按日聚合）</summary>
    [Fact]
    public async Task ExecuteAsync_TwoDays_ReturnsTwoDays()
    {
        var userId = SetUser(61001);
        var start = new DateTime(2026, 9, 1);
        await SeedDailyAsync(userId, new DateOnly(2026, 9, 1), learned: 10, starred: 2);
        await SeedDailyAsync(userId, new DateOnly(2026, 9, 2), learned: 5, starred: 1);
        var svc = User.Use<GetHeatmapService>();

        var result = await svc.ExecuteAsync(new GetHeatmapReqDto
        {
            Start = start,
            End = new DateTime(2026, 9, 3),
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Days.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Days[0].StatDate);
        Assert.Equal(10, result.Days[0].LearnedCount);
        Assert.Equal(2, result.Days[0].StarredCount);
        Assert.Equal(5, result.Days[1].LearnedCount);
    }

    /// <summary>BR-02：start > end → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_StartAfterEnd_ReturnsParamInvalid()
    {
        SetUser(61002);
        var svc = User.Use<GetHeatmapService>();

        var result = await svc.ExecuteAsync(new GetHeatmapReqDto
        {
            Start = new DateTime(2026, 9, 5),
            End = new DateTime(2026, 9, 1),
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(StatsErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-01：无数据 → 空数组（前端展示空数据态）</summary>
    [Fact]
    public async Task ExecuteAsync_NoData_ReturnsEmpty()
    {
        SetUser(61003);
        var svc = User.Use<GetHeatmapService>();

        var result = await svc.ExecuteAsync(new GetHeatmapReqDto
        {
            Start = new DateTime(2026, 8, 1),
            End = new DateTime(2026, 8, 31),
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Days);
    }

    /// <summary>BR-03：仅当前学生数据（他人数据不返回）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnData()
    {
        var userId = SetUser(61004);
        await SeedDailyAsync(userId, new DateOnly(2026, 9, 1));
        await SeedDailyAsync(999999, new DateOnly(2026, 9, 1)); // 他人
        var svc = User.Use<GetHeatmapService>();

        var result = await svc.ExecuteAsync(new GetHeatmapReqDto
        {
            Start = new DateTime(2026, 9, 1),
            End = new DateTime(2026, 9, 30),
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var day = Assert.Single(result.Days);
        Assert.Equal(5, day.LearnedCount); // 仅本人数据（他人 999999 不返回）
    }
}
