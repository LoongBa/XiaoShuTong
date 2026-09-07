using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.6 PK 战绩（GetPkStatsService）Contract 测试
/// 覆盖 BR：BR-24 无记录零值 | BR-25 仅当前用户 | BR-26 胜率 API 层计算 | BR-27 无对比榜
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetPkStatsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<PkMatches> SeedMatchAsync(long userIdA, long userIdB, PkMatchStatus status, long? winnerId, int scoreA)
    {
        var ds = User.Use<PkMatchesDataService>();
        var match = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank",
            QuestionCount = 5,
            Mode = PkMode.Sync,
            Status = status,
            WinnerId = winnerId,
            FinishReason = status == PkMatchStatus.Finished ? PkFinishReason.Score : null,
        }, TestContext.Current.CancellationToken);

        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = userIdA, Score = scoreA }, TestContext.Current.CancellationToken);
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = userIdB, Score = 10 }, TestContext.Current.CancellationToken);
        return match;
    }

    /// <summary>主流程 + BR-26：胜率 = Wins/Total（8 胜 12 场 → 0.67 类比分）</summary>
    [Fact]
    public async Task ExecuteAsync_Stats_WinRateComputed()
    {
        var userId = SetUser(96001);
        // 2 胜 1 平 1 负（4 场 Finished，1 场 Ongoing 不计入）
        await SeedMatchAsync(userId, 96101, PkMatchStatus.Finished, winnerId: userId, scoreA: 30);
        await SeedMatchAsync(userId, 96102, PkMatchStatus.Finished, winnerId: userId, scoreA: 20);
        await SeedMatchAsync(userId, 96103, PkMatchStatus.Finished, winnerId: null, scoreA: 20);   // 平局
        await SeedMatchAsync(userId, 96104, PkMatchStatus.Finished, winnerId: 96104, scoreA: 5);  // 负
        await SeedMatchAsync(userId, 96105, PkMatchStatus.Ongoing, winnerId: null, scoreA: 10);   // 不计入
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalMatches);  // 仅 Finished 计入
        Assert.Equal(2, result.Wins);
        Assert.Equal(1, result.Draws);
        Assert.Equal(0.5, result.WinRate);     // 2/4
        Assert.Equal(75, result.TotalScore);   // 30+20+20+5

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // BR-27 无对比榜
    }

    /// <summary>BR-24：无对战记录 → 零值</summary>
    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsZeros()
    {
        SetUser(96002);
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalMatches);
        Assert.Equal(0, result.Wins);
        Assert.Equal(0, result.Draws);
        Assert.Equal(0d, result.WinRate);
        Assert.Equal(0, result.TotalScore);
    }

    /// <summary>BR-25：仅当前用户战绩（他人对局不计入）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnMatches()
    {
        var userId = SetUser(96003);
        await SeedMatchAsync(96901, 96902, PkMatchStatus.Finished, winnerId: 96901, scoreA: 30); // 他人对局
        var svc = User.Use<GetPkStatsService>();

        var result = await svc.ExecuteAsync(new GetPkStatsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalMatches); // 不含他人对局
        Assert.Equal(0, result.TotalScore);
    }
}