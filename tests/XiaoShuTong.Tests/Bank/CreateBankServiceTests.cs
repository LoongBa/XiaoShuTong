using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.3 创建题库（CreateBankService）Contract 测试
/// 覆盖 BR：BR-07 名称必填 ≤128、学科必填合法 | BR-08 Privacy=Group 必须指定群组 | BR-09 群主可创建多个题库
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CreateBankServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    /// <summary>主流程：合法请求创建默认私有题库（OwnerId=当前群主，默认私有）</summary>
    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesPrivateBank()
    {
        var ownerId = SetUser(50001);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "古诗词背诵",
            Subject = "Chinese",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.BankUid));
        Assert.False(string.IsNullOrEmpty(result.BankId));

        // 数据已落库：OwnerId=当前群主，默认私有，学科/用途/状态正确
        var ds = User.Use<BanksDataService>();
        var bank = await ds.EntityGetAsync(x => x.BankId == result.BankId, TestContext.Current.CancellationToken);
        Assert.NotNull(bank);
        Assert.Equal(ownerId, bank.OwnerId);
        Assert.Equal(BankPrivacy.Private, bank.Privacy);
        Assert.Equal(Subject.Chinese, bank.Subject);
        Assert.Equal(BankPurpose.Memorize, bank.Purpose);
        Assert.Equal(BankStatus.Active, bank.Status);
    }

    /// <summary>BR-07：名称为空 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsParamInvalid()
    {
        SetUser(50002);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "   ",
            Subject = "Math",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-07：名称超 128 字 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_NameTooLong_ReturnsParamInvalid()
    {
        SetUser(50003);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = new string('长', 129),
            Subject = "Math",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-07：学科为空 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_EmptySubject_ReturnsParamInvalid()
    {
        SetUser(50004);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "合法名称",
            Subject = "",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-07：学科非法枚举 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidSubject_ReturnsParamInvalid()
    {
        SetUser(50005);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "合法名称",
            Subject = "xyz",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-08：Privacy=Group 但未指定群组 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_GroupPrivacyWithoutGroupIds_ReturnsParamInvalid()
    {
        SetUser(50006);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "小组题库",
            Subject = "History",
            Privacy = "Group",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-08 主流程：Privacy=Group 且指定群组 → 创建成功（切片校验仅非空）</summary>
    [Fact]
    public async Task ExecuteAsync_GroupPrivacyWithGroupIds_CreatesGroupBank()
    {
        var ownerId = SetUser(50007);
        var svc = User.Use<CreateBankService>();

        var result = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "小组题库",
            Subject = "History",
            Privacy = "Group",
            GroupIds = ["g-50007-1"],
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);

        var ds = User.Use<BanksDataService>();
        var bank = await ds.EntityGetAsync(x => x.BankId == result.BankId, TestContext.Current.CancellationToken);
        Assert.NotNull(bank);
        Assert.Equal(BankPrivacy.Group, bank.Privacy);
        Assert.Equal(ownerId, bank.OwnerId);
    }

    /// <summary>BR-09：同一群主连续创建 2 个题库均成功（可建多库）</summary>
    [Fact]
    public async Task ExecuteAsync_CreateTwoBanks_BothSucceed()
    {
        SetUser(50008);
        var svc = User.Use<CreateBankService>();

        var first = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "题库A",
            Subject = "Chinese",
        }, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(new CreateBankReqDto
        {
            Name = "题库B",
            Subject = "Math",
        }, TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(first.BankId, second.BankId);
    }
}
