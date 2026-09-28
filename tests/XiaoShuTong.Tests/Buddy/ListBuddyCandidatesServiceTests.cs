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
/// UC-8.1 配合：可邀搭子候选列表（ListBuddyCandidatesService）Contract 测试
/// 覆盖搭子-BR-33：同群/同年级（OR）学生成员候选 | 排除自己 | 排除已有搭子（Pending/Accepted）| 排除 Parent | 多群/跨群去重
/// 搭子-BR-34：只读查询（当前用户群成员身份即可）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListBuddyCandidatesServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string groupName, string grade = "七年级")
    {
        var ds = User.Use<GroupsDataService>();
        var group = await ds.EntityCreateAsync(new Groups
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            Name = groupName,
            Subject = "chinese",
            Grade = grade,
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);
        return group;
    }

    private async Task SeedMemberAsync(long groupId, long userId, MemberRole role, string nickname)
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

    private async Task SeedBuddyAsync(long inviterId, long inviteeId, BuddyStatus status)
    {
        var ds = User.Use<StudyBuddiesDataService>();
        await ds.EntityCreateAsync(new StudyBuddies
        {
            UId = UidGenerator.NewId(),
            InviterId = inviterId,
            InviteeId = inviteeId,
            Status = status,
            InvitedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>种子过期 Pending 邀请（ExpiresAt 过去，惰性过期语义——Status 保留 Pending）</summary>
    private async Task SeedExpiredPendingBuddyAsync(long inviterId, long inviteeId)
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

    /// <summary>BR-33 主路径：同群学生成员入候选（昵称 + 群名）</summary>
    [Fact]
    public async Task ExecuteAsync_SameGroupStudents_ReturnsCandidates()
    {
        var meId = SetUser(48101);
        var group = await SeedGroupAsync(999981, "七(1)班");
        await SeedMemberAsync(group.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(group.Id, 48102, MemberRole.Student, "小红");
        await SeedMemberAsync(group.Id, 48103, MemberRole.Student, "小刚");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, c => Assert.Equal("七(1)班", c.GroupName));
        Assert.Contains(result.Items, c => c.UserId == 48102 && c.Nickname == "小红");
        Assert.Contains(result.Items, c => c.UserId == 48103 && c.Nickname == "小刚");
    }

    /// <summary>BR-33 排除自己</summary>
    [Fact]
    public async Task ExecuteAsync_ExcludesSelf()
    {
        var meId = SetUser(48110);
        var group = await SeedGroupAsync(999982, "七(2)班");
        await SeedMemberAsync(group.Id, meId, MemberRole.Student, "小明");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Items); // 仅自己 → 无候选
    }

    /// <summary>BR-33 排除已有搭子（Pending 与 Accepted 均排除）</summary>
    [Fact]
    public async Task ExecuteAsync_ExcludesExistingBuddyRelations()
    {
        var meId = SetUser(48120);
        var group = await SeedGroupAsync(999983, "七(3)班");
        await SeedMemberAsync(group.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(group.Id, 48121, MemberRole.Student, "小红");   // 已接受搭子 → 排除
        await SeedMemberAsync(group.Id, 48122, MemberRole.Student, "小刚");   // 已邀请 Pending → 排除
        await SeedMemberAsync(group.Id, 48123, MemberRole.Student, "小丽");   // 可邀
        await SeedBuddyAsync(meId, 48121, BuddyStatus.Accepted);
        await SeedBuddyAsync(meId, 48122, BuddyStatus.Pending);
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var candidateIds = result.Items.Select(c => c.UserId).ToArray();
        Assert.DoesNotContain(48121, candidateIds); // Accepted 排除
        Assert.DoesNotContain(48122, candidateIds); // Pending 排除
        Assert.Contains(48123, candidateIds);        // 可邀保留
    }

    /// <summary>BR-33 排除 Parent 角色</summary>
    [Fact]
    public async Task ExecuteAsync_ExcludesParentRole()
    {
        var meId = SetUser(48130);
        var group = await SeedGroupAsync(999984, "七(4)班");
        await SeedMemberAsync(group.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(group.Id, 48131, MemberRole.Parent, "小红妈妈"); // Parent → 排除
        await SeedMemberAsync(group.Id, 48132, MemberRole.Student, "小刚");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Equal(48132, result.Items[0].UserId);
    }

    /// <summary>BR-33 多群同人去重（GROUP BY UserId）</summary>
    [Fact]
    public async Task ExecuteAsync_MultiGroupSameUser_Deduplicated()
    {
        var meId = SetUser(48140);
        var g1 = await SeedGroupAsync(999985, "语文班");
        var g2 = await SeedGroupAsync(999986, "数学班");
        await SeedMemberAsync(g1.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(g2.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(g1.Id, 48141, MemberRole.Student, "小红"); // 同人两群
        await SeedMemberAsync(g2.Id, 48141, MemberRole.Student, "小红");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items); // 同人去重
        Assert.Equal(48141, result.Items[0].UserId);
    }

    /// <summary>BR-33 无群 → 空候选（非错误）</summary>
    [Fact]
    public async Task ExecuteAsync_NoGroups_ReturnsEmpty()
    {
        SetUser(48150);
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }

    /// <summary>BR-34 只读：非群主学生可查询（区别于成员管理 BR-08 群主专属）</summary>
    [Fact]
    public async Task ExecuteAsync_NonOwnerStudent_CanQuery()
    {
        var meId = SetUser(48160);
        var group = await SeedGroupAsync(999988, "七(6)班"); // owner 999988 ≠ me，但我是成员
        await SeedMemberAsync(group.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(group.Id, 48161, MemberRole.Student, "小红");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success); // 非群主成员身份即可查（BR-34）
        Assert.Single(result.Items);
        Assert.Equal(48161, result.Items[0].UserId);
    }

    /// <summary>BR-33 修正：过期 Pending 恢复候选（惰性过期豁免，V0.6.19）</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredPending_RestoresCandidate()
    {
        var meId = SetUser(48170);
        var group = await SeedGroupAsync(999989, "七(7)班");
        await SeedMemberAsync(group.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(group.Id, 48171, MemberRole.Student, "小红");   // 过期 Pending → 恢复候选
        await SeedMemberAsync(group.Id, 48172, MemberRole.Student, "小刚");   // 未过期 Pending → 仍排除
        await SeedMemberAsync(group.Id, 48173, MemberRole.Student, "小丽");   // 可邀
        await SeedExpiredPendingBuddyAsync(meId, 48171); // 过期 Pending（Status 仍 Pending）
        await SeedBuddyAsync(meId, 48172, BuddyStatus.Pending); // 未过期 Pending
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

var candidateIds = result.Items.Select(c => c.UserId).ToArray();
        Assert.Contains(48171, candidateIds); // 过期 Pending 恢复候选
        Assert.DoesNotContain(48172, candidateIds); // 未过期 Pending 仍排除
        Assert.Contains(48173, candidateIds);        // 可邀保留
    }

    /// <summary>BR-33 扩展：同年级跨群候选（OR 对齐 BR-16）——同群回归 + 同年级异群候选带群名 + 非同群非同年级不出现</summary>
    [Fact]
    public async Task ExecuteAsync_SameGradeCrossGroup_ReturnsCandidates()
    {
        var meId = SetUser(48301);
        var g1 = await SeedGroupAsync(999901, "七(1)班", "七年级");
        var g2 = await SeedGroupAsync(999902, "七(2)班", "七年级"); // 同年级异群
        var g3 = await SeedGroupAsync(999903, "八(1)班", "八年级"); // 非同群非同年级
        await SeedMemberAsync(g1.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(g1.Id, 48302, MemberRole.Student, "同群小红");
        await SeedMemberAsync(g2.Id, 48303, MemberRole.Student, "跨群小刚");
        await SeedMemberAsync(g3.Id, 48304, MemberRole.Student, "跨年级小丽");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Contains(result.Items, c => c.UserId == 48302); // 同群候选回归
        var cross = result.Items.Single(c => c.UserId == 48303); // 同年级跨群候选
        Assert.Equal("跨群小刚", cross.Nickname);
        Assert.Equal("七(2)班", cross.GroupName); // 跨群候选带群名
        Assert.DoesNotContain(result.Items, c => c.UserId == 48304); // 非同群非同年级不出现
    }

    /// <summary>BR-33 扩展：已有关系排除跨群同样生效（跨群 Accepted 排除，跨群可邀保留，同群回归）</summary>
    [Fact]
    public async Task ExecuteAsync_CrossGroupAcceptedRelation_Excluded()
    {
        var meId = SetUser(48310);
        var g1 = await SeedGroupAsync(999904, "七(3)班", "七年级");
        var g2 = await SeedGroupAsync(999905, "七(4)班", "七年级"); // 同年级异群
        await SeedMemberAsync(g1.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(g1.Id, 48311, MemberRole.Student, "同群小刚");
        await SeedMemberAsync(g2.Id, 48312, MemberRole.Student, "跨群小红"); // Accepted → 跨群排除
        await SeedMemberAsync(g2.Id, 48313, MemberRole.Student, "跨群小丽"); // 可邀保留
        await SeedBuddyAsync(meId, 48312, BuddyStatus.Accepted);
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var candidateIds = result.Items.Select(c => c.UserId).ToArray();
        Assert.Contains(48311, candidateIds);        // 同群候选回归
        Assert.DoesNotContain(48312, candidateIds); // 跨群 Accepted 排除
        Assert.Contains(48313, candidateIds);        // 跨群可邀保留
    }

    /// <summary>BR-33 扩展：多群同人去重跨群合并（同人在同群 + 同年级异群 → 单条候选）</summary>
    [Fact]
    public async Task ExecuteAsync_UserInOwnAndGradeGroup_Deduplicated()
    {
        var meId = SetUser(48320);
        var g1 = await SeedGroupAsync(999906, "七(5)班", "七年级");
        var g2 = await SeedGroupAsync(999907, "七(6)班", "七年级"); // 同年级异群
        await SeedMemberAsync(g1.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(g1.Id, 48321, MemberRole.Student, "小红"); // 同群成员
        await SeedMemberAsync(g2.Id, 48321, MemberRole.Student, "小红"); // 同人跨群 → 合并去重
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items); // 跨群同人合并去重
        Assert.Equal(48321, result.Items[0].UserId);
    }

    /// <summary>BR-33 修订（2026-09-29）：同年级跨群候选出现在列表（另一群 Grade 相同的学生，含 GroupName）</summary>
    [Fact]
    public async Task ExecuteAsync_SameGradeCrossGroup_ReturnsCandidate()
    {
        var meId = SetUser(48201);
        var myGroup = await SeedGroupAsync(999101, "七(1)班");      // 我所在群（七年级）
        var crossGroup = await SeedGroupAsync(999102, "七(2)班", "七年级"); // 同年级跨群
        await SeedMemberAsync(myGroup.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(crossGroup.Id, 48202, MemberRole.Student, "小红"); // 跨群同年级
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal(48202, result.Items[0].UserId);
        Assert.Equal("七(2)班", result.Items[0].GroupName); // 跨群候选携带其所在群名
    }

    /// <summary>BR-33 回归：同群候选仍出现（与同年级候选并存）</summary>
    [Fact]
    public async Task ExecuteAsync_SameGroupAndCrossGrade_BothReturned_Regression()
    {
        var meId = SetUser(48210);
        var myGroup = await SeedGroupAsync(999103, "七(3)班");
        var crossGroup = await SeedGroupAsync(999104, "七(4)班", "七年级");
        await SeedMemberAsync(myGroup.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(myGroup.Id, 48211, MemberRole.Student, "小刚");       // 同群候选（回归）
        await SeedMemberAsync(crossGroup.Id, 48212, MemberRole.Student, "小丽");    // 同年级跨群候选
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var candidateIds = result.Items.Select(c => c.UserId).ToArray();
        Assert.Contains(48211, candidateIds); // 同群候选仍出现
        Assert.Contains(48212, candidateIds); // 同年级跨群亦出现
        Assert.Equal(2, result.Items.Count);
    }

    /// <summary>BR-33：非同群且非同年级不出现</summary>
    [Fact]
    public async Task ExecuteAsync_NotSameGroupNotSameGrade_Excluded()
    {
        var meId = SetUser(48220);
        var myGroup = await SeedGroupAsync(999105, "七(5)班");
        var otherGradeGroup = await SeedGroupAsync(999106, "八(5)班", "八年级");
        await SeedMemberAsync(myGroup.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(otherGradeGroup.Id, 48221, MemberRole.Student, "小刚"); // 非同群 + 非同年级
        var emptyGradeGroup = await SeedGroupAsync(999107, "九(1)班", "九年级");
        await SeedMemberAsync(emptyGradeGroup.Id, 48222, MemberRole.Student, "小丽");
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items); // 我群内无其他学生成员 → 非同群非同年级者不入列
    }

    /// <summary>BR-33：已有关系排除跨群同样生效（Accepted 跨群候选被排除）</summary>
    [Fact]
    public async Task ExecuteAsync_AcceptedRelation_ExcludesCrossGroupCandidate()
    {
        var meId = SetUser(48230);
        var myGroup = await SeedGroupAsync(999108, "七(6)班");
        var crossGroup = await SeedGroupAsync(999109, "七(7)班", "七年级");
        await SeedMemberAsync(myGroup.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(crossGroup.Id, 48231, MemberRole.Student, "小红"); // 跨群同年级
        await SeedMemberAsync(crossGroup.Id, 48232, MemberRole.Student, "小刚"); // 跨群同年级（可邀）
        await SeedBuddyAsync(meId, 48231, BuddyStatus.Accepted); // 跨群 Accepted → 排除
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        var candidateIds = result.Items.Select(c => c.UserId).ToArray();
        Assert.DoesNotContain(48231, candidateIds); // Accepted 跨群候选排除
        Assert.Contains(48232, candidateIds);        // 可邀跨群候选保留
    }

    /// <summary>BR-33：多群同人去重跨群合并（同人跨多个同年级群仅一条）</summary>
    [Fact]
    public async Task ExecuteAsync_CrossGroupSameUser_Deduplicated()
    {
        var meId = SetUser(48240);
        var myGroup = await SeedGroupAsync(999110, "七(8)班");
        var g1 = await SeedGroupAsync(999111, "语文加强班", "七年级");
        var g2 = await SeedGroupAsync(999112, "数学加强班", "七年级");
        await SeedMemberAsync(myGroup.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(g1.Id, 48241, MemberRole.Student, "小红"); // 跨群同人（语文加强班）
        await SeedMemberAsync(g2.Id, 48241, MemberRole.Student, "小红"); // 跨群同人（数学加强班）
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items); // 跨群同人去重合并
        Assert.Equal(48241, result.Items[0].UserId);
    }

    /// <summary>BR-33：跨群候选的群名/组来源正确标注（GroupId + GroupName 指向其所在同年级群）</summary>
    [Fact]
    public async Task ExecuteAsync_CrossGroupCandidate_GroupSourceMarked()
    {
        var meId = SetUser(48250);
        var myGroup = await SeedGroupAsync(999113, "七(9)班");
        var crossGroup = await SeedGroupAsync(999114, "七(10)班", "七年级");
        await SeedMemberAsync(myGroup.Id, meId, MemberRole.Student, "小明");
        await SeedMemberAsync(crossGroup.Id, 48251, MemberRole.Student, "小红"); // 仅在同年级跨群
        var svc = User.Use<ListBuddyCandidatesService>();

        var result = await svc.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        var candidate = result.Items[0];
        Assert.Equal(48251, candidate.UserId);
        Assert.Equal(crossGroup.Id, candidate.GroupId);   // 组来源 = 其所在同年级群
        Assert.Equal("七(10)班", candidate.GroupName);     // 群名 = 其所在同年级群
    }
}