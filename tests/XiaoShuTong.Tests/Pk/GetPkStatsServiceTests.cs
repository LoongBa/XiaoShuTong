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
/// 数据源为 VEntity（vw_pk_player_stats）——InMemory DAC 不执行 SQL 视图，
/// 测试手动填充 PkPlayerStatsView 数据（tkwf-test §7 VEntity 策略）。
/// 视图聚合口径（仅 Finished 计入）由 vw_pk_player_stats ViewSql 定义，测试验证 Service 消费视图数据的正确性。
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
        // EQR 认证守卫：需激活会话（User.Query 要求 IsAuthenticated）
        await User.LoginAsUserAsync(User.UserInfo, TKW.Framework.Enumerations.EnumLoginFrom.PcWeb);
    }

    /// <summary>
    /// 手动填充视图数据（InMemory DAC）。
    /// ⚠️ 框架对 IEntityDAC&lt;T&gt; 与 IEntityReadOnlyDAC&lt;T&gt; 分别注册独立 scoped 实例（即使实现类型相同）；
    /// VEntity 查询（User.Query / EQR）走 IEntityReadOnlyDAC&lt;T&gt; —— 必须向该实例插入数据。
    /// TestingEntityDAC&lt;T&gt; 同时实现两接口，强转后调用其 InsertAsync。
    /// </summary>
    private async Task SeedViewRowAsync(long userId, long totalMatches, long wins, long draws, long totalScore)
    {
        var readDac = User.GetService<IEntityReadOnlyDAC<PkPlayerStatsView>>();
        if (readDac is not TestingEntityDAC<PkPlayerStatsView> testingDac)
            throw new InvalidOperationException($"测试环境未使用 TestingEntityDAC，实际类型 {readDac.GetType().Name}");

        await testingDac.InsertAsync(new PkPlayerStatsView
        {
            Id = userId, // 方案 A：业务唯一键 = UserId 透传
            UserId = userId,
            TotalMatches = totalMatches,
            Wins = wins,
            Draws = draws,
            TotalScore = totalScore,
        }, TestContext.Current.CancellationToken);

        // 自检：IEntityReadOnlyDAC 读回确认（与 EQR 同源）
        var selfRows = readDac.Query.ToList();
        if (selfRows.All(r => r.UserId != userId))
            throw new InvalidOperationException($"SeedViewRowAsync 自检失败：IEntityReadOnlyDAC 读回未命中 UserId={userId}，共 {selfRows.Count} 行");
    }

    /// <summary>主流程 + BR-26：胜率 = Wins/Total（2 胜 1 平 1 负 4 场 → 0.5）</summary>
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

    /// <summary>BR-25：仅当前用户战绩（视图数据按 UserId 过滤，他人行不可见）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnMatches()
    {
        await SetUserAsync(96003);
        // 他人战绩行（不应被当前用户查到）
        await SeedViewRowAsync(96901, totalMatches: 10, wins: 8, draws: 1, totalScore: 100);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalMatches); // 不含他人
        Assert.Equal(0, result.TotalScore);
    }
}
