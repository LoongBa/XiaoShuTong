using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.9 薄弱知识点（GetWeaknessReportService）Contract 测试
/// 覆盖 BR：BR-25 订阅门控 8001 | BR-26 按需聚合 | BR-27 状态映射
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetWeaknessReportServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedRelationAsync(long parentId, long studentId)
    {
        var ds = User.Use<ParentStudentRelationsDataService>();
        await ds.EntityCreateAsync(new ParentStudentRelations
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Relation = ParentRelation.Parent,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedSubscriptionAsync(long parentId, long studentId, SubscriptionStatus status = SubscriptionStatus.Active)
    {
        var ds = User.Use<SubscriptionsDataService>();
        await ds.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = studentId,
            Plan = SubscriptionPlan.Month,
            Status = status,
            PeriodEndAt = DateTime.UtcNow.AddDays(20),
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedMasteryAsync(long studentId, string kp, double accuracy, MemoryState state)
    {
        var ds = User.Use<KnowledgeMasteryDataService>();
        await ds.EntityCreateAsync(new KnowledgeMastery
        {
            UId = UidGenerator.NewId(),
            UserId = studentId,
            Subject = "chinese",
            KnowledgePoint = kp,
            State = state,
            Accuracy = accuracy,
            AttemptCount = 1,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Banks> SeedBankAsync(string bankId, Subject subject)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = $"题库-{bankId}",
            Subject = subject,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedQuestionAsync(string questionId, string bankId, string? chapterId, string[] knowledgePoints, string? topic = null)
    {
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            ChapterId = chapterId,
            Topic = topic,
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\"}}",
            Keywords = "[]",
            KnowledgePoints = knowledgePoints,
            Difficulty = 0,
            Status = QuestionStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-27：薄弱点按正确率升序 + 状态映射</summary>
    [Fact]
    public async Task ExecuteAsync_Subscribed_ReturnsWeakPointsAsc()
    {
        var parentId = SetUser(10901);
        await SeedRelationAsync(parentId, 19011);
        await SeedSubscriptionAsync(parentId, 19011);
        await SeedMasteryAsync(19011, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedMasteryAsync(19011, "观沧海", 0.9, MemoryState.Proficient);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19011 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.WeakPoints.Count);
        Assert.Equal("岳阳楼记", result.WeakPoints[0].KnowledgePoint); // 0.3 最薄弱在前
        Assert.Equal("未掌握", result.WeakPoints[0].StateText);       // BR-27 状态映射
        Assert.Equal("熟练", result.WeakPoints[1].StateText);
    }

    /// <summary>BR-25：未订阅 → 8001</summary>
    [Fact]
    public async Task ExecuteAsync_NoSubscription_Returns8001()
    {
        var parentId = SetUser(10902);
        await SeedRelationAsync(parentId, 19021);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19021 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.SubscriptionRequired, result.ErrorCode);
    }

    /// <summary>BR-26：无掌握度数据且无作答 → 4001（按需聚合无源数据）</summary>
    [Fact]
    public async Task ExecuteAsync_NoMasteryData_Returns4001()
    {
        var parentId = SetUser(10903);
        await SeedRelationAsync(parentId, 19031);
        await SeedSubscriptionAsync(parentId, 19031);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19031 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ParentErrorCodes.NoStatsData, result.ErrorCode);
    }

    /// <summary>C3 主路径：薄弱点反查得 ChapterId + QuestionIds（Subject → Banks → Questions → 内存 kp 过滤）</summary>
    [Fact]
    public async Task ExecuteAsync_WithQuestions_DrillsChapterAndQuestionIds()
    {
        var parentId = SetUser(11001);
        await SeedRelationAsync(parentId, 19051);
        await SeedSubscriptionAsync(parentId, 19051);
        await SeedMasteryAsync(19051, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedMasteryAsync(19051, "观沧海", 0.9, MemoryState.Proficient);
        await SeedBankAsync("chinese-7to9", Subject.Chinese);
        await SeedQuestionAsync("Q-ch-7a-0001", "chinese-7to9", "7a", ["岳阳楼记"]);
        await SeedQuestionAsync("Q-ch-7a-0002", "chinese-7to9", "7b", ["观沧海"]);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19051 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.WeakPoints.Count);
        var weakest = result.WeakPoints[0]; // 岳阳楼记 0.3 最薄弱在前
        Assert.Equal("岳阳楼记", weakest.KnowledgePoint);
        Assert.Equal("7a", weakest.ChapterId);
        Assert.Equal(new[] { "Q-ch-7a-0001" }, weakest.QuestionIds);
        var second = result.WeakPoints[1];
        Assert.Equal("观沧海", second.KnowledgePoint);
        Assert.Equal("7b", second.ChapterId);
        Assert.Equal(new[] { "Q-ch-7a-0002" }, second.QuestionIds);
    }

    /// <summary>C3：某知识点无关联题 → ChapterId=null + QuestionIds 空（不报错）</summary>
    [Fact]
    public async Task ExecuteAsync_PointWithoutQuestions_ReturnsEmptyDrill()
    {
        var parentId = SetUser(11002);
        await SeedRelationAsync(parentId, 19052);
        await SeedSubscriptionAsync(parentId, 19052);
        await SeedMasteryAsync(19052, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedMasteryAsync(19052, "滕王阁序", 0.8, MemoryState.Mastered);
        await SeedBankAsync("chinese-7to9", Subject.Chinese);
        await SeedQuestionAsync("Q-ch-7a-0001", "chinese-7to9", "7a", ["岳阳楼记"]);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19052 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var withQuestion = result.WeakPoints.Single(w => w.KnowledgePoint == "岳阳楼记");
        Assert.Equal("7a", withQuestion.ChapterId);
        Assert.Equal(new[] { "Q-ch-7a-0001" }, withQuestion.QuestionIds);
        var withoutQuestion = result.WeakPoints.Single(w => w.KnowledgePoint == "滕王阁序");
        Assert.Null(withoutQuestion.ChapterId);
        Assert.Empty(withoutQuestion.QuestionIds);
    }

    /// <summary>C3：多题不同 ChapterId → 取首个非空（按 QuestionId 序，最小编号题目章节优先）</summary>
    [Fact]
    public async Task ExecuteAsync_MultipleChapters_TakesFirstNonNull()
    {
        var parentId = SetUser(11003);
        await SeedRelationAsync(parentId, 19053);
        await SeedSubscriptionAsync(parentId, 19053);
        await SeedMasteryAsync(19053, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedBankAsync("chinese-7to9", Subject.Chinese);
        await SeedQuestionAsync("Q-ch-7a-0002", "chinese-7to9", "7b", ["岳阳楼记"]);
        await SeedQuestionAsync("Q-ch-7a-0001", "chinese-7to9", "7a", ["岳阳楼记"]);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19053 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var weak = Assert.Single(result.WeakPoints);
        Assert.Equal("7a", weak.ChapterId); // 首个非空（按 QuestionId 序）
        Assert.Equal(new[] { "Q-ch-7a-0001", "Q-ch-7a-0002" }, weak.QuestionIds.OrderBy(q => q));
    }

    /// <summary>C3 RLS：他人（非本学生薄弱知识点/他学科题库）的题目不混入</summary>
    [Fact]
    public async Task ExecuteAsync_OtherQuestions_NotMixedIn()
    {
        var parentId = SetUser(11004);
        await SeedRelationAsync(parentId, 19054);
        await SeedSubscriptionAsync(parentId, 19054);
        await SeedMasteryAsync(19054, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedBankAsync("chinese-7to9", Subject.Chinese);
        await SeedQuestionAsync("Q-ch-7a-0001", "chinese-7to9", "7a", ["岳阳楼记"]);  // 本学生薄弱点关联题 → 应包含
        await SeedQuestionAsync("Q-ch-7a-0002", "chinese-7to9", "7b", ["滕王阁序"]);  // 他人知识点 → 不混入（内存 kp 过滤）
        await SeedBankAsync("math-7to9", Subject.Math);
        await SeedQuestionAsync("Q-ma-7a-0001", "math-7to9", "7a", ["岳阳楼记"]);     // 他学科题库同 kp → 不混入（题库收窄排除）
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19054 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var weak = Assert.Single(result.WeakPoints);
        Assert.Equal("岳阳楼记", weak.KnowledgePoint);
        Assert.Equal(new[] { "Q-ch-7a-0001" }, weak.QuestionIds); // 仅本学生薄弱点关联题
    }

    /// <summary>C3 Topic 富化主路径：关联题有 Topic → 透出 Topic；无 Topic 题目 → Topic null</summary>
    [Fact]
    public async Task ExecuteAsync_WithTopic_DrillsTopic()
    {
        var parentId = SetUser(11005);
        await SeedRelationAsync(parentId, 19055);
        await SeedSubscriptionAsync(parentId, 19055);
        await SeedMasteryAsync(19055, "岳阳楼记", 0.3, MemoryState.NotMastered);
        await SeedMasteryAsync(19055, "滕王阁序", 0.9, MemoryState.Proficient);
        await SeedBankAsync("chinese-7to9", Subject.Chinese);
        await SeedQuestionAsync("Q-ch-7a-0001", "chinese-7to9", "7a", ["岳阳楼记"], topic: "七年级上册-古文");
        await SeedQuestionAsync("Q-ch-7a-0002", "chinese-7to9", "7b", ["滕王阁序"]);
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19055 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var withTopic = result.WeakPoints.Single(w => w.KnowledgePoint == "岳阳楼记");
        Assert.Equal("七年级上册-古文", withTopic.Topic);
        Assert.Equal("7a", withTopic.ChapterId);
        var withoutTopic = result.WeakPoints.Single(w => w.KnowledgePoint == "滕王阁序");
        Assert.Null(withoutTopic.Topic);   // 题目无 Topic → 透出 null
        Assert.Equal("7b", withoutTopic.ChapterId);
    }

    /// <summary>C3 Topic 富化：多题不同 Topic → 取首个非空（按 QuestionId 序，与 ChapterId 同口径）</summary>
    [Fact]
    public async Task ExecuteAsync_MultipleTopics_TakesFirstNonNull()
    {
        var parentId = SetUser(11006);
        await SeedRelationAsync(parentId, 19056);
        await SeedSubscriptionAsync(parentId, 19056);
        await SeedMasteryAsync(19056, "观沧海", 0.3, MemoryState.NotMastered);
        await SeedBankAsync("chinese-7to9", Subject.Chinese);
        await SeedQuestionAsync("Q-ch-7a-0002", "chinese-7to9", "7b", ["观沧海"], topic: "七年级下册-古诗文");
        await SeedQuestionAsync("Q-ch-7a-0001", "chinese-7to9", "7a", ["观沧海"], topic: "七年级上册-古诗文");
        var svc = User.Use<GetWeaknessReportService>();

        var result = await svc.ExecuteAsync(new GetWeaknessReportReqDto { StudentId = 19056 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var weak = Assert.Single(result.WeakPoints);
        Assert.Equal("观沧海", weak.KnowledgePoint);
        Assert.Equal("七年级上册-古诗文", weak.Topic); // 首个非空（按 QuestionId 序）
        Assert.Equal("7a", weak.ChapterId);
    }
}