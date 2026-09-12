using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Pk;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Pk;

/// <summary>
/// UC-9.1 发起 PK（CreatePkMatchService）Contract 测试
/// 覆盖 BR：BR-01 搭子资格 2005 | BR-02 题库 1501 | BR-03 题量 1002 | BR-04 对战码 | BR-05 原子创建
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CreatePkMatchServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedBuddyAsync(long userA, long userB, BuddyStatus status = BuddyStatus.Accepted)
    {
        var now = DateTime.UtcNow;
        var ds = User.Use<StudyBuddiesDataService>();
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = userA,
            InviteeId = userB,
            Status = status,
            InvitedAt = now,
            ExpiresAt = now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Banks> SeedBankAsync(string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "PK题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-04/05：accepted 搭子 → 对局创建（Pending + 4 位码 + 发起方参赛记录）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidBuddy_CreatesMatch()
    {
        var userId = SetUser(91001);
        await SeedBuddyAsync(userId, 91101);
        await SeedBankAsync("bank-pk-91001");
        var svc = User.Use<CreatePkMatchService>();

        var result = await svc.ExecuteAsync(new CreatePkMatchReqDto
        {
            OpponentUserId = 91101,
            BankId = "bank-pk-91001",
            QuestionCount = 10,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.MatchUid));
        Assert.Equal(4, result.InviteCode.Length); // BR-04：4 位对战码

        var matchesDs = User.Use<PkMatchesDataService>();
        var match = await matchesDs.EntityGetAsync(x => x.UId == result.MatchUid, TestContext.Current.CancellationToken);
        Assert.NotNull(match);
        Assert.Equal(PkMatchStatus.Pending, match.Status);
        Assert.Equal(result.InviteCode, match.InviteCode);

        // BR-05：发起方参赛记录原子创建
        var playersDs = User.Use<PkPlayersDataService>();
        var players = await playersDs.EntitySelectAsync(x => x.MatchId == match.Id, ct: TestContext.Current.CancellationToken);
        var creator = Assert.Single(players);
        Assert.Equal(userId, creator.UserId);
    }

    /// <summary>BR-01：非搭子 → 2005</summary>
    [Fact]
    public async Task ExecuteAsync_NotBuddy_Returns2005()
    {
        SetUser(91002);
        await SeedBankAsync("bank-pk-91002");
        var svc = User.Use<CreatePkMatchService>();

        var result = await svc.ExecuteAsync(new CreatePkMatchReqDto
        {
            OpponentUserId = 91201, // 无搭子关系
            BankId = "bank-pk-91002",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.NotBuddyPk, result.ErrorCode);
    }

    /// <summary>BR-02：题库不存在 → 1501</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_Returns1501()
    {
        var userId = SetUser(91003);
        await SeedBuddyAsync(userId, 91301);
        var svc = User.Use<CreatePkMatchService>();

        var result = await svc.ExecuteAsync(new CreatePkMatchReqDto
        {
            OpponentUserId = 91301,
            BankId = "no-such-bank",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-03：题量 15（非 5/10/20）→ 1002</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidQuestionCount_Returns1002()
    {
        var userId = SetUser(91004);
        await SeedBuddyAsync(userId, 91401);
        await SeedBankAsync("bank-pk-91004");
        var svc = User.Use<CreatePkMatchService>();

        var result = await svc.ExecuteAsync(new CreatePkMatchReqDto
        {
            OpponentUserId = 91401,
            BankId = "bank-pk-91004",
            QuestionCount = 15,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PkErrorCodes.ParamInvalid, result.ErrorCode);
    }
}