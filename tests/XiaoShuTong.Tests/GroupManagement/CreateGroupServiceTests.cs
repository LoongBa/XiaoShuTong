using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.1 激活建群（CreateGroupService）Contract 测试
/// 覆盖 BR：BR-01 码有效/已用/过期 | BR-02 一码一群组 | BR-03 名称/学科校验 | BR-04 可建多群 | BR-05 内测开关
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CreateGroupServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<BetaInviteCodes> SeedBetaCodeAsync(
        string code, BetaCodeStatus status = BetaCodeStatus.Pending, int daysToExpire = 30)
    {
        var ds = User.Use<BetaInviteCodesDataService>();
        return await ds.EntityCreateAsync(new BetaInviteCodes
        {
            Code = code,
            Status = status,
            ExpiresAt = DateTime.UtcNow.AddDays(daysToExpire),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：有效码 + 名称/学科 → 建群成功 + 码置 Used（BR-01）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidCode_CreatesGroupAndMarksCodeUsed()
    {
        var ownerId = SetUser(61001);
        var code = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code);
        var svc = User.Use<CreateGroupService>();

        var result = await svc.ExecuteAsync(new CreateGroupReqDto
        {
            BetaCode = code,
            Name = "七(3)班",
            Subject = "history",
            Grade = "七年级",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.GroupId > 0);

        // 群组已建（OwnerId=当前用户、BetaCodeId 关联）
        var groupsDs = User.Use<GroupsDataService>();
        var group = await groupsDs.EntityGetAsync(x => x.Id == result.GroupId, TestContext.Current.CancellationToken);
        Assert.NotNull(group);
        Assert.Equal(ownerId, group.OwnerId);
        Assert.Equal(GroupStatus.Active, group.Status);

        // 码已置 Used + UsedByGroupId 落库
        var betaDs = User.Use<BetaInviteCodesDataService>();
        var used = await betaDs.EntityGetAsync(x => x.Code == code, TestContext.Current.CancellationToken);
        Assert.NotNull(used);
        Assert.Equal(BetaCodeStatus.Used, used.Status);
        Assert.Equal(result.GroupId, used.UsedByGroupId);
        Assert.NotNull(used.UsedAt);
    }

    /// <summary>BR-01/BR-02：已使用码二次激活 → BETA_CODE_INVALID（一码一群组）</summary>
    [Fact]
    public async Task ExecuteAsync_UsedCode_ReturnsBetaCodeInvalid()
    {
        SetUser(61002);
        var code = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code, BetaCodeStatus.Used);
        var svc = User.Use<CreateGroupService>();

        var result = await svc.ExecuteAsync(new CreateGroupReqDto
        {
            BetaCode = code,
            Name = "群组A",
            Subject = "geography",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.BetaCodeInvalid, result.ErrorCode);
    }

    /// <summary>BR-01：过期码 → BETA_CODE_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredCode_ReturnsBetaCodeInvalid()
    {
        SetUser(61003);
        var code = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code, daysToExpire: -1);
        var svc = User.Use<CreateGroupService>();

        var result = await svc.ExecuteAsync(new CreateGroupReqDto
        {
            BetaCode = code,
            Name = "群组A",
            Subject = "biology",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.BetaCodeInvalid, result.ErrorCode);
    }

    /// <summary>BR-01：不存在的码 → BETA_CODE_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownCode_ReturnsBetaCodeInvalid()
    {
        SetUser(61004);
        var svc = User.Use<CreateGroupService>();

        var result = await svc.ExecuteAsync(new CreateGroupReqDto
        {
            BetaCode = "NO-SUCH-CODE-0001",
            Name = "群组A",
            Subject = "history",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.BetaCodeInvalid, result.ErrorCode);
    }

    /// <summary>BR-03：名称为空 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsParamInvalid()
    {
        SetUser(61005);
        var code = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code);
        var svc = User.Use<CreateGroupService>();

        var result = await svc.ExecuteAsync(new CreateGroupReqDto
        {
            BetaCode = code,
            Name = " ",
            Subject = "history",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-03：学科缺失 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_MissingSubject_ReturnsParamInvalid()
    {
        SetUser(61006);
        var code = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code);
        var svc = User.Use<CreateGroupService>();

        var result = await svc.ExecuteAsync(new CreateGroupReqDto
        {
            BetaCode = code,
            Name = "七(3)班",
            Subject = "",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-05：内测开关关闭 → BETA_NOT_OPEN（try/finally 恢复，避免影响同 Collection 其他测试）</summary>
    [Fact]
    public async Task ExecuteAsync_BetaNotOpen_ReturnsBetaNotOpen()
    {
        SetUser(61007);
        var code = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code);
        var svc = User.Use<CreateGroupService>();

        BetaAccessSettings.IsBetaOpen = false;
        try
        {
            var result = await svc.ExecuteAsync(new CreateGroupReqDto
            {
                BetaCode = code,
                Name = "群组A",
                Subject = "history",
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(GroupErrorCodes.BetaNotOpen, result.ErrorCode);
        }
        finally
        {
            BetaAccessSettings.IsBetaOpen = true;
        }
    }

    /// <summary>BR-04：群主可用不同内测码连续创建多个群组</summary>
    [Fact]
    public async Task ExecuteAsync_OwnerCreatesMultipleGroups_AllSucceed()
    {
        SetUser(61008);
        var svc = User.Use<CreateGroupService>();

        var code1 = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var code2 = $"B{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedBetaCodeAsync(code1);
        await SeedBetaCodeAsync(code2);

        var r1 = await svc.ExecuteAsync(new CreateGroupReqDto { BetaCode = code1, Name = "群组一", Subject = "history" }, TestContext.Current.CancellationToken);
        var r2 = await svc.ExecuteAsync(new CreateGroupReqDto { BetaCode = code2, Name = "群组二", Subject = "geography" }, TestContext.Current.CancellationToken);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.NotEqual(r1.GroupId, r2.GroupId);
    }
}
