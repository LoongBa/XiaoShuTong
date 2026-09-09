using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Rank;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Services.Buddy;
using XiaoShuTong.Services.Rank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Buddy;

/// <summary>
/// UC-8.5 搭子排名详情（GetBuddyRankService）+ UC-8.6 解除（RemoveBuddyService）Contract 测试
/// 覆盖 BR：BR-28 非搭子 6004 | BR-29 最小化暴露 | BR-30 非当事人 | BR-31 历史保留 | BR-32 重复解除
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetBuddyRankServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<StudyBuddies> SeedBuddyAsync(long inviterId, long inviteeId, BuddyStatus status)
    {
        var now = DateTime.UtcNow;
        var ds = User.Use<StudyBuddiesDataService>();
        return await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = inviterId,
            InviteeId = inviteeId,
            Status = status,
            InvitedAt = now,
            ExpiresAt = now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedSnapshotAsync(long userId, string groupUid, int rank)
    {
        var ds = User.Use<RankSnapshotsDataService>();
        await ds.EntityCreateAsync(new RankSnapshots
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            ScopeType = RankScopeType.Group,
            ScopeId = groupUid,
            Subject = "All",
            MetricType = RankMetricType.Streak,
            MetricValue = rank * 2m,
            Rank = rank,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-29：accepted 搭子 → 最新排名快照（最小化暴露）</summary>
    [Fact]
    public async Task ExecuteAsync_AcceptedBuddy_ReturnsRank()
    {
        var userId = SetUser(85001);
        var buddy = await SeedBuddyAsync(userId, 85101, BuddyStatus.Accepted);
        await SeedSnapshotAsync(85101, "group-buddy-rank", rank: 2);
        var svc = User.Use<GetBuddyRankService>();

        var result = await svc.ExecuteAsync(new GetBuddyRankReqDto
        {
            BuddyId = buddy.UId,
            ScopeType = "Group",
            ScopeId = "group-buddy-rank",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Rank);
        Assert.Equal(4m, result.MetricValue);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>BR-28：非 accepted 搭子 → 6004</summary>
    [Fact]
    public async Task ExecuteAsync_NotBuddy_Returns6004()
    {
        SetUser(85002);
        var buddy = await SeedBuddyAsync(85901, 85902, BuddyStatus.Accepted); // 与当前用户无关
        var svc = User.Use<GetBuddyRankService>();

        var result = await svc.ExecuteAsync(new GetBuddyRankReqDto
        {
            BuddyId = buddy.UId,
            ScopeType = "Group",
            ScopeId = "g",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.NotBuddy, result.ErrorCode);
    }

    /// <summary>Remove 主流程 + BR-31：accepted 解除 → Removed（历史保留）</summary>
    [Fact]
    public async Task Remove_Accepted_Removes()
    {
        var userId = SetUser(85003);
        var buddy = await SeedBuddyAsync(userId, 85301, BuddyStatus.Accepted);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var updated = await buddiesDs.EntityGetAsync(x => x.Id == buddy.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(BuddyStatus.Removed, updated.Status); // 状态置 Removed，行保留
    }

    /// <summary>BR-30：非当事人解除 → 6001</summary>
    [Fact]
    public async Task Remove_NotParty_Returns6001()
    {
        SetUser(85004);
        var buddy = await SeedBuddyAsync(85991, 85992, BuddyStatus.Accepted); // 与当前用户无关
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-32：重复解除（已 Removed）→ 6001</summary>
    [Fact]
    public async Task Remove_AlreadyRemoved_Returns6001()
    {
        var userId = SetUser(85005);
        var buddy = await SeedBuddyAsync(userId, 85501, BuddyStatus.Removed);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }
}