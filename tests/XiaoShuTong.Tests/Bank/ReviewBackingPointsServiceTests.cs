using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.8 背诵点校验入库（ReviewBackingPointsService）Contract 测试
/// 覆盖 BR：BR-28 未校验需显式确认 | BR-29 AI 草稿不可静默入库 | BR-30 文件+索引原子 | BR-31 批次存在且属当前题库
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ReviewBackingPointsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(long ownerId, string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "校验入库题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Private,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private static string SeedBatch(string bankId)
    {
        var batchId = $"draft-{bankId}-{Guid.NewGuid():N}"[..24];
        DraftBackingPointStore.Put(new DraftBatch(batchId, bankId, new List<DraftBackingPoint>
        {
            new("D-1", "观沧海原文第一句", "东临碣石，以观沧海", "chinese", "观沧海", ["东临碣石", "以观沧海"], Reviewed: false),
            new("D-2", "岳阳楼记原文第一句", "庆历四年春，滕子京谪守巴陵郡", "chinese", "岳阳楼记", ["庆历四年春"], Reviewed: false),
        }, DateTime.UtcNow));
        return batchId;
    }

    /// <summary>主流程：显式确认条目入库（BR-29：草稿不可静默入库——仅确认条目被导入）</summary>
    [Fact]
    public async Task ExecuteAsync_ApproveItems_ImportsOnlyApproved()
    {
        var ownerId = SetUser(38001);
        var bank = await SeedBankAsync(ownerId, "bank-rev-38001");
        var batchId = SeedBatch(bank.BankId);
        var svc = User.Use<ReviewBackingPointsService>();

        var result = await svc.ExecuteAsync(new ReviewBackingPointsReqDto
        {
            BankId = bank.BankId,
            BatchId = batchId,
            Items = [new ReviewItemInputDto { QuestionId = "D-1" }],
            SkipUnreviewed = true,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, result.Imported);

        // 仅确认条目入库（D-1 入库，D-2 未入库）
        var questionsDs = User.Use<QuestionsDataService>();
        var questions = await questionsDs.EntitySelectAsync(x => x.BankId == bank.BankId, ct: TestContext.Current.CancellationToken);
        var q = Assert.Single(questions);
        Assert.Equal("D-1", q.QuestionId);

        // BR-30：内容权威文件已写入（含答案）
        var file = ContentFileStore.Read(bank.JsonPath);
        Assert.NotNull(file);
        Assert.Contains("东临碣石", file);
    }

    /// <summary>BR-28：存在未校验条目且未显式确认跳过 → 拒绝</summary>
    [Fact]
    public async Task ExecuteAsync_UnreviewedWithoutSkip_ReturnsParamInvalid()
    {
        var ownerId = SetUser(38002);
        var bank = await SeedBankAsync(ownerId, "bank-rev-38002");
        var batchId = SeedBatch(bank.BankId);
        var svc = User.Use<ReviewBackingPointsService>();

        var result = await svc.ExecuteAsync(new ReviewBackingPointsReqDto
        {
            BankId = bank.BankId,
            BatchId = batchId,
            Items = [new ReviewItemInputDto { QuestionId = "D-1" }],
            SkipUnreviewed = false, // 未确认跳过（D-2 未校验未确认）
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-31：非法批次 → 拒绝（批次不存在）</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBatch_ReturnsParamInvalid()
    {
        var ownerId = SetUser(38003);
        var bank = await SeedBankAsync(ownerId, "bank-rev-38003");
        var svc = User.Use<ReviewBackingPointsService>();

        var result = await svc.ExecuteAsync(new ReviewBackingPointsReqDto
        {
            BankId = bank.BankId,
            BatchId = "no-such-batch",
            Items = [],
            SkipUnreviewed = true,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-31：批次属于其他题库 → 拒绝</summary>
    [Fact]
    public async Task ExecuteAsync_BatchOfOtherBank_ReturnsParamInvalid()
    {
        var ownerId = SetUser(38004);
        var bankA = await SeedBankAsync(ownerId, "bank-rev-38004a");
        await SeedBankAsync(ownerId, "bank-rev-38004b");
        var batchId = SeedBatch(bankA.BankId);
        var svc = User.Use<ReviewBackingPointsService>();

        var result = await svc.ExecuteAsync(new ReviewBackingPointsReqDto
        {
            BankId = "bank-rev-38004b", // 批次属于 A
            BatchId = batchId,
            Items = [],
            SkipUnreviewed = true,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }
}
