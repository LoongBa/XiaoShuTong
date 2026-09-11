using XiaoShuTong.Services.Pk;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.6 PK 战绩（GetPkStatsService）Contract 测试
/// 覆盖 BR：BR-24 无记录零值
/// </summary>
/// <remarks>
/// 数据源为 VEntity（vw_pk_player_stats）。测试环境说明：
/// - Tier 1（MockDbEntityDAC）不执行 SQL 视图：VEntity 写守卫禁写、只读接口返回空集，无特殊填充通道
///   （v4.10.6 的测试专用 Seed 旁路已于 v4.10.7 移除，ADR60——"伪覆盖"假数据直填 store，视图计算/聚合/策略未被真测）。
/// - 查询路径：测试上下文 User.Query&lt;T&gt;() 因 IsInsideDomain=false 走 AOP 路径不可用（N-5）——
///   本类保留的 BR-24 用例不依赖填充（空数据零值降级），无需 DAC 查询。
/// - 聚合正确性（仅 Finished 计入、Count/Sum 口径、胜率）需真实视图语义——待 Tier 1.5
///   （FreeSqlEntityDAC @ SQLite :memory:，需 PkPlayerStatsView 提供 ViewSqlSQLite 方言变体）真迁移后恢复覆盖。
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
}
