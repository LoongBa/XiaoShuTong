using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.10 成员凭码激活加入群组（ActivateMemberService）Contract 测试
/// 覆盖 BR：BR-26 码无效/已用 → 5202 | BR-27 同码重复激活幂等 | BR-28 码过期 → 5202
/// BR-29 后四位不匹配 → 5203 | BR-30 绑定微信 ID（本切片=用户 Id）| BR-31 同群同用户同角色唯一 → 5002 | BR-05 内测开关
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ActivateMemberServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
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

    private async Task<OneTimeInviteCodes> SeedOneTimeCodeAsync(
        long groupId, string code, string phoneLast4,
        OneTimeCodeStatus status = OneTimeCodeStatus.Unused, int daysToExpire = 30)
    {
        var ds = User.Use<OneTimeInviteCodesDataService>();
        return await ds.EntityCreateAsync(new OneTimeInviteCodes
        {
            GroupId = groupId,
            Code = code,
            PhoneLast4 = phoneLast4,
            Status = status,
            GeneratedBy = 999000,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(daysToExpire),
        }, TestContext.Current.CancellationToken);
    }

    private static string NewCode() => $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    /// <summary>主流程：有效码 + 后四位一致 → 激活成功，码置 Used + BoundUserId 落库（BR-26/29/30）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidCodeAndLast4_ActivatesMemberAndBindsCode()
    {
        var userId = SetUser(61001);
        var group = await SeedGroupAsync(999001, "七(3)班");
        var codeStr = NewCode();
        await SeedOneTimeCodeAsync(group.Id, codeStr, "8001");
        var svc = User.Use<ActivateMemberService>();

        var result = await svc.ExecuteAsync(new ActivateMemberReqDto
        {
            OneTimeCode = codeStr,
            PhoneLast4 = "8001",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(group.Id, result.GroupId);
        Assert.Equal("七(3)班", result.GroupName);
        Assert.Equal("Student", result.Role);
        Assert.True(result.MemberId > 0);

        // 成员已写入（InviteCodeId 溯源）
        var membersDs = User.Use<GroupMembersDataService>();
        var member = await membersDs.EntityGetAsync(x => x.Id == result.MemberId, TestContext.Current.CancellationToken);
        Assert.NotNull(member);
        Assert.Equal(group.Id, member.GroupId);
        Assert.Equal(userId, member.UserId);

        // 码已绑定 + 置 Used（BR-30）
        var codesDs = User.Use<OneTimeInviteCodesDataService>();
        var code = await codesDs.EntityGetAsync(x => x.Code == codeStr, TestContext.Current.CancellationToken);
        Assert.NotNull(code);
        Assert.Equal(OneTimeCodeStatus.Used, code.Status);
        Assert.Equal(userId, code.BoundUserId);
        Assert.NotNull(code.UsedAt);
    }

    /// <summary>BR-26：不存在的码 → ONE_TIME_CODE_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownCode_ReturnsOneTimeCodeInvalid()
    {
        SetUser(61002);
        var svc = User.Use<ActivateMemberService>();

        var result = await svc.ExecuteAsync(new ActivateMemberReqDto
        {
            OneTimeCode = "ZZZZZZZZ",
            PhoneLast4 = "8001",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.OneTimeCodeInvalid, result.ErrorCode);
    }

    /// <summary>BR-28：过期码 → ONE_TIME_CODE_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredCode_ReturnsOneTimeCodeInvalid()
    {
        SetUser(61003);
        var group = await SeedGroupAsync(999003, "群组C");
        var codeStr = NewCode();
        await SeedOneTimeCodeAsync(group.Id, codeStr, "8003", daysToExpire: -1);
        var svc = User.Use<ActivateMemberService>();

        var result = await svc.ExecuteAsync(new ActivateMemberReqDto
        {
            OneTimeCode = codeStr,
            PhoneLast4 = "8003",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.OneTimeCodeInvalid, result.ErrorCode);
    }

    /// <summary>BR-29：后四位不匹配 → PHONE_LAST4_MISMATCH</summary>
    [Fact]
    public async Task ExecuteAsync_Last4Mismatch_ReturnsPhoneLast4Mismatch()
    {
        SetUser(61004);
        var group = await SeedGroupAsync(999004, "群组D");
        var codeStr = NewCode();
        await SeedOneTimeCodeAsync(group.Id, codeStr, "8004");
        var svc = User.Use<ActivateMemberService>();

        var result = await svc.ExecuteAsync(new ActivateMemberReqDto
        {
            OneTimeCode = codeStr,
            PhoneLast4 = "9999",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.PhoneLast4Mismatch, result.ErrorCode);
    }

    /// <summary>BR-27：同码重复激活幂等（返回原结果，不重复建成员行）</summary>
    [Fact]
    public async Task ExecuteAsync_SameCodeTwice_ReturnsIdempotentResult()
    {
        var userId = SetUser(61005);
        var group = await SeedGroupAsync(999005, "群组E");
        var codeStr = NewCode();
        await SeedOneTimeCodeAsync(group.Id, codeStr, "8005");
        var svc = User.Use<ActivateMemberService>();

        var first = await svc.ExecuteAsync(new ActivateMemberReqDto { OneTimeCode = codeStr, PhoneLast4 = "8005" }, TestContext.Current.CancellationToken);
        Assert.True(first.Success);

        var second = await svc.ExecuteAsync(new ActivateMemberReqDto { OneTimeCode = codeStr, PhoneLast4 = "8005" }, TestContext.Current.CancellationToken);
        Assert.True(second.Success);
        Assert.Equal(first.MemberId, second.MemberId);

        // 仅 1 条成员行
        var membersDs = User.Use<GroupMembersDataService>();
        var members = await membersDs.EntitySelectAsync(
            x => x.InviteCodeId != null && x.UserId == userId && x.GroupId == group.Id,
            ct: TestContext.Current.CancellationToken);
        Assert.Single(members);
    }

    /// <summary>BR-26：码已被他人使用 → ONE_TIME_CODE_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_CodeUsedByOtherUser_ReturnsOneTimeCodeInvalid()
    {
        SetUser(61006);
        var group = await SeedGroupAsync(999006, "群组F");
        var codeStr = NewCode();
        await SeedOneTimeCodeAsync(group.Id, codeStr, "8006", OneTimeCodeStatus.Used);
        // 已被另一个用户绑定
        var codesDs = User.Use<OneTimeInviteCodesDataService>();
        var code = await codesDs.EntityGetAsync(x => x.Code == codeStr, TestContext.Current.CancellationToken);
        code!.BoundUserId = 888888;
        await codesDs.EntityUpdateAsync(code, TestContext.Current.CancellationToken);
        var svc = User.Use<ActivateMemberService>();

        var result = await svc.ExecuteAsync(new ActivateMemberReqDto { OneTimeCode = codeStr, PhoneLast4 = "8006" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.OneTimeCodeInvalid, result.ErrorCode);
    }

    /// <summary>BR-31：同一群组同一用户同一角色重复加入 → ALREADY_IN_GROUP（两枚不同码同群）</summary>
    [Fact]
    public async Task ExecuteAsync_SameGroupSameUser_ReturnsAlreadyInGroup()
    {
        var userId = SetUser(61007);
        var group = await SeedGroupAsync(999007, "群组G");
        var code1 = NewCode();
        var code2 = NewCode();
        await SeedOneTimeCodeAsync(group.Id, code1, "8007");
        await SeedOneTimeCodeAsync(group.Id, code2, "8008");
        var svc = User.Use<ActivateMemberService>();

        var first = await svc.ExecuteAsync(new ActivateMemberReqDto { OneTimeCode = code1, PhoneLast4 = "8007" }, TestContext.Current.CancellationToken);
        Assert.True(first.Success);

        var second = await svc.ExecuteAsync(new ActivateMemberReqDto { OneTimeCode = code2, PhoneLast4 = "8008" }, TestContext.Current.CancellationToken);
        Assert.False(second.Success);
        Assert.Equal(GroupErrorCodes.AlreadyInGroup, second.ErrorCode);
        Assert.Equal(61007, userId);
    }

    /// <summary>BR-05：内测开关关闭 → BETA_NOT_OPEN</summary>
    [Fact]
    public async Task ExecuteAsync_BetaNotOpen_ReturnsBetaNotOpen()
    {
        SetUser(61008);
        var group = await SeedGroupAsync(999008, "群组H");
        var codeStr = NewCode();
        await SeedOneTimeCodeAsync(group.Id, codeStr, "8009");
        var svc = User.Use<ActivateMemberService>();

        BetaAccessSettings.IsBetaOpen = false;
        try
        {
            var result = await svc.ExecuteAsync(new ActivateMemberReqDto { OneTimeCode = codeStr, PhoneLast4 = "8009" }, TestContext.Current.CancellationToken);
            Assert.False(result.Success);
            Assert.Equal(GroupErrorCodes.BetaNotOpen, result.ErrorCode);
        }
        finally
        {
            BetaAccessSettings.IsBetaOpen = true;
        }
    }
}
