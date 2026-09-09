using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Services.Buddy;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Buddy;

/// <summary>
/// UC-8.6 解除搭子（RemoveBuddyService）Contract 测试
/// 覆盖 BR：BR-30 关系不存在/非当事人/非 accepted → 6001 | BR-31 解除后历史保留（Status=Removed）| BR-32 重复解除（已 removed）→ 6001
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class RemoveBuddyServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<StudyBuddies> SeedBuddyAsync(long inviterId, long inviteeId, BuddyStatus status = BuddyStatus.Accepted)
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

    /// <summary>主流程 + BR-31：accepted 关系任一方解除 → Success + Status=Removed（记录保留 = 历史保留）</summary>
    [Fact]
    public async Task ExecuteAsync_AcceptedRelation_Removes()
    {
        SetUser(53102); // 被邀请方解除
        var buddy = await SeedBuddyAsync(53101, 53102);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var ds = User.Use<StudyBuddiesDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == buddy.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated); // BR-31：记录仍存在（历史保留）
        Assert.Equal(BuddyStatus.Removed, updated.Status);
    }

    /// <summary>BR-30：关系不存在 → 6001</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownRelation_Returns6001()
    {
        SetUser(53103);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-30：非当事人（第三方）→ 6001</summary>
    [Fact]
    public async Task ExecuteAsync_NotParty_Returns6001()
    {
        SetUser(53106); // 第三方，非关系任一方
        var buddy = await SeedBuddyAsync(53104, 53105);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-30：非 accepted 关系（Pending）→ 6001</summary>
    [Fact]
    public async Task ExecuteAsync_PendingRelation_Returns6001()
    {
        SetUser(53108);
        var buddy = await SeedBuddyAsync(53107, 53108, status: BuddyStatus.Pending);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-32：已 removed 关系重复解除 → 6001（幂等拦截）</summary>
    [Fact]
    public async Task ExecuteAsync_AlreadyRemoved_Returns6001()
    {
        SetUser(53110);
        var buddy = await SeedBuddyAsync(53109, 53110, status: BuddyStatus.Removed);
        var svc = User.Use<RemoveBuddyService>();

        var result = await svc.ExecuteAsync(new RemoveBuddyReqDto { BuddyId = buddy.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }
}
