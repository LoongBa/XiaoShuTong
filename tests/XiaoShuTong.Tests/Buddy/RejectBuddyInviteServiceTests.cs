using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Services.Buddy;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Buddy;

/// <summary>
/// UC-8.3 拒绝搭子邀请（RejectBuddyInviteService）Contract 测试
/// 覆盖 BR：BR-22 邀请不存在/非被邀请人 → 6001 | BR-23 邀请过期/已处理 → 6002
/// 主流程：有效 Pending 邀请 → Success + Status=Rejected
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class RejectBuddyInviteServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<StudyBuddies> SeedInviteAsync(long inviterId, long inviteeId, BuddyStatus status = BuddyStatus.Pending, DateTime? expiresAt = null)
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
            ExpiresAt = expiresAt ?? now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：有效 Pending 邀请 → Success + 关系置 Rejected</summary>
    [Fact]
    public async Task ExecuteAsync_ValidPendingInvite_Rejects()
    {
        SetUser(53002); // 被邀请人
        var invite = await SeedInviteAsync(53001, 53002);
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var updated = await buddiesDs.EntityGetAsync(x => x.Id == invite.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(BuddyStatus.Rejected, updated.Status);
    }

    /// <summary>BR-22：邀请不存在 → 6001</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownInvite_Returns6001()
    {
        SetUser(53003);
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-22：非被邀请人（第三方）拒绝 → 6001（权限拦截）</summary>
    [Fact]
    public async Task ExecuteAsync_NotInvitee_Returns6001()
    {
        SetUser(53006); // 第三方，非被邀请人
        var invite = await SeedInviteAsync(53004, 53005);
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-23：邀请已过期 → 6002</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredInvite_Returns6002()
    {
        SetUser(53008);
        var invite = await SeedInviteAsync(53007, 53008, expiresAt: DateTime.UtcNow.AddDays(-1));
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteExpired, result.ErrorCode);
    }

    /// <summary>BR-23：已处理（非 Pending）→ 6002</summary>
    [Fact]
    public async Task ExecuteAsync_ProcessedInvite_Returns6002()
    {
        SetUser(53010);
        var invite = await SeedInviteAsync(53009, 53010, status: BuddyStatus.Accepted);
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteExpired, result.ErrorCode);
    }
}
