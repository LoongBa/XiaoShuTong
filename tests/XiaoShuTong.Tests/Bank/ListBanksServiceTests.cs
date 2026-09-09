using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.1 题库列表（ListBanksService）Contract 测试
/// 覆盖 BR：BR-01 空列表 | BR-02 参数校验 | BR-03 可见性过滤（公开可见、私域仅 Owner）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ListBanksServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(long? ownerId, BankPrivacy privacy, Subject subject, string? bankId = null)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = XiaoShuTong.Tools.UidGenerator.NewId(),
            BankId = bankId ?? $"bank-{Guid.NewGuid():N}"[..20],
            Name = $"题库-{Guid.NewGuid():N}"[..10],
            Subject = subject,
            Purpose = BankPurpose.Memorize,
            Privacy = privacy,
            OwnerId = ownerId,
            JsonPath = $"bank.{Guid.NewGuid():N}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedQuestionAsync(string bankId, string questionId)
    {
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = XiaoShuTong.Tools.UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\"}}",
            Keywords = "[]",
            KnowledgePoints = [],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-01：空列表正常返回</summary>
    [Fact]
    public async Task ExecuteAsync_NoBanks_ReturnsEmptyList()
    {
        SetUser(31001);
        var svc = User.Use<ListBanksService>();

        var result = await svc.ExecuteAsync(new ListBanksReqDto { Subject = "Fun" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    /// <summary>BR-02：非法学科 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidSubject_ReturnsParamInvalid()
    {
        SetUser(31002);
        var svc = User.Use<ListBanksService>();

        var result = await svc.ExecuteAsync(new ListBanksReqDto { Subject = "xyz" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-02：PageSize=0 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPageSize_ReturnsParamInvalid()
    {
        SetUser(31003);
        var svc = User.Use<ListBanksService>();

        var result = await svc.ExecuteAsync(new ListBanksReqDto { PageSize = 0 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-03：学生仅可见公开题库 + 自己的私域；他人私域不可见</summary>
    [Fact]
    public async Task ExecuteAsync_Visibility_OnlyPublicAndOwnPrivate()
    {
        var userId = SetUser(31004);
        await SeedBankAsync(ownerId: null, BankPrivacy.Public, Subject.Chinese, bankId: "bank-pub-31004");
        await SeedBankAsync(ownerId: 999999, BankPrivacy.Private, Subject.History, bankId: "bank-other-31004");
        await SeedBankAsync(ownerId: userId, BankPrivacy.Private, Subject.Math, bankId: "bank-mine-31004");
        var svc = User.Use<ListBanksService>();

        var result = await svc.ExecuteAsync(new ListBanksReqDto { PageSize = 50 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var ids = result.Items.Select(i => i.Bank?.BankId).ToList();
        Assert.Contains("bank-pub-31004", ids);   // 公开可见
        Assert.Contains("bank-mine-31004", ids);  // 自己私域可见
        Assert.DoesNotContain("bank-other-31004", ids); // 他人私域不可见
    }

    /// <summary>主流程：学科过滤 + 题量聚合（fixture 共享数据，断言用 Contains + 全量学科校验）</summary>
    [Fact]
    public async Task ExecuteAsync_SubjectFilter_ReturnsQuestionCount()
    {
        SetUser(31005);
        await SeedBankAsync(ownerId: null, BankPrivacy.Public, Subject.Chinese, bankId: "bank-ch-31005");
        await SeedQuestionAsync("bank-ch-31005", "Q-31005a");
        await SeedQuestionAsync("bank-ch-31005", "Q-31005b");
        var svc = User.Use<ListBanksService>();

        var result = await svc.ExecuteAsync(new ListBanksReqDto { Subject = "Chinese" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.All(result.Items, i => Assert.Equal("Chinese", i.Bank?.Subject.ToString())); // 全部为中文
        Assert.Contains(result.Items, i => i.Bank?.BankId == "bank-ch-31005");
        var mine = result.Items.First(i => i.Bank?.BankId == "bank-ch-31005");
        Assert.Equal(2, mine.QuestionCount);
    }
}
