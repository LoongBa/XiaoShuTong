using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.6 PK 战绩（GetPkStatsService）Contract 测试
/// 覆盖 BR：BR-24 无记录零值 | BR-25 仅当前用户（RLS）| BR-26 胜率 API 层计算
/// </summary>
/// <remarks>
/// ⚠️ **Tier 1.5（SQLite :memory: 真实视图）**：数据源为 VEntity（vw_pk_player_stats，仅 Finished 计入）。
/// 框架 v4.10.14（G6b）已修复 Tier 1.5 全链路（ConfigTestDomainAsync + SyncViewsAsync + 每 Fact Reset 原地清空），
/// 本类恢复真实视图聚合语义测试——写基表（PkMatches/PkPlayers）→ Service 读视图聚合 → 断言。
/// 每 Fact 前经 XiaoShuTongTestBase.OnInitializeAsync 自动 Reset（ADR66 契约：干净空库开始）。
/// 断言口径：仅自己 UserId 的 Finished 对局；TotalMatches/Wins/Draws = 视图 COUNT/SUM；WinRate = API 层 Math.Round(…,2)。
/// </remarks>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetPkStatsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    /// <summary>
    /// 种子一场 Finished 对局：me 参与并得 <paramref name="meScore"/> 分；对手 0 分。
    /// <paramref name="winnerId"/> = 胜者（null = 平局）。经 DataService 写入（审计字段自动填充，走真实 SQLite 表）。
    /// </summary>
    private async Task SeedFinishedAsync(long meId, long opponentId, long? winnerId, int meScore)
    {
        var matchesDs = User.Use<PkMatchesDataService>();
        var match = await matchesDs.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank-pk-stats",
            QuestionCount = 1,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Finished,
            WinnerId = winnerId,
            FinishReason = PkFinishReason.Score,
        }, TestContext.Current.CancellationToken);

        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers
        {
            UId = UidGenerator.NewId(),
            MatchId = match.Id,
            UserId = meId,
            Score = meScore,
        }, TestContext.Current.CancellationToken);
        await playersDs.EntityCreateAsync(new PkPlayers
        {
            UId = UidGenerator.NewId(),
            MatchId = match.Id,
            UserId = opponentId,
            Score = 0,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<GetPkStatsResDto> ExecuteAsync(long userId)
    {
        SetUser(userId);
        var svc = User.Use<GetPkStatsService>();
        return await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);
    }

    /// <summary>BR-24/26：2 场 Finished（1 胜 1 平）→ 场次 2 / 胜 1 / 平 1 / 胜率 0.5 / 累计分</summary>
    [Fact]
    public async Task ExecuteAsync_WinRateComputed_Returns050()
    {
        await SeedFinishedAsync(96101, 96901, winnerId: 96101, meScore: 10); // 胜
        await SeedFinishedAsync(96101, 96902, winnerId: null, meScore: 8);   // 平

        var result = await ExecuteAsync(96101);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalMatches);
        Assert.Equal(1, result.Wins);
        Assert.Equal(1, result.Draws);
        Assert.Equal(0.5d, result.WinRate); // 1/2，API 层计算
        Assert.Equal(18, result.TotalScore); // 10+8
    }

    /// <summary>BR-26：4 场 Finished（3 胜 1 平）→ 胜率 0.75</summary>
    [Fact]
    public async Task ExecuteAsync_WinRateRounded_Returns075()
    {
        await SeedFinishedAsync(96102, 96901, winnerId: 96102, meScore: 10);
        await SeedFinishedAsync(96102, 96902, winnerId: 96102, meScore: 10);
        await SeedFinishedAsync(96102, 96903, winnerId: 96102, meScore: 10);
        await SeedFinishedAsync(96102, 96904, winnerId: null, meScore: 8);

        var result = await ExecuteAsync(96102);

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalMatches);
        Assert.Equal(3, result.Wins);
        Assert.Equal(1, result.Draws);
        Assert.Equal(0.75d, result.WinRate);
        Assert.Equal(38, result.TotalScore);
    }

    /// <summary>BR-24：无对局记录 → 全零值（视图无该用户行）</summary>
    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsZeros()
    {
        var result = await ExecuteAsync(96103);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalMatches);
        Assert.Equal(0, result.Wins);
        Assert.Equal(0, result.Draws);
        Assert.Equal(0d, result.WinRate);
        Assert.Equal(0, result.TotalScore);
    }

    /// <summary>BR-25：其他用户对局不影响自己（RLS——视图按 UserId 分组）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnMatches_IgnoresOthers()
    {
        await SeedFinishedAsync(96104, 96901, winnerId: 96104, meScore: 10);   // 自己 1 场胜
        await SeedFinishedAsync(96999, 96998, winnerId: 96999, meScore: 0);    // 他人 3 场（不读）
        await SeedFinishedAsync(96998, 96997, winnerId: null, meScore: 0);
        await SeedFinishedAsync(96997, 96996, winnerId: 96997, meScore: 0);

        var result = await ExecuteAsync(96104);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalMatches);
        Assert.Equal(1, result.Wins);
        Assert.Equal(0, result.Draws);
        Assert.Equal(1.0d, result.WinRate);
        Assert.Equal(10, result.TotalScore);
    }

    /// <summary>BR-26 银行家舍入：5 胜 8 场（3 负）→ 5/8=0.625 → ToEven 舍入 0.62</summary>
    [Fact]
    public async Task ExecuteAsync_FiltersOwnRow_Returns062()
    {
        // 8 场：5 胜（score 10）+ 3 负（score 5）
        await SeedFinishedAsync(96105, 96901, winnerId: 96105, meScore: 10);
        await SeedFinishedAsync(96105, 96902, winnerId: 96105, meScore: 10);
        await SeedFinishedAsync(96105, 96903, winnerId: 96105, meScore: 10);
        await SeedFinishedAsync(96105, 96904, winnerId: 96105, meScore: 10);
        await SeedFinishedAsync(96105, 96905, winnerId: 96105, meScore: 10);
        await SeedFinishedAsync(96105, 96906, winnerId: 96906, meScore: 5);  // 负
        await SeedFinishedAsync(96105, 96907, winnerId: 96907, meScore: 5);  // 负
        await SeedFinishedAsync(96105, 96908, winnerId: 96908, meScore: 5);  // 负

        var result = await ExecuteAsync(96105);

        Assert.True(result.Success);
        Assert.Equal(8, result.TotalMatches);
        Assert.Equal(5, result.Wins);
        Assert.Equal(0, result.Draws);
        Assert.Equal(0.62d, result.WinRate); // Math.Round(5/8, 2, ToEven)
        Assert.Equal(65, result.TotalScore); // 5*10 + 3*5
    }
}
