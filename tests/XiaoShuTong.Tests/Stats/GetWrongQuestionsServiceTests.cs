using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Shared;
using XiaoShuTong.Services.Stats;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Stats;

/// <summary>
/// UC-6.4 查看错题本（GetWrongQuestionsService Stats 版）Contract 测试
/// 覆盖 BR：BR-12 空态 | BR-13 参数校验 | BR-14 仅当前学生（RLS）| BR-15 只读 + 题目摘要关联
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
            BankId = "bank-stats-64001",
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
            BankId = "bank-stats-64001",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"{stem}\"}}",
            Keywords = "[]",
            KnowledgePoints = [],
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-15：错题列表 + 题目摘要关联（不含答案）</summary>
    [Fact]
    public async Task ExecuteAsync_ListWithSummary()
    {
        var userId = SetUser(64001);
        await SeedWrongAsync(userId, "Q-64001a", "chinese", 3, mastered: false);
        await SeedQuestionAsync("Q-64001a", "东临碣石，___");
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.Equal("Q-64001a", item.QuestionId);
        Assert.Equal(3, item.WrongCount);
        Assert.Equal("东临碣石，___", item.Summary); // 题目摘要（题干，无答案）

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>BR-14：仅当前学生错题（RLS）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnWrongQuestions()
    {
        var userId = SetUser(64002);
        await SeedWrongAsync(userId, "Q-64002a", "chinese", 2, mastered: false);
        await SeedWrongAsync(999999, "Q-64002b", "chinese", 5, mastered: false); // 他人
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Equal("Q-64002a", result.Items[0].QuestionId);
    }

    /// <summary>BR-12：空错题本正常返回空</summary>
    [Fact]
    public async Task ExecuteAsync_NoWrongQuestions_ReturnsEmpty()
    {
        SetUser(64003);
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    /// <summary>BR-13：PageSize=0 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPageSize_ReturnsParamInvalid()
    {
        SetUser(64004);
        var svc = User.Use<GetWrongQuestionsService>();

        var result = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false, PageSize = 0 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(StatsErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-12/Mastered 分组：待掌握与已掌握分组过滤</summary>
    [Fact]
    public async Task ExecuteAsync_MasteredFilter_GroupsCorrectly()
    {
        var userId = SetUser(64005);
        await SeedWrongAsync(userId, "Q-64005a", "chinese", 2, mastered: false);
        await SeedWrongAsync(userId, "Q-64005b", "chinese", 4, mastered: true);
        var svc = User.Use<GetWrongQuestionsService>();

        var pending = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = false }, TestContext.Current.CancellationToken);
        var mastered = await svc.ExecuteAsync(new GetWrongQuestionsReqDto { Mastered = true }, TestContext.Current.CancellationToken);

        Assert.Single(pending.Items);
        Assert.Equal("Q-64005a", pending.Items[0].QuestionId);
        Assert.Single(mastered.Items);
        Assert.Equal("Q-64005b", mastered.Items[0].QuestionId);
    }
}