using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.2 题库详情（GetBankDetailService）Contract 测试
/// 覆盖 BR：BR-04 题库不存在 → 1501 | BR-05 私域仅 Owner | BR-06 预览题目不含答案（防剧透）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetBankDetailServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 他人 Owner（避免与 50xxx 测试用户冲突）
    private const long OtherOwnerId = 999902;

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(long? ownerId, BankPrivacy privacy, string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "详情测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = privacy,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Questions> SeedQuestionAsync(string bankId, string questionId, string chapterId, string knowledgePoint)
    {
        var ds = User.Use<QuestionsDataService>();
        return await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            ChapterId = chapterId,
            QType = QuestionType.R1,
            // Content 镜像本身不含答案（BR-06 前置：防爬 DRM）
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            Keywords = "[]",
            KnowledgePoints = [knowledgePoint],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-04：题库不存在 → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_ReturnsBankNotFound()
    {
        SetUser(51001);
        var svc = User.Use<GetBankDetailService>();

        var result = await svc.ExecuteAsync(new GetBankDetailReqDto { BankId = "no-such-bank" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-05：私域题库非 Owner → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_PrivateBankNotOwner_ReturnsForbidden()
    {
        await SeedBankAsync(OtherOwnerId, BankPrivacy.Private, "bank-detail-51002");
        SetUser(51002);
        var svc = User.Use<GetBankDetailService>();

        var result = await svc.ExecuteAsync(new GetBankDetailReqDto { BankId = "bank-detail-51002" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>主流程：私域题库 Owner 访问详情 → 返回题库元数据（空题库 → 空知识点树）</summary>
    [Fact]
    public async Task ExecuteAsync_OwnerPrivateBank_ReturnsBankDetail()
    {
        var ownerId = SetUser(51003);
        await SeedBankAsync(ownerId, BankPrivacy.Private, "bank-detail-51003");
        var svc = User.Use<GetBankDetailService>();

        var result = await svc.ExecuteAsync(new GetBankDetailReqDto { BankId = "bank-detail-51003" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Bank);
        Assert.Equal("bank-detail-51003", result.Bank.BankId);
        Assert.Equal("详情测试题库", result.Bank.Name);
        Assert.Empty(result.Topics);
        Assert.Empty(result.PreviewQuestions);
    }

    /// <summary>主流程 + BR-06：公开题库 → 知识点树分组 + 预览 ≤5 且响应不含答案</summary>
    [Fact]
    public async Task ExecuteAsync_PublicBankWithQuestions_ReturnsTopicsAndAnswerFreePreview()
    {
        SetUser(51004);
        await SeedBankAsync(ownerId: null, BankPrivacy.Public, "bank-detail-51004");
        // 6 题分布在 2 个章节（第 6 题用于验证 Take(5) 截断）
        await SeedQuestionAsync("bank-detail-51004", "Q-51004a", "第一章", "观沧海");
        await SeedQuestionAsync("bank-detail-51004", "Q-51004b", "第一章", "观沧海");
        await SeedQuestionAsync("bank-detail-51004", "Q-51004c", "第一章", "次北固山下");
        await SeedQuestionAsync("bank-detail-51004", "Q-51004d", "第二章", "论语十二章");
        await SeedQuestionAsync("bank-detail-51004", "Q-51004e", "第二章", "论语十二章");
        await SeedQuestionAsync("bank-detail-51004", "Q-51004f", "第二章", "咏雪");
        var svc = User.Use<GetBankDetailService>();

        var result = await svc.ExecuteAsync(new GetBankDetailReqDto { BankId = "bank-detail-51004" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Bank);

        // 知识点树按 ChapterId 分组
        Assert.Equal(2, result.Topics.Count);
        var ch1 = result.Topics.First(t => t.ChapterId == "第一章");
        Assert.Equal(3, ch1.QuestionIds.Length);
        Assert.Contains("Q-51004a", ch1.QuestionIds);
        Assert.Equal(new[] { "观沧海", "次北固山下" }, ch1.SubTopics);

        // BR-06：预览仅 5 条（Take(5)）且响应不含答案
        Assert.Equal(5, result.PreviewQuestions.Count);
        var previewJson = System.Text.Json.JsonSerializer.Serialize(result.PreviewQuestions);
        Assert.DoesNotContain("answer", previewJson, StringComparison.OrdinalIgnoreCase);
    }
}
