using System.Text.Json;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Services.Bank;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Bank;

/// <summary>
/// UC-B.4 导入题目（ImportQuestionsService）Contract 测试
/// 覆盖 BR：BR-11 题库存在且 Owner | BR-12 大小预检 | BR-13 仅 Owner | BR-14 部分失败行号 | BR-15 文件+索引 | BR-16 关键词提取
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ImportQuestionsServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
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
            Name = "导入测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Private,
            OwnerId = ownerId,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private const string ValidText = """
        岳阳楼记###若夫淫雨霏霏，连月不开
        观沧海###东临碣石，以观沧海
        """;

    /// <summary>主流程 + BR-15/16：txt 导入 → 题目索引写入 + 内容权威文件含答案 + 关键词提取</summary>
    [Fact]
    public async Task ExecuteAsync_ValidText_ImportsAndWritesContentFile()
    {
        var ownerId = SetUser(34001);
        var bank = await SeedBankAsync(ownerId, "bank-imp-34001");
        var svc = User.Use<ImportQuestionsService>();

        var result = await svc.ExecuteAsync(new ImportQuestionsReqDto
        {
            BankId = bank.BankId,
            RawText = ValidText,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);

        // BR-15：内容权威文件已写入（含答案——答案只在文件，永不下发客户端）
        var fileContent = ContentFileStore.Read(bank.JsonPath);
        Assert.NotNull(fileContent);
        Assert.Contains("若夫淫雨霏霏", fileContent);

        // 题目索引已 upsert（Content 镜像不含答案）
        var questionsDs = User.Use<QuestionsDataService>();
        var questions = await questionsDs.EntitySelectAsync(
            x => x.BankId == bank.BankId, ct: TestContext.Current.CancellationToken);
        Assert.Equal(2, questions.Count);
        Assert.All(questions, q => Assert.DoesNotContain("answer", q.Content, StringComparison.OrdinalIgnoreCase));

        // BR-16：关键词已提取入库
        Assert.All(questions, q =>
        {
            var groups = JsonSerializer.Deserialize<List<KeywordGroup>>(q.Keywords);
            Assert.NotNull(groups);
            Assert.NotEmpty(groups);
        });
    }

    /// <summary>BR-14：部分失败返回行号明细</summary>
    [Fact]
    public async Task ExecuteAsync_BadLines_ReturnsFailureDetails()
    {
        var ownerId = SetUser(34002);
        var bank = await SeedBankAsync(ownerId, "bank-imp-34002");
        var svc = User.Use<ImportQuestionsService>();

        var result = await svc.ExecuteAsync(new ImportQuestionsReqDto
        {
            BankId = bank.BankId,
            RawText = "好的题目###正确作答内容\n这一行格式不对没有分隔符",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("行 2"));
    }

    /// <summary>BR-11：非法 bankId → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_ReturnsBankNotFound()
    {
        SetUser(34003);
        var svc = User.Use<ImportQuestionsService>();

        var result = await svc.ExecuteAsync(new ImportQuestionsReqDto
        {
            BankId = "no-such-bank",
            RawText = ValidText,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-13：非 Owner → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsForbidden()
    {
        await SeedBankAsync(999901, "bank-imp-34004");
        SetUser(34004); // 非 Owner
        var svc = User.Use<ImportQuestionsService>();

        var result = await svc.ExecuteAsync(new ImportQuestionsReqDto
        {
            BankId = "bank-imp-34004",
            RawText = ValidText,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-12：超大文件 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_OversizeFile_ReturnsParamInvalid()
    {
        var ownerId = SetUser(34005);
        var bank = await SeedBankAsync(ownerId, "bank-imp-34005");
        var svc = User.Use<ImportQuestionsService>();

        var result = await svc.ExecuteAsync(new ImportQuestionsReqDto
        {
            BankId = bank.BankId,
            File = new byte[11 * 1024 * 1024], // 11M > 10M
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(BankErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-14：重复题检测（同库 QuestionId 唯一）</summary>
    [Fact]
    public async Task ExecuteAsync_DuplicateQuestion_Skipped()
    {
        var ownerId = SetUser(34006);
        var bank = await SeedBankAsync(ownerId, "bank-imp-34006");
        var svc = User.Use<ImportQuestionsService>();

        // 导入两次相同内容（QuestionId 由 bankId+序号生成，二次导入序号重置 → 全重复）
        await svc.ExecuteAsync(new ImportQuestionsReqDto { BankId = bank.BankId, RawText = ValidText }, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(new ImportQuestionsReqDto { BankId = bank.BankId, RawText = ValidText }, TestContext.Current.CancellationToken);

        Assert.Equal(0, second.Imported);
        Assert.Equal(2, second.Failed);
        Assert.Contains(second.Failures, f => f.Contains("重复"));
    }
}
