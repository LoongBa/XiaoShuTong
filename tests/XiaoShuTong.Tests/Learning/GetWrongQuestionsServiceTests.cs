using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Services.Shared;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.7 错题本查询（GetWrongQuestionsService）Contract 测试
/// 覆盖 BR：BR-43 空态 | BR-44 仅当前用户（RLS）| BR-45 Mastered/Subject 过滤 | BR-46 参数校验
/// 富化：KnowledgePoint（注册表）+ Summary（题库域题目摘要，2026-09-09 合并 Stats 版）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetWrongQuestionsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedWrongAsync(long userId, string questionId, string subject, int wrongCount, bool mastered)
    {
        var ds = User.Use<WrongQuestionsDataService>();
        await ds.EntityCreateAsync(new WrongQuestions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = questionId,
            BankId = "bank-learning-47001",
            Subject = subject,
            WrongCount = wrongCount,
            LastWrongAt = DateTime.UtcNow,
            Mastered = mastered,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedQuestionAsync(string questionId, string stem)
    {
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = "bank-learning-47001",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"{stem}\"}}",
            Keywords = "[]",
            KnowledgePoints = [],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private void RegisterMeta(string questionId, string knowledgePoint)
    {
        LearningQuestionRegistry.Register(new LearningQuestionMeta(
            questionId, "bank-learning-47001", "chinese", knowledgePoint, "R1", [], ""));
    }

    /// <summary>主流程 + 富化：错题列表 + KnowledgePoint（注册表）+ Summary（题目摘要，无答案）</summary>
    [Fact]
    public async Task ExecuteAsync_ListWithKnowledgePointAndSummary()
    {
        var userId = SetUser(47001);
        await SeedWrongAsync(userId, "Q-47001a", "chinese", 3, mastered: false);
        await SeedQuestionAsync("Q-47001a", "东临碣石，___");
        RegisterMeta("Q-47001a", "诗歌鉴赏");
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.Equal("Q-47001a", item.QuestionId);
        Assert.Equal(3, item.WrongCount);
        Assert.Equal("诗歌鉴赏", item.KnowledgePoint); // 注册表富化
        Assert.Equal("东临碣石，___", item.Summary);    // 题库域题目摘要
        Assert.Equal(1, result.Total);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>BR-44：仅当前用户错题（RLS）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnWrongQuestions()
    {
        var userId = SetUser(47002);
        await SeedWrongAsync(userId, "Q-47002a", "chinese", 2, mastered: false);
        await SeedWrongAsync(999999, "Q-47002b", "chinese", 5, mastered: false); // 他人
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Equal("Q-47002a", result.Items[0].QuestionId);
    }

    /// <summary>BR-43：空错题本正常返回空</summary>
    [Fact]
    public async Task ExecuteAsync_NoWrongQuestions_ReturnsEmpty()
    {
        SetUser(47003);
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    /// <summary>BR-46：PageSize=0 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPageSize_ReturnsParamInvalid()
    {
        SetUser(47004);
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false, PageSize = 0 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-45/Mastered 分组：待掌握与已掌握分组过滤</summary>
    [Fact]
    public async Task ExecuteAsync_MasteredFilter_GroupsCorrectly()
    {
        var userId = SetUser(47005);
        await SeedWrongAsync(userId, "Q-47005a", "chinese", 2, mastered: false);
        await SeedWrongAsync(userId, "Q-47005b", "chinese", 4, mastered: true);
        var svc = User.Use<GetWrongQuestionsService>();

        var pending = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);
        var mastered = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = true }, TestContext.Current.CancellationToken);

        Assert.Single(pending.Items);
        Assert.Equal("Q-47005a", pending.Items[0].QuestionId);
        Assert.Single(mastered.Items);
        Assert.Equal("Q-47005b", mastered.Items[0].QuestionId);
    }
}