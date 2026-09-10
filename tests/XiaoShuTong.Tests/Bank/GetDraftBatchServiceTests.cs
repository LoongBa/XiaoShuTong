using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.7 草稿批次查询（GetDraftBatchService）Contract 测试
/// 覆盖 BR：BR-31 批次必须存在且属于当前题库（且为 Owner 可见）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetDraftBatchServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 他人 Owner（避免与 51xxx 测试用户冲突）
    private const long OtherOwnerId = 999902;

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
            Name = "草稿查询测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Private,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-31：批次不存在 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBatch_ReturnsParamInvalid()
    {
        SetUser(51201);
        var svc = User.Use<GetDraftBatchService>();

        var result = await svc.ExecuteAsync(new GetDraftBatchReqDto { BatchId = "no-such-batch" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-31：批次存在但题库为他人 → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_BatchOfOtherOwner_ReturnsForbidden()
    {
        await SeedBankAsync(OtherOwnerId, "bank-draft-51202");
        DraftBackingPointStore.Put(new DraftBatch(
            BatchId: "draft-other-51202",
            BankId: "bank-draft-51202",
            Items: [new DraftBackingPoint("D-1", "题干", "答案", "chinese", "知识点", ["关键词"], false)],
            CreatedAt: DateTime.UtcNow));
        SetUser(51202);
        var svc = User.Use<GetDraftBatchService>();

        var result = await svc.ExecuteAsync(new GetDraftBatchReqDto { BatchId = "draft-other-51202" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>主流程：批次存在且属当前用户 → 返回草稿条目列表</summary>
    [Fact]
    public async Task ExecuteAsync_OwnBatch_ReturnsDraftItems()
    {
        var ownerId = SetUser(51203);
        await SeedBankAsync(ownerId, "bank-draft-51203");
        DraftBackingPointStore.Put(new DraftBatch(
            BatchId: "draft-own-51203",
            BankId: "bank-draft-51203",
            Items:
            [
                new DraftBackingPoint("D-51203-001", "东临碣石", "以观沧海", "chinese", "观沧海", ["碣石"], false),
                new DraftBackingPoint("D-51203-002", "水何澹澹", "山岛竦峙", "chinese", "观沧海", ["澹澹"], true),
            ],
            CreatedAt: DateTime.UtcNow));
        var svc = User.Use<GetDraftBatchService>();

        var result = await svc.ExecuteAsync(new GetDraftBatchReqDto { BatchId = "draft-own-51203" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("draft-own-51203", result.BatchId);
        Assert.Equal("bank-draft-51203", result.BankId);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("D-51203-001", result.Items[0].QuestionId);
        Assert.Equal("东临碣石", result.Items[0].Stem);
        Assert.Equal("以观沧海", result.Items[0].Answer);
        Assert.Equal(["碣石"], result.Items[0].Keywords);
        Assert.False(result.Items[0].Reviewed);
        Assert.True(result.Items[1].Reviewed);
    }
}
