using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.4b：题库四件套 JSON 批量导入（ADR-010 决策六：Hint 经 knowledge_card_id join 背景钩子卡片）
/// </summary>
/// <remarks>
/// BR-11 题库存在且 Owner | BR-13 仅 Owner | BR-15 文件写入 + 索引 upsert 原子
/// 题库-BR-19 响应不含答案与关键词（Hint=线索非答案合规）| 学习-BR-29 提示 ≤20 字（读侧截断）
/// 幂等键 = QuestionId（全局业务键）：重复导入 upsert（Oracle 评审闭环 #9），对齐 ImportQuestionsService upsert 语义。
/// JsonPath 生产首选（服务端读题库目录，避免大 payload 经 RPC）；JsonContent 仅小批量/测试（Oracle 评审闭环 #5）。
/// Content 镜像补 cardType/card 字段（对齐 GetKnowledgeCardService 读取；Oracle 信息项 #10：填充空值不改读取契约）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class ImportBankJsonService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 解析背诵内容 JSON → 写内容权威文件 → upsert 题目索引（Hint 经 knowledge_card_id join 卡片）
    /// </summary>
    public async Task<ImportBankJsonResDto> ExecuteAsync(ImportBankJsonReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-11/BR-13：题库存在且为 Owner
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new ImportBankJsonResDto { Success = false, ErrorCode = BankErrorCodes.BankNotFound };
        if (bank.OwnerId != ownerId)
            return new ImportBankJsonResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        // 读取 JSON 内容（jsonPath 生产首选 / jsonContent 小批量测试）
        string jsonText;
        var jsonPath = request.JsonPath;
        if (!string.IsNullOrWhiteSpace(request.JsonContent))
        {
            jsonText = request.JsonContent;
        }
        else if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            if (!System.IO.File.Exists(jsonPath))
                return new ImportBankJsonResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };
            jsonText = await System.IO.File.ReadAllTextAsync(jsonPath, ct);
        }
        else
        {
            return new ImportBankJsonResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };
        }

        // 解析背诵内容 JSON
        List<JsonQuestion> questions;
        try
        {
            questions = ParseQuestionsJson(jsonText, bank.BankId);
        }
        catch (JsonException)
        {
            return new ImportBankJsonResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };
        }
        if (questions.Count == 0)
            return new ImportBankJsonResDto { Success = true, Imported = 0, Skipped = 0, HintFilled = 0 };

        // 知识卡片映射（knowledge_card_id → fields.记忆钩子；缺省从题库目录读）
        var cardMap = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var cardsJson = request.KnowledgeCardsJson
                ?? (FindKnowledgeCardsFile(bank) is { } cardPath ? await System.IO.File.ReadAllTextAsync(cardPath, ct) : null);
            if (!string.IsNullOrWhiteSpace(cardsJson))
                cardMap = BuildCardMap(cardsJson);
        }
        catch (JsonException)
        {
            cardMap = new Dictionary<string, string>(StringComparer.Ordinal); // 卡片解析失败 → Hint 留空
        }

        var imported = 0;
        var skipped = 0;
        var hintFilled = 0;
        var failures = new List<string>();

        foreach (var q in questions)
        {
            // 幂等键 = QuestionId（upsert：更新 Content/Keywords/Hint，不跳过）
            var existing = await QuestionsDs.EntityGetAsync(
                x => x.BankId == bank.BankId && x.QuestionId == q.QuestionId, ct);

            // Hint join：knowledge_card_id → 卡片 fields.记忆钩子（无卡片映射 → 留空）
            var hint = cardMap.GetValueOrDefault(q.KnowledgeCardId ?? string.Empty);

            // BR-16：自动提取判题关键词（答案分段 → KeywordGroup[]）
            var keywordGroups = ImportQuestionsService.ExtractKeywordGroups(q.Answer);

            // BR-15：写内容权威文件（含答案，权威源；Hint 是线索可随内容文件登记）
            var contentFile = await LoadOrCreateContentFileAsync(bank.JsonPath, ct) ?? new ContentFileRecord("", "V1.0", []);
            contentFile.Questions.RemoveAll(x => x.QuestionId == q.QuestionId); // upsert 语义
            contentFile.Questions.Add(new ContentQuestionRecord(q.QuestionId, q.Stem, q.Answer, keywordGroups, hint));
            ContentFileStore.Save(bank.JsonPath, SerializeContent(contentFile));

            // upsert 题目索引（Content 镜像不含答案/关键词，防爬 BR-19；补 cardType/card 供 GetKnowledgeCardService）
            var contentMirror = new { q.QuestionId, q.Stem, q.ChapterId, CardType = "authorCard", Card = hint };
            if (existing == null)
            {
                await QuestionsDs.EntityCreateAsync(new Questions
                {
                    UId = UidGenerator.NewId(),
                    QuestionId = q.QuestionId,
                    BankId = bank.BankId,
                    ChapterId = q.ChapterId,
                    QType = q.QType,
                    Content = JsonSerializer.Serialize(contentMirror),
                    Keywords = JsonSerializer.Serialize(keywordGroups),
                    KnowledgePoints = q.KnowledgePoints,
                    Difficulty = q.Difficulty,
                    Status = QuestionStatus.Active,
                    Hint = hint,
                }, ct);
            }
            else
            {
                existing.Content = JsonSerializer.Serialize(contentMirror);
                existing.Keywords = JsonSerializer.Serialize(keywordGroups);
                existing.KnowledgePoints = q.KnowledgePoints;
                existing.Hint = hint;
                await QuestionsDs.EntityUpdateAsync(existing, ct);
            }

            imported++;
            if (!string.IsNullOrWhiteSpace(hint))
                hintFilled++;
        }

        return new ImportBankJsonResDto
        {
            Success = true,
            Imported = imported,
            Skipped = skipped,
            HintFilled = hintFilled,
            Failures = failures.ToArray(),
        };
    }

    /// <summary>背诵内容 JSON 题目结构</summary>
    internal sealed record JsonQuestion(
        string QuestionId, string BankId, string Stem, string Answer, string? ChapterId,
        QuestionType QType, string[] KnowledgePoints, int Difficulty, string? KnowledgeCardId);

    /// <summary>解析背诵内容 JSON（id/bank_id/subject/type/content/meta.knowledge_card_id）</summary>
    internal static List<JsonQuestion> ParseQuestionsJson(string json, string expectedBankId)
    {
        var root = JsonNode.Parse(json);
        if (root is not JsonArray array)
            return [];

        var result = new List<JsonQuestion>();
        foreach (var node in array)
        {
            if (node is not JsonObject obj) continue;

            var questionId = obj["id"]?.GetValue<string>() ?? string.Empty;
            var bankId = obj["bank_id"]?.GetValue<string>() ?? string.Empty;
            var type = obj["type"]?.GetValue<string>() ?? "R1";
            var content = obj["content"] as JsonObject;
            var stem = content?["question"]?.GetValue<string>() ?? string.Empty;
            var answer = content?["answer"]?.GetValue<string>() ?? string.Empty;
            var difficulty = obj["meta"]?["difficulty"]?.GetValue<int>() ?? 0;
            var cardId = obj["meta"]?["knowledge_card_id"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(answer))
                continue;
            if (!string.IsNullOrWhiteSpace(expectedBankId) && bankId != expectedBankId)
                continue;

            result.Add(new JsonQuestion(
                questionId, bankId, stem, answer, obj["chapter_id"]?.GetValue<string>(),
                ParseQType(type), ParseKnowledgePoints(content), difficulty, cardId));
        }
        return result;
    }

    private static QuestionType ParseQType(string type)
        => type switch
        {
            "R2" => QuestionType.R2,
            "R3a" => QuestionType.R3a,
            "R3b" => QuestionType.R3b,
            "R4" => QuestionType.R4,
            "O1" => QuestionType.O1,
            "O2" => QuestionType.O2,
            "O3" => QuestionType.O3,
            "O4" => QuestionType.O4,
            "O5" => QuestionType.O5,
            _ => QuestionType.R1,
        };

    private static string[] ParseKnowledgePoints(JsonObject? content)
    {
        var kp = content?["knowledge_points"];
        if (kp is JsonArray arr)
            return arr.Select(x => x?.GetValue<string>() ?? string.Empty).Where(s => s.Length > 0).ToArray();
        return [];
    }

    /// <summary>构建 knowledge_card_id → fields.记忆钩子 映射</summary>
    internal static Dictionary<string, string> BuildCardMap(string cardsJson)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = JsonNode.Parse(cardsJson);
        if (root is not JsonArray array) return map;

        foreach (var node in array)
        {
            if (node is not JsonObject obj) continue;
            var id = obj["id"]?.GetValue<string>();
            var hook = obj["fields"]?["记忆钩子"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(hook))
                map[id] = hook;
        }
        return map;
    }

    /// <summary>从题库目录查找背景钩子-知识卡片文件（题库根目录 subject 与版本子目录下的 背景钩子-知识卡片.json）</summary>
    private static string? FindKnowledgeCardsFile(Banks bank)
    {
        // 官方题库 JsonPath 形如 bank.chinese-7to9-pep.json → 尝试在 题库/ 目录按 subject 定位（最佳努力）
        var subjectDir = bank.Subject.ToString().ToLowerInvariant() switch
        {
            "chinese" or "语文" => "题库/chinese",
            _ => null,
        };
        if (subjectDir == null || !System.IO.Directory.Exists(subjectDir))
            return null;
        return System.IO.Directory
            .EnumerateFiles(subjectDir, "背景钩子-知识卡片.json", System.IO.SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private sealed record ContentQuestionRecord(string QuestionId, string Stem, string Answer, List<KeywordGroup> Keywords, string? Hint);
    private sealed record ContentFileRecord(string BankId, string Version, List<ContentQuestionRecord> Questions);

    private static string SerializeContent(object content)
        => JsonSerializer.Serialize(content, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private async Task<ContentFileRecord?> LoadOrCreateContentFileAsync(string jsonPath, CancellationToken ct)
    {
        var existing = ContentFileStore.Read(jsonPath);
        if (existing != null)
        {
            try
            {
                return JsonSerializer.Deserialize<ContentFileRecord>(existing)
                       ?? new ContentFileRecord("", "V1.0", []);
            }
            catch (JsonException)
            {
                return new ContentFileRecord("", "V1.0", []);
            }
        }
        return new ContentFileRecord("", "V1.0", []);
    }
}

/// <summary>题库 JSON 导入请求 DTO</summary>
public sealed record ImportBankJsonReqDto
{
    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>生产首选：服务端读题库目录背诵内容 JSON 路径（避免大 payload 经 RPC，Oracle 评审闭环 #5）</summary>
    public string? JsonPath { get; init; }

    /// <summary>仅小批量/测试用例：背诵内容 JSON 全量文本</summary>
    public string? JsonContent { get; init; }

    /// <summary>背景钩子-知识卡片 JSON（可选，缺省从题库目录读）</summary>
    public string? KnowledgeCardsJson { get; init; }
}

/// <summary>题库 JSON 导入响应 DTO</summary>
public sealed record ImportBankJsonResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>成功导入数</summary>
    public int Imported { get; init; }

    /// <summary>跳过数</summary>
    public int Skipped { get; init; }

    /// <summary>Hint 填充数（knowledge_card_id join 卡片记忆钩子成功）</summary>
    public int HintFilled { get; init; }

    /// <summary>失败明细</summary>
    public string[] Failures { get; init; } = [];
}