using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.4 配置群组排名开关（SetRankEnabledService）Contract 测试
/// 覆盖 BR：BR-12 开关默认 true（Req 默认值）、关闭/开启双向持久化 | BR-07 群组不存在 → GROUP_NOT_FOUND | BR-08 非群主操作他人群组 → GROUP_NOT_FOUND（不泄露存在性）| BR-13 战力榜不受影响（切片不校验，仅验证开关变更不涉及战力榜数据）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class SetRankEnabledServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
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
            OwnerId = ownerId,
            Name = name,
            Subject = "history",
            Status = GroupStatus.Active,
            RankEnabled = rankEnabled,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-12：群主关闭开关 → 成功 + 落库持久化（再次读取已更新）</summary>
    [Fact]
    public async Task ExecuteAsync_TurnOff_SavesRankEnabled()
    {
        var ownerId = SetUser(56101);
        var group = await SeedGroupAsync(ownerId, "战绩榜班", rankEnabled: true);
        var svc = User.Use<SetRankEnabledService>();

        var result = await svc.ExecuteAsync(new SetRankEnabledReqDto { GroupId = group.Id, RankEnabled = false }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
        Assert.Equal(group.Id, result.GroupId);
        Assert.False(result.RankEnabled);

        // 落库验证：再次读取同一群组，RankEnabled=false
        var ds = User.Use<GroupsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == group.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.False(updated.RankEnabled);
    }

    /// <summary>BR-12 反向：Req 默认 RankEnabled=true，关闭后再用默认值开启 → 成功并持久化（开关双向可切换）</summary>
    [Fact]
    public async Task ExecuteAsync_TurnOnAgain_WithDefaultTrue_Restores()
    {
        var ownerId = SetUser(56102);
        var group = await SeedGroupAsync(ownerId, "恢复班", rankEnabled: false);
        var svc = User.Use<SetRankEnabledService>();

        // RankEnabled 未指定 → 使用 Req 默认值 true（BR-12 开关默认 true）
        var result = await svc.ExecuteAsync(new SetRankEnabledReqDto { GroupId = group.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.RankEnabled);

        var ds = User.Use<GroupsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == group.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.True(updated.RankEnabled);
    }

    /// <summary>BR-13：开关关闭不影响群组成员/战力榜基础数据（切片仅验证开关字段变更，不触碰成员数据）</summary>
    [Fact]
    public async Task ExecuteAsync_TurnOff_KeepsMembersUntouched()
    {
        var ownerId = SetUser(56103);
        var group = await SeedGroupAsync(ownerId, "战力榜班", rankEnabled: true);
        var membersDs = User.Use<GroupMembersDataService>();
        await membersDs.EntityCreateAsync(new GroupMembers
        {
            GroupId = group.Id,
            UserId = 56113,
            Role = MemberRole.Student,
            Nickname = "小战",
            JoinedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
        var svc = User.Use<SetRankEnabledService>();

        var result = await svc.ExecuteAsync(new SetRankEnabledReqDto { GroupId = group.Id, RankEnabled = false }, TestContext.Current.CancellationToken);
        Assert.True(result.Success);

        // 成员行未受影响
        var cnt = await membersDs.CountAsync(x => x.GroupId == group.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, cnt);
    }

    /// <summary>BR-07：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_GroupNotFound_ReturnsGroupNotFound()
    {
        SetUser(56104);
        var svc = User.Use<SetRankEnabledService>();

        var result = await svc.ExecuteAsync(new SetRankEnabledReqDto { GroupId = 99999901, RankEnabled = false }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-08：非群主操作他人群组 → GROUP_NOT_FOUND（Owner 校验，不泄露群组存在性）</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsGroupNotFound()
    {
        var group = await SeedGroupAsync(56105, "他人群组");
        SetUser(56106); // 另一用户（非该群组 Owner）
        var svc = User.Use<SetRankEnabledService>();

        var result = await svc.ExecuteAsync(new SetRankEnabledReqDto { GroupId = group.Id, RankEnabled = false }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }
}