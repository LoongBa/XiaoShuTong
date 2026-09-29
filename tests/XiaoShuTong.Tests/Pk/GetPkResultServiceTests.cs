using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.GroupManagement;
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
    : XiaoShuTongTestBase(fixture, output)
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

    private async Task SeedMemberAsync(long groupId, long userId, MemberRole role, string? nickname)
    {
        var ds = User.Use<GroupMembersDataService>();
        await ds.EntityCreateAsync(new GroupMembers
        {
            UId = UidGenerator.NewId(),
            GroupId = groupId,
            UserId = userId,
            Role = role,
            Nickname = nickname,
            JoinedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
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

    /// <summary>昵称富化：参赛者有 GroupMembers 记录（Role=Student）→ 富化昵称</summary>
    [Fact]
    public async Task ExecuteAsync_NicknameEnriched_FromStudentMember()
    {
        SetUser(94005);
        var (match, _) = await SeedFinishedMatchAsync(94005, 94501, winnerId: 94005, PkFinishReason.Score);
        await SeedMemberAsync(1, 94005, MemberRole.Student, "小明");
        await SeedMemberAsync(1, 94501, MemberRole.Student, "小红");
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = match.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Players.Count);
        Assert.Contains(result.Players, p => p.UserId == 94005 && p.Nickname == "小明");
        Assert.Contains(result.Players, p => p.UserId == 94501 && p.Nickname == "小红");
    }

    /// <summary>昵称兜底：参赛者无 GroupMembers 记录 / Nickname=null → 学生{userId}</summary>
    [Fact]
    public async Task ExecuteAsync_NicknameFallback_WhenNoStudentMember()
    {
        SetUser(94006);
        var (match, _) = await SeedFinishedMatchAsync(94006, 94601, winnerId: 94006, PkFinishReason.Score);
        await SeedMemberAsync(1, 94601, MemberRole.Student, null); // Nickname=null
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = match.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Players.Count);
        Assert.Contains(result.Players, p => p.UserId == 94006 && p.Nickname == "学生94006");
        Assert.Contains(result.Players, p => p.UserId == 94601 && p.Nickname == "学生94601");
    }

    /// <summary>Oracle C1：Role=Parent 的 GroupMembers 记录不参与昵称富化（防 Role 歧义）</summary>
    [Fact]
    public async Task ExecuteAsync_Nickname_IgnoresParentRole()
    {
        SetUser(94007);
        var (match, _) = await SeedFinishedMatchAsync(94007, 94701, winnerId: 94007, PkFinishReason.Score);
        await SeedMemberAsync(1, 94701, MemberRole.Parent, "家长昵称"); // 仅 Parent 角色
        var svc = User.Use<GetPkResultService>();

        var result = await svc.ExecuteAsync(new GetPkResultReqDto { MatchUid = match.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains(result.Players, p => p.UserId == 94701 && p.Nickname == "学生94701"); // Parent 角色不富化 → 兜底
    }
}