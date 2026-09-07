using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.3 群组详情与成员管理（ManageGroupMembersService）Contract 测试
/// 覆盖 BR：BR-07 群组不存在 | BR-08 仅群主可管理 | BR-09 非本群成员 → 5003 | BR-10 移除不删学习数据（仅删成员行）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ManageGroupMembersServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string name)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            OwnerId = ownerId,
            Name = name,
            Subject = "history",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<GroupMembers> SeedMemberAsync(long groupId, long userId, MemberRole role, string? nickname = null)
    {
        var ds = User.Use<GroupMembersDataService>();
        return await ds.EntityCreateAsync(new GroupMembers
        {
            GroupId = groupId,
            UserId = userId,
            Role = role,
            Nickname = nickname,
            JoinedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：查询成员列表（角色/昵称/加入时间）</summary>
    [Fact]
    public async Task GetMembers_ValidGroup_ReturnsMembers()
    {
        var ownerId = SetUser(63001);
        var group = await SeedGroupAsync(ownerId, $"群组{63001}");
        await SeedMemberAsync(group.Id, 63011, MemberRole.Student, "小张");
        await SeedMemberAsync(group.Id, 63012, MemberRole.Parent, "小张妈妈");
        var svc = User.Use<ManageGroupMembersService>();

        var result = await svc.ExecuteAsync(new GetMembersReqDto { GroupId = group.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.UserId == 63011 && m.Role == "Student" && m.Nickname == "小张");
        Assert.Contains(result.Members, m => m.UserId == 63012 && m.Role == "Parent");
    }

    /// <summary>BR-07：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task GetMembers_GroupNotFound_ReturnsGroupNotFound()
    {
        SetUser(63002);
        var svc = User.Use<ManageGroupMembersService>();

        var result = await svc.ExecuteAsync(new GetMembersReqDto { GroupId = 99999901 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-08：非群主访问他人群组 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task GetMembers_NotOwner_ReturnsGroupNotFound()
    {
        var group = await SeedGroupAsync(63003, "他人群组");
        SetUser(63004); // 另一个用户
        var svc = User.Use<ManageGroupMembersService>();

        var result = await svc.ExecuteAsync(new GetMembersReqDto { GroupId = group.Id }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>主流程 + BR-10：移除成员成功（仅删成员行，群组仍在）</summary>
    [Fact]
    public async Task RemoveMember_ValidMember_RemovesRowOnly()
    {
        var ownerId = SetUser(63005);
        var group = await SeedGroupAsync(ownerId, $"群组{63005}");
        await SeedMemberAsync(group.Id, 63015, MemberRole.Student, "小张");
        var svc = User.Use<ManageGroupMembersService>();

        var result = await svc.ExecuteAsync(new RemoveMemberReqDto { GroupId = group.Id, UserId = 63015 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.Removed);

        // 成员行已删
        var membersDs = User.Use<GroupMembersDataService>();
        var remaining = await membersDs.EntitySelectAsync(x => x.GroupId == group.Id, ct: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(remaining, m => m.UserId == 63015);

        // 群组仍存在（学习数据在模块 2，本切片只验证群组不被误删）
        var groupsDs = User.Use<GroupsDataService>();
        var groupStill = await groupsDs.EntityGetAsync(x => x.Id == group.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(groupStill);
    }

    /// <summary>BR-09：移除非本群成员 → NOT_GROUP_MEMBER</summary>
    [Fact]
    public async Task RemoveMember_NotInGroup_ReturnsNotGroupMember()
    {
        var ownerId = SetUser(63006);
        var group = await SeedGroupAsync(ownerId, $"群组{63006}");
        var svc = User.Use<ManageGroupMembersService>();

        var result = await svc.ExecuteAsync(new RemoveMemberReqDto { GroupId = group.Id, UserId = 99999902 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.NotGroupMember, result.ErrorCode);
    }

    /// <summary>BR-07：移除目标群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task RemoveMember_GroupNotFound_ReturnsGroupNotFound()
    {
        SetUser(63007);
        var svc = User.Use<ManageGroupMembersService>();

        var result = await svc.ExecuteAsync(new RemoveMemberReqDto { GroupId = 99999903, UserId = 63017 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }
}
