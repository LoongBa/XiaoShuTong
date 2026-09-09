using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.6 知识卡片（GetKnowledgeCardService）Contract 测试
/// 覆盖 BR：BR-21 题目必须存在 → 1502 | BR-22 无关联卡片返回空（不报错）| BR-23 卡片按题库模板渲染
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetKnowledgeCardServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Questions> SeedQuestionAsync(string questionId, string content)
    {
        var ds = User.Use<QuestionsDataService>();
        return await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = "bank-card-52001",
            QType = QuestionType.R1,
            Content = content,
            Keywords = "[]",
            KnowledgePoints = ["观沧海"],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-21：题目不存在 → QUESTION_NOT_IN_BANK</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownQuestion_ReturnsQuestionNotInBank()
    {
        SetUser(52001);
        var svc = User.Use<GetKnowledgeCardService>();

        var result = await svc.ExecuteAsync(new GetKnowledgeCardReqDto { QuestionId = "no-such-question" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.QuestionNotInBank, result.ErrorCode);
    }

    /// <summary>BR-22：题目无关联卡片 → 成功但空 content（不报错）</summary>
    [Fact]
    public async Task ExecuteAsync_QuestionWithoutCard_ReturnsEmptyCard()
    {
        SetUser(52002);
        await SeedQuestionAsync("Q-card-52002", "{\"stem\":\"补全：东临碣石，___\"}");
        var svc = User.Use<GetKnowledgeCardService>();

        var result = await svc.ExecuteAsync(new GetKnowledgeCardReqDto { QuestionId = "Q-card-52002" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("authorCard", result.CardType); // 默认作者卡
        Assert.Equal("{}", result.Content);          // 空卡片
    }

    /// <summary>BR-22 备选：Content 非法 JSON → 默认空卡片（JsonException 兜底）</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidJsonContent_ReturnsEmptyCard()
    {
        SetUser(52003);
        await SeedQuestionAsync("Q-card-52003", "not-a-json");
        var svc = User.Use<GetKnowledgeCardService>();

        var result = await svc.ExecuteAsync(new GetKnowledgeCardReqDto { QuestionId = "Q-card-52003" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("authorCard", result.CardType);
        Assert.Equal("{}", result.Content);
    }

    /// <summary>BR-23：卡片按题库模板渲染（从 Content 镜像提取 cardType/card 字段）</summary>
    [Fact]
    public async Task ExecuteAsync_QuestionWithCard_ReturnsRenderedCard()
    {
        SetUser(52004);
        await SeedQuestionAsync(
            "Q-card-52004",
            "{\"cardType\":\"wordCard\",\"card\":{\"word\":\"观沧海\",\"meaning\":\"观看大海\"}}");
        var svc = User.Use<GetKnowledgeCardService>();

        var result = await svc.ExecuteAsync(new GetKnowledgeCardReqDto { QuestionId = "Q-card-52004" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("wordCard", result.CardType);
        // Content 为 card 字段的原始 JSON
        Assert.Contains("\"word\":\"观沧海\"", result.Content);
        Assert.Contains("\"meaning\":\"观看大海\"", result.Content);
    }
}
