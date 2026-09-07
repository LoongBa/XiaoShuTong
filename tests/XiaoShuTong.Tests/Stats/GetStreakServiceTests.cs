using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Stats;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Stats;

/// <summary>
/// UC-6.2 查看连续天数（GetStreakService）Contract 测试
/// 覆盖 BR：BR-05 无记录 0 | BR-06 当日/昨日延续 | BR-07 断更清零、最长保留
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetStreakServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)); // UTC+8 业务日

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedDailyAsync(long userId, DateOnly date)
    {
        var ds = User.Use<DailyStatsDataService>();
        await ds.EntityCreateAsync(new DailyStats
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            StatDate = date,
            LearnedCount = 1,
            StudySeconds = 300,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-05：无记录 → 当前 0、最长 0</summary>
    [Fact]
    public async Task ExecuteAsync_NoRecords_ZeroStreaks()
    {
        SetUser(62001);
        var svc = User.Use<GetStreakService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.CurrentStreak);
        Assert.Equal(0, result.LongestStreak);
    }

    /// <summary>BR-06：连续 3 天（含今日）→ 当前 3、最长 3</summary>
    [Fact]
    public async Task ExecuteAsync_ThreeConsecutiveDays_StreakThree()
    {
        var userId = SetUser(62002);
        await SeedDailyAsync(userId, Today.AddDays(-2));
        await SeedDailyAsync(userId, Today.AddDays(-1));
        await SeedDailyAsync(userId, Today);
        var svc = User.Use<GetStreakService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak);
    }

    /// <summary>BR-06/07：断更清零——昨日+今日连击，但更早断更 → 当前 2、最长 2</summary>
    [Fact]
    public async Task ExecuteAsync_BrokenStreak_ResetsCurrent()
    {
        var userId = SetUser(62003);
        await SeedDailyAsync(userId, Today.AddDays(-5)); // 更早记录（断更）
        await SeedDailyAsync(userId, Today.AddDays(-1));
        await SeedDailyAsync(userId, Today);
        var svc = User.Use<GetStreakService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.CurrentStreak); // 断更清零，从昨日重新起算
        Assert.Equal(2, result.LongestStreak);
    }

    /// <summary>BR-07：最长保留——曾 3 天连击 + 当前 2 天 → 最长 3</summary>
    [Fact]
    public async Task ExecuteAsync_LongestStreak_Retained()
    {
        var userId = SetUser(62004);
        await SeedDailyAsync(userId, Today.AddDays(-8));
        await SeedDailyAsync(userId, Today.AddDays(-7));
        await SeedDailyAsync(userId, Today.AddDays(-6)); // 历史 3 连
        await SeedDailyAsync(userId, Today.AddDays(-1));
        await SeedDailyAsync(userId, Today);             // 当前 2 连
        var svc = User.Use<GetStreakService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak); // 历史最长保留
    }
}
