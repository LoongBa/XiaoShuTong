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
/// UC-10.2 我的孩子列表（ListChildrenService）Contract 测试
/// 覆盖 BR：BR-03 无关联孩子空列表 | BR-04 仅当前家长的孩子（RLS）+ HasSubscription 派生
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListChildrenServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedRelationAsync(long parentId, long studentId)
    {
        var ds = User.Use<ParentStudentRelationsDataService>();
        await ds.EntityCreateAsync(new ParentStudentRelations
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Relation = ParentRelation.Parent,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedSubscriptionAsync(long parentId, long studentId, SubscriptionStatus status)
    {
        var ds = User.Use<SubscriptionsDataService>();
        await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = SubscriptionPlan.Month,
            Status = status,
            TrialEndAt = null,
            PeriodEndAt = DateTime.UtcNow.AddDays(30),
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Groups> SeedGroupAsync(string name)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            UId = UidGenerator.NewId(),
            OwnerId = 48400,
            Name = name,
            Subject = "chinese",
            Status = GroupStatus.Active,
            RankEnabled = true,
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

    /// <summary>BR-03：无关联孩子 → 空列表 + Success</summary>
    [Fact]
    public async Task Execute_NoChildren_ReturnsEmpty()
    {
        SetUser(48401);
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-04：仅返回当前家长的孩子（RLS），他人孩子不可见</summary>
    [Fact]
    public async Task Execute_OnlyOwnChildren()
    {
        var parentId = SetUser(48402);
        await SeedRelationAsync(parentId, 48511);
        await SeedRelationAsync(489903, 48512); // 他人家长的孩子
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.Equal("48511", item.StudentUid);
        Assert.False(item.HasSubscription); // 无订阅
    }

    /// <summary>HasSubscription 派生：存在非过期订阅 → true</summary>
    [Fact]
    public async Task Execute_HasSubscription_TrueWhenActive()
    {
        var parentId = SetUser(48403);
        await SeedRelationAsync(parentId, 48521);
        await SeedSubscriptionAsync(parentId, 48521, SubscriptionStatus.Active);
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.True(item.HasSubscription);
    }

    /// <summary>HasSubscription 派生：仅过期订阅 → false（Expired 不计入）</summary>
    [Fact]
    public async Task Execute_HasSubscription_FalseWhenExpired()
    {
        var parentId = SetUser(48404);
        await SeedRelationAsync(parentId, 48531);
        await SeedSubscriptionAsync(parentId, 48531, SubscriptionStatus.Expired);
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.False(item.HasSubscription);
    }

    /// <summary>昵称 + 班级富化：孩子有 GroupMembers（Role=Student）+ 群组 → 富化昵称与班级</summary>
    [Fact]
    public async Task Execute_NicknameAndClassName_Enriched()
    {
        var parentId = SetUser(48405);
        await SeedRelationAsync(parentId, 48541);
        var group = await SeedGroupAsync("七(3)班");
        await SeedMemberAsync(group.Id, 48541, MemberRole.Student, "小明");
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("小明", item.Nickname);
        Assert.Equal("七(3)班", item.ClassName);
    }

    /// <summary>昵称/班级兜底：孩子无 GroupMembers 记录 → 学生{userId} + 空班级</summary>
    [Fact]
    public async Task Execute_NicknameAndClassName_FallbackWhenNoMember()
    {
        var parentId = SetUser(48406);
        await SeedRelationAsync(parentId, 48551); // 无 GroupMembers 记录
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("学生48551", item.Nickname);
        Assert.Equal(string.Empty, item.ClassName);
    }

    /// <summary>Oracle C1：Role=Parent 的 GroupMembers 记录不参与昵称/班级富化（防 Role 歧义）</summary>
    [Fact]
    public async Task Execute_NicknameAndClassName_IgnoresParentRole()
    {
        var parentId = SetUser(48407);
        await SeedRelationAsync(parentId, 48561);
        var group = await SeedGroupAsync("七(3)班");
        await SeedMemberAsync(group.Id, 48561, MemberRole.Parent, "家长昵称"); // 仅 Parent 角色
        var svc = User.Use<ListChildrenService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("学生48561", item.Nickname); // Parent 角色不富化 → 兜底
        Assert.Equal(string.Empty, item.ClassName);
    }
}
