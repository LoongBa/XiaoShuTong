using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.6 PK 战绩（GetPkStatsService）Contract 测试
/// 覆盖 BR：BR-24 无记录零值 | BR-25 仅当前用户 | BR-26 胜率 API 层计算 | BR-27 无对比榜
/// </summary>
/// <remarks>
/// 数据源为 VEntity（vw_pk_player_stats）。测试环境说明：
/// - InMemory（TestingEntityDAC）不执行 SQL 视图，但 V4.10.7（框架 SeedViewAsync，消费端反馈 v0.3 落地）
///   提供测试专用填充旁路 SeedViewAsync（直写 _store，绕过 View 写守卫）——VEntity 数据可经此填充。
/// - 查询路径：测试上下文 User.Query<T>() 因 IsInsideDomain=false 走 AOP 路径返回空——
///   本类用注入的 IEntityDAC<PkPlayerStatsView>（与 GetPkStatsService 注入的只读 DAC 同源，
///   V4.10.6 P0-1 Forwarder 桥接保证）填充 + Service 路径（构造注入只读 DAC）验证聚合。
/// - 聚合正确性（仅 Finished 计入、Count/Sum 口径）由 ViewSql 定义，本类验证 Service 消费视图数据的正确性。
/// </remarks>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetPkStatsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private async Task SetUserAsync(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        await User.LoginAsUserAsync(User.UserInfo, TKW.Framework.Enumerations.EnumLoginFrom.PcWeb);
    }

    /// <summary>经 SeedViewAsync 填充 VEntity 数据（V4.10.7 测试专用旁路，绕过 View 写守卫）</summary>
    private async Task SeedViewRowAsync(long userId, long totalMatches, long wins, long draws, long totalScore)
    {
        var dac = User.GetService<IEntityDAC<PkPlayerStatsView>>();
        if (dac is not TestingEntityDAC<PkPlayerStatsView> testingDac)
            throw new InvalidOperationException($"测试环境未使用 TestingEntityDAC，实际类型 {dac.GetType().Name}");

        await testingDac.SeedViewAsync(new[]
        {
            new PkPlayerStatsView
            {
                Id = userId, // 方案 A：业务唯一键 = UserId 透传
                UserId = userId,
                TotalMatches = totalMatches,
                Wins = wins,
                Draws = draws,
                TotalScore = totalScore,
            },
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-26：胜率 = Wins/Total（2 胜 1 平 1 负 4 场 → 0.5）+ BR-27 无对比榜</summary>
    [Fact]
    public async Task ExecuteAsync_Stats_WinRateComputed()
    {
        var userId = 96001L;
        await SetUserAsync(userId);
        await SeedViewRowAsync(userId, totalMatches: 4, wins: 2, draws: 1, totalScore: 75);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalMatches);
        Assert.Equal(2, result.Wins);
        Assert.Equal(1, result.Draws);
        Assert.Equal(0.5, result.WinRate);     // 2/4（API 层计算）
        Assert.Equal(75, result.TotalScore);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // BR-27 无对比榜
    }

    /// <summary>BR-26：胜率四舍五入到 2 位（3 胜 4 场 → 0.75）</summary>
    [Fact]
    public async Task ExecuteAsync_Stats_WinRateRounded()
    {
        var userId = 96004L;
        await SetUserAsync(userId);
        await SeedViewRowAsync(userId, totalMatches: 4, wins: 3, draws: 0, totalScore: 40);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0.75, result.WinRate);
    }

    /// <summary>BR-24：无对战记录 → 零值（视图无该用户行）</summary>
    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsZeros()
    {
        await SetUserAsync(96002);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalMatches);
        Assert.Equal(0, result.Wins);
        Assert.Equal(0, result.Draws);
        Assert.Equal(0d, result.WinRate);
        Assert.Equal(0, result.TotalScore);
    }

    /// <summary>BR-25：仅当前用户战绩（填充他人行，Service 查询应以本人 UserId 过滤）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnMatches()
    {
        await SetUserAsync(96003);
        await SeedViewRowAsync(96901, totalMatches: 10, wins: 8, draws: 1, totalScore: 100);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalMatches); // 不含他人
        Assert.Equal(0, result.TotalScore);
    }

    /// <summary>BR-25/26：本人与他人数据并存时只取本人行 + 胜率计算</summary>
    [Fact]
    public async Task ExecuteAsync_Stats_FiltersOwnRow()
    {
        var userId = 96007L;
        await SetUserAsync(userId);
        await SeedViewRowAsync(userId, totalMatches: 8, wins: 5, draws: 2, totalScore: 120);
        await SeedViewRowAsync(96907, totalMatches: 20, wins: 15, draws: 0, totalScore: 300);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
Assert.Equal(8, result.TotalMatches);
        Assert.Equal(5, result.Wins);
        Assert.Equal(2, result.Draws);
        Assert.Equal(0.62, result.WinRate);    // 5/8=0.625，Service Math.Round(x,2) 银行家舍入 → 0.62
        Assert.Equal(120, result.TotalScore);
    }
}
