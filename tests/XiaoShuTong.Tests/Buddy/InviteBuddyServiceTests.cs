using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.Buddy;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Buddy;

/// <summary>
/// UC-8.1 发起搭子邀请（InviteBuddyService）Contract 测试
/// 覆盖 BR：BR-14 搭子数≥5 | BR-15 当日邀请>10 | BR-16 同群组/同年级（OR）| BR-17 重复邀请 | BR-18 ExpiresAt+7天
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class InviteBuddyServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(string groupUid, string? grade, params long[] memberIds)
    {
        var ds = User.Use<GroupsDataService>();
        var group = await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = 80001,
            Name = "搭子群组",
            Subject = "chinese",
            Grade = grade,
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);

        var membersDs = User.Use<GroupMembersDataService>();
        foreach (var memberId in memberIds)
        {
            await membersDs.EntityCreateAsync(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = memberId,
                Role = MemberRole.Student,
                JoinedAt = DateTime.UtcNow,
            }, TestContext.Current.CancellationToken);
        }
        return group;
    }

    private async Task SeedAcceptedAsync(long userId, long otherId)
    {
        var ds = User.Use<StudyBuddiesDataService>();
        var now = DateTime.UtcNow;
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = userId,
            InviteeId = otherId,
            Status = BuddyStatus.Accepted,
            InvitedAt = now,
            AcceptedAt = now,
            ExpiresAt = now.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>种子过期 Pending 邀请（ExpiresAt 过去，惰性过期语义——Status 保留 Pending）</summary>
    private async Task SeedExpiredPendingAsync(long inviterId, long inviteeId)
    {
        var ds = User.Use<StudyBuddiesDataService>();
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = inviterId,
            InviteeId = inviteeId,
            Status = BuddyStatus.Pending,
            InvitedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-18：同群组邀请成功 + ExpiresAt = +7 天</summary>
    [Fact]
    public async Task ExecuteAsync_SameGroup_CreatesInviteWith7DayExpiry()
    {
        var userId = SetUser(81001);
        await SeedGroupAsync($"group-buddy-{81001}", "七年级", userId, 81101);
        var before = DateTime.UtcNow;
        var svc = User.Use<InviteBuddyService>();

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81101 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.InviteId));
        Assert.InRange(result.ExpiresAt, before.AddDays(6.9), before.AddDays(7.1)); // +7 天

        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var buddy = await buddiesDs.EntityGetAsync(x => x.UId == result.InviteId, TestContext.Current.CancellationToken);
        Assert.NotNull(buddy);
        Assert.Equal(BuddyStatus.Pending, buddy.Status);
    }

    /// <summary>BR-16：不同群组且不同年级 → 6006</summary>
    [Fact]
    public async Task ExecuteAsync_DifferentGroupDifferentGrade_Returns6006()
    {
        var userId = SetUser(81002);
        await SeedGroupAsync($"group-a-{81002}", "七年级", userId);
        await SeedGroupAsync($"group-b-{81002}", "八年级", 81201); // 不同年级
        var svc = User.Use<InviteBuddyService>();

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81201 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyGroupGradeRequired, result.ErrorCode);
    }

    /// <summary>BR-16：同年级（不同群组）→ 允许（OR 语义）</summary>
    [Fact]
    public async Task ExecuteAsync_SameGradeDifferentGroup_Allowed()
    {
        var userId = SetUser(81003);
        await SeedGroupAsync($"group-a-{81003}", "七年级", userId);
        await SeedGroupAsync($"group-b-{81003}", "七年级", 81301); // 同年级
        var svc = User.Use<InviteBuddyService>();

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81301 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>BR-14：已有 5 个 accepted 搭子 → 6003</summary>
    [Fact]
    public async Task ExecuteAsync_FiveBuddies_Returns6003()
    {
        var userId = SetUser(81004);
        await SeedGroupAsync($"group-buddy-{81004}", "七年级", userId);
        for (var i = 1; i <= 5; i++)
            await SeedAcceptedAsync(userId, 81400 + i);
        var svc = User.Use<InviteBuddyService>();

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81499 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyLimitReached, result.ErrorCode);
    }

    /// <summary>BR-17：重复邀请（已存在 pending）→ 6002</summary>
    [Fact]
    public async Task ExecuteAsync_DuplicateInvite_Returns6002()
    {
        var userId = SetUser(81005);
        await SeedGroupAsync($"group-buddy-{81005}", "七年级", userId, 81501);
        var svc = User.Use<InviteBuddyService>();
        var first = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81501 }, TestContext.Current.CancellationToken);
        Assert.True(first.Success);

        var second = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81501 }, TestContext.Current.CancellationToken);

        Assert.False(second.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteExpired, second.ErrorCode);
    }

    /// <summary>BR-15：当日邀请 >10 → 6005</summary>
    [Fact]
    public async Task ExecuteAsync_DailyLimit_Returns6005()
    {
        var userId = SetUser(81006);
        await SeedGroupAsync($"group-buddy-{81006}", "七年级", userId);
        var svc = User.Use<InviteBuddyService>();
        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var now = DateTime.UtcNow;
        for (var i = 1; i <= 10; i++)
        {
            await buddiesDs.EntityCreateAsync(new StudyBuddies
            {
                UId = UidGenerator.NewId(),
                InviterId = userId,
                InviteeId = 81600 + i,
                Status = BuddyStatus.Pending,
                InvitedAt = now,
                ExpiresAt = now.AddDays(7),
            }, TestContext.Current.CancellationToken);
        }

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81699 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteDailyLimit, result.ErrorCode);
    }

    /// <summary>BR-17 修正：过期 Pending（惰性过期）复用复活——同方向重邀放行且复用原记录（维持 UNIQUE(InviterId,InviteeId) 单记录约束）（V0.6.19）</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredPending_ReinviteReusesSameRow()
    {
        var userId = SetUser(81007);
        await SeedGroupAsync($"group-buddy-{81007}", "七年级", userId, 81701);
        var buddiesDs = User.Use<StudyBuddiesDataService>();
        var staleUid = UidGenerator.NewId();
        await buddiesDs.EntityCreateAsync(new StudyBuddies
        {
            UId = staleUid,
            InviterId = userId,
            InviteeId = 81701,
            Status = BuddyStatus.Pending,
            InvitedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // 过期 Pending（Status 仍 Pending）
        }, TestContext.Current.CancellationToken);
        var svc = User.Use<InviteBuddyService>();
        var before = DateTime.UtcNow;

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81701 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success); // 过期豁免，放行重邀
        Assert.Equal(staleUid, result.InviteId); // 复用原记录 UId（非新建）
        Assert.InRange(result.ExpiresAt, before.AddDays(6.9), before.AddDays(7.1)); // ExpiresAt 刷新 +7 天（BR-18）

        // 同方向仍只有一条记录（UNIQUE 约束未被破坏）
        var rows = await buddiesDs.EntitySelectAsync(x => x.InviterId == userId && x.InviteeId == 81701, ct: TestContext.Current.CancellationToken);
        Assert.Single(rows);
    }

    /// <summary>BR-17 修正（Oracle C3）：过期 Pending 反向豁免——收到过对方过期邀请的人可反向邀请对方（V0.6.19）</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredPending_ReverseDirectionAllowsInvite()
    {
        var userId = SetUser(81008); // 我是被邀请方（历史上收到过 81702 的过期邀请）
        await SeedGroupAsync($"group-buddy-{81008}", "七年级", userId, 81702);
        await SeedExpiredPendingAsync(81702, userId); // 反向：81702 曾邀请我，已过期
        var svc = User.Use<InviteBuddyService>();

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81702 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success); // 反向过期也豁免
    }

    /// <summary>BR-17 修正（Oracle M2）：跨方向并存（对方过期邀请 + 我已接受）仍阻塞——EntitySelectAsync 全量判定防误放行（V0.6.19）</summary>
    [Fact]
    public async Task ExecuteAsync_OtherDirectionExpired_ButAcceptedExists_StillReturns6002()
    {
        var userId = SetUser(81009);
        await SeedGroupAsync($"group-buddy-{81009}", "七年级", userId, 81703);
        await SeedExpiredPendingAsync(81703, userId); // 对方 → 我（过期 Pending，跨方向）
        await SeedAcceptedAsync(userId, 81703);       // 我 → 对方（已 accepted）
        var svc = User.Use<InviteBuddyService>();

        var result = await svc.ExecuteAsync(new InviteBuddyReqDto { InviteeUserId = 81703 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BuddyErrorCodes.BuddyInviteExpired, result.ErrorCode); // accepted 恒阻塞（跨方向过期不影响）
    }
}