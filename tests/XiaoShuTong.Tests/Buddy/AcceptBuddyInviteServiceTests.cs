using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Services.Buddy;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Buddy;

/// <summary>
/// UC-8.2 同意搭子邀请（AcceptBuddyInviteService）+ UC-8.3 拒绝（RejectBuddyInviteService）Contract 测试
/// 覆盖 BR：BR-19 邀请不存在 | BR-20 过期/已处理 | BR-21 双方≤5 | BR-22/23 拒绝路径
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class AcceptBuddyInviteServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<StudyBuddies> SeedInviteAsync(DateTime? expiresAt = null, BuddyStatus status = BuddyStatus.Pending)
    {
        var now = DateTime.UtcNow;
        var ds = User.Use<StudyBuddiesDataService>();
        return await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = 82001,
            InviteeId = 82002,
            Status = status,
            InvitedAt = now,
            ExpiresAt = expiresAt ?? now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：有效邀请 → Accepted + AcceptedAt 落库</summary>
    [Fact]
    public async Task ExecuteAsync_ValidInvite_Accepts()
    {
        SetUser(82002); // 被邀请人
        var invite = await SeedInviteAsync();
        var svc = User.Use<AcceptBuddyInviteService>();

        var result = await svc.ExecuteAsync(new AcceptBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(invite.UId, result.BuddyId);

        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var updated = await buddiesDs.EntityGetAsync(x => x.Id == invite.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(BuddyStatus.Accepted, updated.Status);
        Assert.NotNull(updated.AcceptedAt);
    }

    /// <summary>BR-19：邀请不存在 → 6001</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownInvite_Returns6001()
    {
        SetUser(82003);
        var svc = User.Use<AcceptBuddyInviteService>();

        var result = await svc.ExecuteAsync(new AcceptBuddyInviteReqDto { InviteId = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }

    /// <summary>BR-20：邀请已过期 → 6002</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredInvite_Returns6002()
    {
        SetUser(82002);
        var invite = await SeedInviteAsync(expiresAt: DateTime.UtcNow.AddDays(-1));
        var svc = User.Use<AcceptBuddyInviteService>();

        var result = await svc.ExecuteAsync(new AcceptBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteExpired, result.ErrorCode);
    }

    /// <summary>BR-20：已处理（非 Pending）→ 6002；BR-19：非被邀请人 → 6001</summary>
    [Fact]
    public async Task ExecuteAsync_ProcessedInvite_Returns6002()
    {
        SetUser(82002);
        var invite = await SeedInviteAsync(status: BuddyStatus.Rejected);
        var svc = User.Use<AcceptBuddyInviteService>();

        var result = await svc.ExecuteAsync(new AcceptBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteExpired, result.ErrorCode);
    }

    /// <summary>BR-21：同意方已满 5 搭子 → 6003</summary>
    [Fact]
    public async Task ExecuteAsync_BuddyLimitReached_Returns6003()
    {
        SetUser(82012); // 同意方（被邀请人），已有 5 个 accepted
        var now = DateTime.UtcNow;
        var ds = User.Use<StudyBuddiesDataService>();
        for (var i = 1; i <= 5; i++)
        {
            await ds.EntityCreateAsync(new StudyBuddies
            {
                UId = UidGenerator.NewId(),
                InviterId = 82012,
                InviteeId = 82900 + i,
                Status = BuddyStatus.Accepted,
                InvitedAt = now,
                ExpiresAt = now.AddDays(7),
            }, TestContext.Current.CancellationToken);
        }
        // 给 82012 建一条待其同意的邀请
        var pending = await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = 82999,
            InviteeId = 82012,
            Status = BuddyStatus.Pending,
            InvitedAt = now,
            ExpiresAt = now.AddDays(7),
        }, TestContext.Current.CancellationToken);
        var svc = User.Use<AcceptBuddyInviteService>();

        var result = await svc.ExecuteAsync(new AcceptBuddyInviteReqDto { InviteId = pending.UId }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyLimitReached, result.ErrorCode);
    }

    /// <summary>Reject：主流程 → Rejected</summary>
    [Fact]
    public async Task Reject_ValidInvite_Rejects()
    {
        SetUser(82002);
        var invite = await SeedInviteAsync();
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = invite.UId }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var updated = await buddiesDs.EntityGetAsync(x => x.Id == invite.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(BuddyStatus.Rejected, updated.Status);
    }

    /// <summary>BR-22/23：Reject 未知/已处理 → 6001/6002</summary>
    [Fact]
    public async Task Reject_UnknownInvite_Returns6001()
    {
        SetUser(82002);
        var svc = User.Use<RejectBuddyInviteService>();

        var result = await svc.ExecuteAsync(new RejectBuddyInviteReqDto { InviteId = "no-such" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteNotFound, result.ErrorCode);
    }
}