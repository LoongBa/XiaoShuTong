using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.7 资料上传与 AI 预处理触发（PreprocessContentService）Contract 测试
/// 覆盖 BR：BR-11 题库存在且 Owner | BR-24 格式（PDF/Word/txt）+ 大小预检 | BR-25 幂等（同批次不重复处理）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class PreprocessContentServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 他人 Owner（避免与 51xxx 测试用户冲突）
    private const long OtherOwnerId = 999901;

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(long? ownerId, string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "预处理测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Private,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-11：题库不存在 → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_ReturnsBankNotFound()
    {
        SetUser(51101);
        var svc = User.Use<PreprocessContentService>();

        var result = await svc.ExecuteAsync(new PreprocessContentReqDto
        {
            BankId = "no-such-bank",
            Content = "测试内容段落。",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-11：私域题库非 Owner → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_BankNotOwner_ReturnsForbidden()
    {
        await SeedBankAsync(OtherOwnerId, "bank-preprocess-51102");
        SetUser(51102);
        var svc = User.Use<PreprocessContentService>();

        var result = await svc.ExecuteAsync(new PreprocessContentReqDto
        {
            BankId = "bank-preprocess-51102",
            Content = "测试内容段落。",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-24：非法格式（非 PDF/Word/txt）→ PARAM_INVALID（Job 预检拒绝）</summary>
    [Fact]
    public async Task ExecuteAsync_UnsupportedFormat_ReturnsParamInvalid()
    {
        var ownerId = SetUser(51103);
        await SeedBankAsync(ownerId, "bank-preprocess-51103");
        var svc = User.Use<PreprocessContentService>();

        var result = await svc.ExecuteAsync(new PreprocessContentReqDto
        {
            BankId = "bank-preprocess-51103",
            FileName = "资料.exe",
            Content = "测试内容段落。",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>主流程：Owner + 合法内容 → 生成草稿批次并返回 BatchId</summary>
    [Fact]
    public async Task ExecuteAsync_OwnerValidContent_ReturnsBatchId()
    {
        var ownerId = SetUser(51104);
        await SeedBankAsync(ownerId, "bank-preprocess-51104");
        DraftBackingPointStore.Reset();
        var svc = User.Use<PreprocessContentService>();

        var result = await svc.ExecuteAsync(new PreprocessContentReqDto
        {
            BankId = "bank-preprocess-51104",
            FileName = "古诗文.txt",
            Content = "东临碣石，以观沧海。\n水何澹澹，山岛竦峙。",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.BatchId));
        // 批次可被 DraftBackingPointStore 读取
        Assert.NotNull(DraftBackingPointStore.Get(result.BatchId!));
    }

    /// <summary>BR-25：幂等——同内容重复触发 → 返回相同 BatchId 且不重复生成</summary>
    [Fact]
    public async Task ExecuteAsync_SameContent_ReturnsSameBatchId()
    {
        var ownerId = SetUser(51105);
        await SeedBankAsync(ownerId, "bank-preprocess-51105");
        DraftBackingPointStore.Reset();
        var svc = User.Use<PreprocessContentService>();
        var req = new PreprocessContentReqDto
        {
            BankId = "bank-preprocess-51105",
            FileName = "资料.txt",
            Content = "第一段内容。\n第二段内容。",
        };

        var first = await svc.ExecuteAsync(req, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(req, TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.BatchId, second.BatchId);
    }
}
