using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.4b/UC-B.4c 题库 JSON 导入 + 存量 Hint 回填（ImportBankJsonService / BankHintBackfillJob）Contract 测试
/// 覆盖 BR：BR-11/13 权限（BankNotFound/Forbidden）| BR-15 文件+索引原子 | ADR-010 决策六 Hint join
/// 幂等键 = QuestionId upsert | JsonPath 生产首选 | 回填匹配算法（精确/包含/未命中上报）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ImportBankJsonServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(string bankId, long? ownerId = null)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "JSON 导入测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private const string SampleQuestionsJson = """
        [
          {
            "id": "Q-chinese-7to9-9001",
            "bank_id": "bank-json-001",
            "subject": "chinese",
            "type": "R1",
            "content": { "question": "补全：东临碣石，___", "answer": "以观沧海", "knowledge_points": ["观沧海"] },
            "meta": { "difficulty": 1, "source": "测试", "knowledge_card_id": "KC-chinese-观沧海" }
          }
        ]
        """;

    private const string SampleCardsJson = """
        [
          { "id": "KC-chinese-观沧海", "title": "观沧海", "subject": "chinese", "template": "author_card",
            "fields": { "记忆钩子": "碣石山是秦皇汉武登临之地，曹操借观海抒壮志。" } }
        ]
        """;

    /// <summary>按实际 BankId 生成测试 JSON（bank_id 字段对齐；ParseQuestionsJson 过滤不匹配题库）</summary>
    private static string JsonFor(string bankId) => SampleQuestionsJson.Replace("bank-json-001", bankId);

    private async Task<Questions?> GetQuestionAsync(string bankId, string questionId)
    {
        var ds = User.Use<QuestionsDataService>();
        return await ds.EntityGetAsync(x => x.BankId == bankId && x.QuestionId == questionId,
            ct: TestContext.Current.CancellationToken);
    }

    /// <summary>BR-11：题库不存在 → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_ReturnsBankNotFound()
    {
        SetUser(45001);
        var svc = User.Use<ImportBankJsonService>();

        var result = await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "no-such",
            JsonContent = JsonFor("no-such"),
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-13：非 Owner → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsForbidden()
    {
        SetUser(45002);
        await SeedBankAsync("bank-json-45002", ownerId: 999999);

        var svc = User.Use<ImportBankJsonService>();
        var result = await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "bank-json-45002",
            JsonContent = JsonFor("bank-json-45002"),
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>ADR-010 决策六：JSON 导入 → Hint 经 knowledge_card_id join 卡片记忆钩子 + Content card 镜像</summary>
    [Fact]
    public async Task ExecuteAsync_JsonImport_FillsHintAndContentCard()
    {
        var userId = SetUser(45003);
        await SeedBankAsync("bank-json-45003", ownerId: userId);

        var svc = User.Use<ImportBankJsonService>();
        var result = await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "bank-json-45003",
            JsonContent = JsonFor("bank-json-45003"),
            KnowledgeCardsJson = SampleCardsJson,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.HintFilled);

        var question = await GetQuestionAsync("bank-json-45003", "Q-chinese-7to9-9001");
        Assert.NotNull(question);
        Assert.Equal("碣石山是秦皇汉武登临之地，曹操借观海抒壮志。", question.Hint); // 卡片记忆钩子
        // Content 镜像补 cardType/card（GetKnowledgeCardService 读取依据）
        Assert.Contains("cardType", question.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Card", question.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>无卡片映射 → Hint 留空，导入仍成功</summary>
    [Fact]
    public async Task ExecuteAsync_JsonImport_NoCard_HintEmpty()
    {
        var userId = SetUser(45004);
        await SeedBankAsync("bank-json-45004", ownerId: userId);

        var svc = User.Use<ImportBankJsonService>();
        var result = await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "bank-json-45004",
            JsonContent = JsonFor("bank-json-45004"), // 无 KnowledgeCardsJson → 卡片映射为空
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.HintFilled);

        var question = await GetQuestionAsync("bank-json-45004", "Q-chinese-7to9-9001");
        Assert.NotNull(question);
        Assert.True(string.IsNullOrEmpty(question.Hint)); // 无卡片 → 留空
    }

    /// <summary>幂等键 = QuestionId：重复导入 upsert（不重复创建）</summary>
    [Fact]
    public async Task ExecuteAsync_Reimport_UpsertsNotDuplicates()
    {
        var userId = SetUser(45005);
        await SeedBankAsync("bank-json-45005", ownerId: userId);

        var svc = User.Use<ImportBankJsonService>();
        await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "bank-json-45005",
            JsonContent = JsonFor("bank-json-45005"),
            KnowledgeCardsJson = SampleCardsJson,
        }, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "bank-json-45005",
            JsonContent = JsonFor("bank-json-45005"),
            KnowledgeCardsJson = SampleCardsJson,
        }, TestContext.Current.CancellationToken);

        Assert.True(second.Success);
        Assert.Equal(1, second.Imported); // upsert 不新增

        var ds = User.Use<QuestionsDataService>();
        var all = await ds.EntitySelectAsync(x => x.BankId == "bank-json-45005",
            ct: TestContext.Current.CancellationToken);
        Assert.Single(all); // 仅 1 条（幂等 upsert）
    }

    /// <summary>JsonPath 生产首选（读完文件内容导入）</summary>
    [Fact]
    public async Task ExecuteAsync_JsonPath_MissingFile_ParamInvalid()
    {
        var userId = SetUser(45006);
        await SeedBankAsync("bank-json-45006", ownerId: userId);

        var svc = User.Use<ImportBankJsonService>();
        var result = await svc.ExecuteAsync(new ImportBankJsonReqDto
        {
            BankId = "bank-json-45006",
            JsonPath = "no-such-file.json", // 文件不存在 → ParamInvalid
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }
}

/// <summary>BankHintBackfillJob（UC-B.4c 存量回填）Contract 测试</summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class BankHintBackfillJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Banks> SeedBankAsync(string bankId)
    {
        var ds = User.Use<BanksDataService>();
        return await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "回填测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Questions> SeedQuestionAsync(string bankId, string questionId, string? hint, string knowledgePoint)
    {
        var ds = User.Use<QuestionsDataService>();
        return await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            ChapterId = "7a",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            Keywords = "[{\"Aliases\":[\"以观沧海\"],\"Weight\":1,\"Required\":false}]",
            KnowledgePoints = [knowledgePoint],
            Difficulty = 0,
            Status = QuestionStatus.Active,
            Hint = hint,
        }, TestContext.Current.CancellationToken);
    }

    private const string CardsJson = """
        [
          { "id": "KC-chinese-观沧海", "title": "观沧海", "subject": "chinese",
            "fields": { "记忆钩子": "碣石山是秦皇汉武登临之地，曹操借观海抒壮志。" } }
        ]
        """;

    /// <summary>Hint 空 → 按 KnowledgePoints[0] 匹配卡片补齐</summary>
    [Fact]
    public async Task ExecuteAsync_EmptyHint_BackfillsFromCard()
    {
        SetUser(46001);
        await SeedBankAsync("bank-backfill-46001");
        await SeedQuestionAsync("bank-backfill-46001", "Q-46001a", hint: null, knowledgePoint: "观沧海");

        var job = User.Use<BankHintBackfillJob>();
        var (filled, unmatched, total) = await job.ExecuteAsync(
            bankId: "bank-backfill-46001", knowledgeCardsJson: CardsJson, TestContext.Current.CancellationToken);

        Assert.Equal(1, filled);
        Assert.Equal(0, unmatched);
        Assert.Equal(1, total);

        var ds = User.Use<QuestionsDataService>();
        var question = await ds.EntityGetAsync(x => x.QuestionId == "Q-46001a", ct: TestContext.Current.CancellationToken);
        Assert.Equal("碣石山是秦皇汉武登临之地，曹操借观海抒壮志。", question!.Hint);
    }

    /// <summary>无卡片映射 → 留空 + 未命中计数（前端兜底文案已生效）</summary>
    [Fact]
    public async Task ExecuteAsync_NoCardMatch_LeavesEmptyAndCountsUnmatched()
    {
        SetUser(46002);
        await SeedBankAsync("bank-backfill-46002");
        await SeedQuestionAsync("bank-backfill-46002", "Q-46002a", hint: null, knowledgePoint: "不存在的知识点");

        var job = User.Use<BankHintBackfillJob>();
        var (filled, unmatched, total) = await job.ExecuteAsync(
            bankId: "bank-backfill-46002", knowledgeCardsJson: CardsJson, TestContext.Current.CancellationToken);

        Assert.Equal(0, filled);
        Assert.Equal(1, unmatched);
        Assert.Equal(1, total);

        var ds = User.Use<QuestionsDataService>();
        var question = await ds.EntityGetAsync(x => x.QuestionId == "Q-46002a", ct: TestContext.Current.CancellationToken);
        Assert.True(string.IsNullOrEmpty(question!.Hint)); // 留空 + 前端兜底
    }

    /// <summary>幂等：已有 Hint 不覆盖</summary>
    [Fact]
    public async Task ExecuteAsync_ExistingHint_NotOverwritten()
    {
        SetUser(46003);
        await SeedBankAsync("bank-backfill-46003");
        await SeedQuestionAsync("bank-backfill-46003", "Q-46003a", hint: "已有线索", knowledgePoint: "观沧海");

        var job = User.Use<BankHintBackfillJob>();
        var (filled, _, total) = await job.ExecuteAsync(
            bankId: "bank-backfill-46003", knowledgeCardsJson: CardsJson, TestContext.Current.CancellationToken);

        Assert.Equal(0, filled); // 已有 Hint 跳过（幂等）
        Assert.Equal(0, total);  // Hint 非空行不在回填范围
    }
}