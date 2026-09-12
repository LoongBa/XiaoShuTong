using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.2 查询我的群组列表（ListGroupsService）Contract 测试
/// 覆盖 BR：BR-06 仅返回当前群主的群组（不含他人群组） | BR-07 无群组/列表场景 → 空列表 Success=true（列表接口返回空态，1003 属单资源 UC）| 成员数聚合 + 字段映射 + 分页参数归一（PageIndex&lt;1→1、PageSize 越界回落 20）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListGroupsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string name, bool rankEnabled = true)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            Name = name,
            Subject = "history",
            Status = GroupStatus.Active,
            RankEnabled = rankEnabled,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<GroupMembers> SeedMemberAsync(long groupId, long userId)
    {
        var ds = User.Use<GroupMembersDataService>();
        return await ds.EntityCreateAsync(new GroupMembers
        {
            UId = UidGenerator.NewId(),
            GroupId = groupId,
            UserId = userId,
            Role = MemberRole.Student,
            JoinedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：群主有 2 个群组 → 全部返回 + 成员数聚合 + RankEnabled/ExecutionRate 字段映射正确</summary>
    [Fact]
    public async Task ExecuteAsync_OwnerGroups_ReturnsAllWithFields()
    {
        var ownerId = SetUser(55101);
        var g1 = await SeedGroupAsync(ownerId, "语文班");
        var g2 = await SeedGroupAsync(ownerId, "数学班", rankEnabled: false);
        await SeedMemberAsync(g1.Id, 55111);
        await SeedMemberAsync(g1.Id, 55112);
        await SeedMemberAsync(g2.Id, 55113);
        var svc = User.Use<ListGroupsService>();

        var result = await svc.ExecuteAsync(new ListGroupsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        var item1 = Assert.Single(result.Items, i => i.GroupId == g1.Id);
        Assert.Equal("语文班", item1.Name);
        Assert.Equal("history", item1.Subject);
        Assert.Equal(2, item1.MemberCount);
        Assert.True(item1.RankEnabled);
        var item2 = Assert.Single(result.Items, i => i.GroupId == g2.Id);
        Assert.Equal("数学班", item2.Name);
        Assert.Equal(1, item2.MemberCount);
        Assert.False(item2.RankEnabled);
        // ExecutionRate 依赖模块 2（学习 Session 域），切片恒 0
        Assert.All(result.Items, i => Assert.Equal(0, i.ExecutionRate));
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(20, result.PageSize);
    }

    /// <summary>BR-06：仅返回当前群主的群组，不含他人群组的群组</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnGroups()
    {
        var ownerId = SetUser(55102);
        var own = await SeedGroupAsync(ownerId, "我的群");
        await SeedGroupAsync(55103, "别人家的群");
        var svc = User.Use<ListGroupsService>();

        var result = await svc.ExecuteAsync(new ListGroupsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal(own.Id, result.Items[0].GroupId);
    }

    /// <summary>BR-07（列表场景）：当前群主无任何群组 → 空列表 Success=true（不报 1003，空态由前端展示「还没有群组」）</summary>
    [Fact]
    public async Task ExecuteAsync_NoGroups_ReturnsEmptyList()
    {
        SetUser(55104);
        var svc = User.Use<ListGroupsService>();

        var result = await svc.ExecuteAsync(new ListGroupsReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>分页：PageIndex&lt;1 归一为 1；PageSize 越界（&lt;1 或 &gt;100）回落默认 20；分页切片生效</summary>
    [Fact]
    public async Task ExecuteAsync_PagingNormalization_SlicesPage()
    {
        var ownerId = SetUser(55105);
        await SeedGroupAsync(ownerId, "群一");
        await SeedGroupAsync(ownerId, "群二");
        var svc = User.Use<ListGroupsService>();

        // PageIndex=0 → 归一 1；PageSize=1 → 只返回 1 条，TotalCount 仍为全量
        var page1 = await svc.ExecuteAsync(new ListGroupsReqDto { PageIndex = 0, PageSize = 1 }, TestContext.Current.CancellationToken);
        Assert.True(page1.Success);
        Assert.Single(page1.Items);
        Assert.Equal(2, page1.TotalCount);
        Assert.Equal(1, page1.PageIndex);
        Assert.Equal(1, page1.PageSize);

        // PageSize=0 → 回落默认 20；PageSize=200 → 回落默认 20
        var zero = await svc.ExecuteAsync(new ListGroupsReqDto { PageSize = 0 }, TestContext.Current.CancellationToken);
        Assert.True(zero.Success);
        Assert.Equal(20, zero.PageSize);
        var over = await svc.ExecuteAsync(new ListGroupsReqDto { PageSize = 200 }, TestContext.Current.CancellationToken);
        Assert.True(over.Success);
        Assert.Equal(20, over.PageSize);
    }
}