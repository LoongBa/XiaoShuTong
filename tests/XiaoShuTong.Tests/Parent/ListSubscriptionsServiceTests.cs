using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.5 我的订阅列表（ListSubscriptionsService）Contract 测试
/// 覆盖 BR：BR-11 无订阅空列表 | BR-12 仅当前家长（RLS）+ Plan/Status/TrialEndAt 透传
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListSubscriptionsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Subscriptions> SeedSubscriptionAsync(long parentId, long studentId, SubscriptionPlan plan, SubscriptionStatus status, DateTime? trialEndAt = null)
    {
        var ds = User.Use<SubscriptionsDataService>();
        return await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = plan,
            Status = status,
            TrialEndAt = trialEndAt,
            PeriodEndAt = status == SubscriptionStatus.Trialing ? null : DateTime.UtcNow.AddDays(30),
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

    /// <summary>BR-11：无订阅 → 空列表 + Success</summary>
    [Fact]
    public async Task Execute_NoSubscriptions_ReturnsEmpty()
    {
        SetUser(48601);
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-12：仅当前家长的订阅（RLS）+ Plan/Status 透传</summary>
    [Fact]
    public async Task Execute_OnlyOwnSubscriptions()
    {
        var parentId = SetUser(48602);
        var own = await SeedSubscriptionAsync(parentId, 48711, SubscriptionPlan.Month, SubscriptionStatus.Active);
        await SeedSubscriptionAsync(489904, 48712, SubscriptionPlan.Year, SubscriptionStatus.Active); // 他人家长订阅
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(own.UId, item.SubscriptionUid);
        Assert.Equal("48711", item.StudentUid);
        Assert.Equal("Month", item.Plan);
        Assert.Equal("Active", item.Status);
    }

    /// <summary>试用中订阅透传：Trialing + TrialEndAt</summary>
    [Fact]
    public async Task Execute_TrialSubscription_PassesThroughTrialEndAt()
    {
        var parentId = SetUser(48603);
        var trialEnd = DateTime.UtcNow.AddDays(7);
        await SeedSubscriptionAsync(parentId, 48721, SubscriptionPlan.Month, SubscriptionStatus.Trialing, trialEnd);
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("Trialing", item.Status);
        Assert.Equal(trialEnd, item.TrialEndAt);
    }

    /// <summary>孩子昵称富化：孩子有 GroupMembers 记录（Role=Student）→ 富化昵称</summary>
    [Fact]
    public async Task Execute_StudentNickname_Enriched()
    {
        var parentId = SetUser(48604);
        await SeedSubscriptionAsync(parentId, 48731, SubscriptionPlan.Month, SubscriptionStatus.Active);
        await SeedMemberAsync(1, 48731, MemberRole.Student, "小明");
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("小明", item.StudentNickname);
    }

    /// <summary>孩子昵称兜底：孩子无 GroupMembers 记录 / Nickname=null → 学生{userId}</summary>
    [Fact]
    public async Task Execute_StudentNickname_FallbackWhenNoMember()
    {
        var parentId = SetUser(48605);
        await SeedSubscriptionAsync(parentId, 48741, SubscriptionPlan.Month, SubscriptionStatus.Active); // 无 GroupMembers 记录
        await SeedSubscriptionAsync(parentId, 48742, SubscriptionPlan.Month, SubscriptionStatus.Active); // Nickname=null
        await SeedMemberAsync(1, 48742, MemberRole.Student, null);
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.StudentUid == "48741" && i.StudentNickname == "学生48741");
        Assert.Contains(result.Items, i => i.StudentUid == "48742" && i.StudentNickname == "学生48742");
    }

    /// <summary>Oracle C1：Role=Parent 的 GroupMembers 记录不参与昵称富化（防 Role 歧义）</summary>
    [Fact]
    public async Task Execute_StudentNickname_IgnoresParentRole()
    {
        var parentId = SetUser(48606);
        await SeedSubscriptionAsync(parentId, 48751, SubscriptionPlan.Month, SubscriptionStatus.Active);
        await SeedMemberAsync(1, 48751, MemberRole.Parent, "家长昵称"); // 仅 Parent 角色
        var svc = User.Use<ListSubscriptionsService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("学生48751", item.StudentNickname); // Parent 角色不富化 → 兜底
    }
}
