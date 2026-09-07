using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.4 PK 结果（GetPkResultService）Contract 测试
/// 覆盖 BR：BR-17 对局不存在 | BR-18 AI 点评兜底 | BR-19 无对比榜 | BR-20 WinnerId 判定（NULL=平局）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetPkResultServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<(PkMatches Match, List<PkPlayers> Players)> SeedFinishedMatchAsync(
        long userA, long userB, long? winnerId, PkFinishReason reason)
    {
        var ds = User.Use<PkMatchesDataService>();
        var match = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank",
            QuestionCount = 2,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Finished,
            WinnerId = winnerId,
            FinishReason = reason,
            FinishedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        var playersDs = User.Use<PkPlayersDataService>();
        var a = await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = userA, Score = 20, TotalTimeMs = 8000 }, TestContext.Current.CancellationToken);
        var b = await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = userB, Score = 10, TotalTimeMs = 9000 }, TestContext.Current.CancellationToken);
        return (match, [a, b]);
    }

    /// <summary>主流程 + BR-18/19/20：胜负判定 + AI 点评兜底 + 无对比榜</summary>
    [Fact]
    public async Task ExecuteAsync_FinishedMatch_ReturnsResult()
    {
        SetUser(94001);
        var (match, _) = await SeedFinishedMatchAsync(94001, 94101, winnerId: 94001, PkFinishReason.Score);
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = match.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Finished", result.Status);
        Assert.Equal(94001, result.WinnerId); // BR-20
        Assert.Equal("Score", result.FinishReason);
        Assert.Equal(2, result.Players.Count);
        Assert.All(result.Players, p => Assert.False(string.IsNullOrEmpty(p.AiComment))); // BR-18 兜底文案

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("accuracy", json, StringComparison.OrdinalIgnoreCase); // BR-19 无正确率对比榜
    }

    /// <summary>BR-20：同分 → WinnerId=null（平局）</summary>
    [Fact]
    public async Task ExecuteAsync_Draw_ReturnsNullWinner()
    {
        SetUser(94002);
        var ds = User.Use<PkMatchesDataService>();
        var match = await ds.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = Subject.Chinese,
            BankId = "bank",
            QuestionCount = 2,
            Mode = PkMode.Sync,
            Status = PkMatchStatus.Finished,
            WinnerId = null,
            FinishReason = PkFinishReason.Score,
        }, TestContext.Current.CancellationToken);
        var playersDs = User.Use<PkPlayersDataService>();
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = 94002, Score = 10 }, TestContext.Current.CancellationToken);
        await playersDs.EntityCreateAsync(new PkPlayers { UId = UidGenerator.NewId(), MatchId = match.Id, UserId = 94201, Score = 10 }, TestContext.Current.CancellationToken);
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = match.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(result.WinnerId); // 平局
    }

    /// <summary>BR-17：对局不存在 → 2001</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownMatch_Returns2001()
    {
        SetUser(94003);
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.MatchNotFound, result.ErrorCode);
    }

    /// <summary>BR-11：非参赛者 → 2004</summary>
    [Fact]
    public async Task ExecuteAsync_NotParticipant_Returns2004()
    {
        SetUser(94004);
        var (match, _) = await SeedFinishedMatchAsync(94401, 94402, winnerId: 94401, PkFinishReason.Score); // 当前用户未参赛
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = match.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.NotParticipant, result.ErrorCode);
    }
}