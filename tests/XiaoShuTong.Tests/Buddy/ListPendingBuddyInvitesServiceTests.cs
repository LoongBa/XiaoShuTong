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
/// UC-8.2/8.3 前置：待收搭子邀请列表（ListPendingBuddyInvitesService）Contract 测试
/// 覆盖搭子-BR-35：InviteeId=me + Pending + 未过期（惰性过滤）| 空列表 | 过期过滤 | RLS
/// 搭子-BR-36：状态过滤（Accepted/Rejected/Removed 不返回）| 只读无副作用 | 昵称兜底
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListPendingBuddyInvitesServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedBuddyAsync(
        long inviterId, long inviteeId, BuddyStatus status,
        DateTime? invitedAt = null, DateTime? expiresAt = null)
    {
        var ds = User.Use<StudyBuddiesDataService>();
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = inviterId,
            InviteeId = inviteeId,
            Status = status,
            InvitedAt = invitedAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
        }, TestContext.Current.CancellationToken);
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

    /// <summary>BR-35/36 主路径：invitee=me 返回 Pending 未过期邀请（昵称 + InvitedAt 倒序）</summary>
    [Fact]
    public async Task ExecuteAsync_PendingInvites_ReturnsNewestFirst()
    {
        var meId = SetUser(48201);
        var now = DateTime.UtcNow;
        await SeedBuddyAsync(48202, meId, BuddyStatus.Pending, now.AddDays(-3), now.AddDays(4)); // 小红 3 天前
        await SeedBuddyAsync(48203, meId, BuddyStatus.Pending, now.AddDays(-1), now.AddDays(6)); // 小刚 1 天前
        await SeedMemberAsync(1, 48202, MemberRole.Student, "小红");
        await SeedMemberAsync(1, 48203, MemberRole.Student, "小刚");
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count);
        // 倒序：小刚（1 天前）在前，小红（3 天前）在后
        Assert.Equal(48203, result.Items[0].InviterUserId);
        Assert.Equal("小刚", result.Items[0].Nickname);
        Assert.Equal(48202, result.Items[1].InviterUserId);
        Assert.Equal("小红", result.Items[1].Nickname);
    }

    /// <summary>BR-35 空列表：无 Pending → items=[]（非错误）</summary>
    [Fact]
    public async Task ExecuteAsync_NoPendingInvites_ReturnsEmpty()
    {
        SetUser(48210);
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-35 过期过滤：ExpiresAt &lt; now 不返回（惰性过期不落库）</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredInvites_FilteredOut()
    {
        var meId = SetUser(48220);
        var now = DateTime.UtcNow;
        await SeedBuddyAsync(48221, meId, BuddyStatus.Pending, now.AddDays(-10), now.AddDays(-3)); // 已过期
        await SeedBuddyAsync(48222, meId, BuddyStatus.Pending, now.AddDays(-1), now.AddDays(6));  // 未过期
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Equal(48222, result.Items[0].InviterUserId); // 仅未过期返回
    }

    /// <summary>BR-35 RLS：他人为 invitee 的邀请不可见（仅返回 InviteeId=me）</summary>
    [Fact]
    public async Task ExecuteAsync_OthersInvites_NotVisible()
    {
        var meId = SetUser(48230);
        await SeedBuddyAsync(48231, meId, BuddyStatus.Pending);     // 给我的 → 可见
        await SeedBuddyAsync(48232, 48233, BuddyStatus.Pending);    // 别人的 → 不可见
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Equal(48231, result.Items[0].InviterUserId);
    }

    /// <summary>BR-36 状态过滤：Accepted/Rejected/Removed 不返回（仅 Pending）</summary>
    [Fact]
    public async Task ExecuteAsync_NonPendingStatuses_FilteredOut()
    {
        var meId = SetUser(48240);
        await SeedBuddyAsync(48241, meId, BuddyStatus.Accepted);
        await SeedBuddyAsync(48242, meId, BuddyStatus.Rejected);
        await SeedBuddyAsync(48243, meId, BuddyStatus.Removed);
        await SeedBuddyAsync(48244, meId, BuddyStatus.Pending);
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Equal(48244, result.Items[0].InviterUserId);
    }

    /// <summary>BR-36 只读：查询无副作用（Status/InvitedAt/ExpiresAt 均不变）</summary>
    [Fact]
    public async Task ExecuteAsync_ReadOnly_NoSideEffects()
    {
        var meId = SetUser(48250);
        var now = DateTime.UtcNow;
        await SeedBuddyAsync(48251, meId, BuddyStatus.Pending, now.AddDays(-2), now.AddDays(5));
        var ds = User.Use<StudyBuddiesDataService>();
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);
        var after = await ds.EntityGetAsync(x => x.InviteeId == meId, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.NotNull(after);
        Assert.Equal(BuddyStatus.Pending, after.Status); // 查询不改变状态
        Assert.Equal(now.AddDays(-2), after.InvitedAt);
        Assert.Equal(now.AddDays(5), after.ExpiresAt);
    }

    /// <summary>BR-36 昵称兜底：邀请人无群成员记录 / Nickname=null → 学生{userId}</summary>
    [Fact]
    public async Task ExecuteAsync_NicknameFallback_WhenNoMemberRecord()
    {
        var meId = SetUser(48260);
        await SeedBuddyAsync(48261, meId, BuddyStatus.Pending); // 48261 无 GroupMembers 记录（退群/跨群邀请）
        await SeedMemberAsync(1, 48262, MemberRole.Student, null); // Nickname=null
        await SeedBuddyAsync(48262, meId, BuddyStatus.Pending);
        var svc = User.Use<ListPendingBuddyInvitesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.InviterUserId == 48261 && i.Nickname == "学生48261");
        Assert.Contains(result.Items, i => i.InviterUserId == 48262 && i.Nickname == "学生48262");
    }
}
