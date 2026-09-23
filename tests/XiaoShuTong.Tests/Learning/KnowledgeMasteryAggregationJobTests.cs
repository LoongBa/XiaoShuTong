using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.8 知识点掌握度聚合（KnowledgeMasteryAggregationJob）Contract 测试
/// 覆盖 BR：BR-47 无作答跳过 | BR-48 单用户失败不影响整批 | BR-49 聚合口径（State 最差、Accuracy 均值、AttemptCount 累计）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class KnowledgeMasteryAggregationJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private async Task SeedQuestionAsync(string questionId, string kp = "岳阳楼记-背诵")
    {
        await SeedBankAsync();
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            ChapterId = "7a",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            Keywords = "[{\"Aliases\":[\"若出其中\"],\"Weight\":1,\"Required\":false}]",
            KnowledgePoints = [kp],
            Difficulty = 0,
            Status = QuestionStatus.Active,
            Hint = "首字：若",
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedBankAsync()
    {
        var ds = User.Use<BanksDataService>();
        var existing = await ds.EntityGetAsync(x => x.BankId == "bank-ch-7a", TestContext.Current.CancellationToken);
        if (existing != null) return;
        await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = "bank-ch-7a",
            Name = "聚合测试题库",
            Subject = Subject.Chinese, // → "Chinese"（聚合分组键真实来源）
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = "bank.bank-ch-7a.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>空题库题目（真实 Questions 存在但无 KnowledgePoints → 聚合跳过）</summary>
    private async Task SeedEmptyMetaQuestionAsync(string questionId)
    {
        await SeedBankAsync();
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            ChapterId = "7a",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            Keywords = "[]",
            KnowledgePoints = [],
            Difficulty = 0,
            Status = QuestionStatus.Active,
            Hint = null,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedAttemptAsync(long userId, string questionId, JudgmentResult result)
    {
        var ds = User.Use<AttemptsDataService>();
        await ds.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = MemoryState.NotMastered,
            PostState = result == JudgmentResult.Correct ? MemoryState.Mastered : MemoryState.NotMastered,
            Result = result,
            HintLevel = HintLevel.None,
            AnsweredAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-47：无作答的用户不产生 KnowledgeMastery 写入（fixture 共享数据，按用户隔离断言）</summary>
    [Fact]
    public async Task ExecuteAsync_NoAttempts_Skips()
    {
        const long noAttemptUser = 48001; // 该用户无任何作答
        var job = User.Use<KnowledgeMasteryAggregationJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var masteryDs = User.Use<KnowledgeMasteryDataService>();
        var rows = await masteryDs.EntitySelectAsync(x => x.UserId == noAttemptUser, ct: TestContext.Current.CancellationToken);
        Assert.Empty(rows);
    }

    /// <summary>BR-49：聚合口径——Partial(0.5) + Correct(1.0) → Accuracy=0.75、AttemptCount=2、State 取最差</summary>
    [Fact]
    public async Task ExecuteAsync_AggregatesAccuracyAndCount()
    {
        await SeedQuestionAsync("Q-48002a");
        await SeedAttemptAsync(48002, "Q-48002a", JudgmentResult.Partial); // 0.5
        await SeedAttemptAsync(48002, "Q-48002a", JudgmentResult.Correct); // 1.0
        var job = User.Use<KnowledgeMasteryAggregationJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var masteryDs = User.Use<KnowledgeMasteryDataService>();
        var row = await masteryDs.EntityGetAsync(
            x => x.UserId == 48002 && x.Subject == "Chinese" && x.KnowledgePoint == "岳阳楼记-背诵",
            TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Equal(0.75, row.Accuracy);
        Assert.Equal(2, row.AttemptCount);
        Assert.Equal(MemoryState.NotMastered, row.State); // 最差状态
        Assert.NotNull(row.LastReviewedAt);
    }

    /// <summary>BR-49/幂等：重复执行不重复累计（AttemptCount 稳定）</summary>
    [Fact]
    public async Task ExecuteAsync_Rerun_Idempotent()
    {
        await SeedQuestionAsync("Q-48003a");
        await SeedAttemptAsync(48003, "Q-48003a", JudgmentResult.Correct);
        var job = User.Use<KnowledgeMasteryAggregationJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);
        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var masteryDs = User.Use<KnowledgeMasteryDataService>();
        var row = await masteryDs.EntityGetAsync(
            x => x.UserId == 48003 && x.KnowledgePoint == "岳阳楼记-背诵",
            TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Equal(1, row.AttemptCount); // 幂等：不重复累计
    }

    /// <summary>BR-48：无题目元数据（真实 Questions 无 KnowledgePoints）的用户被过滤（跳过），其他用户正常聚合</summary>
    [Fact]
    public async Task ExecuteAsync_UserWithoutMeta_Skipped_OthersAggregated()
    {
        // 用户 A：题目存在但无知识点元数据（真实 Questions 空 KnowledgePoints → 聚合过滤跳过）
        await SeedEmptyMetaQuestionAsync("Q-48004a");
        await SeedAttemptAsync(48004, "Q-48004a", JudgmentResult.Correct);
        // 用户 B：正常注册
        await SeedQuestionAsync("Q-48004b");
        await SeedAttemptAsync(48005, "Q-48004b", JudgmentResult.Correct);
        var job = User.Use<KnowledgeMasteryAggregationJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var masteryDs = User.Use<KnowledgeMasteryDataService>();
        var userB = await masteryDs.EntityGetAsync(x => x.UserId == 48005, TestContext.Current.CancellationToken);
        Assert.NotNull(userB); // 用户 B 正常聚合
        var userA = await masteryDs.EntitySelectAsync(x => x.UserId == 48004, ct: TestContext.Current.CancellationToken);
        Assert.Empty(userA); // 用户 A 未产生写入
    }
}
