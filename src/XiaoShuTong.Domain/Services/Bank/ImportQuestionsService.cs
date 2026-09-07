using System.Text.Encodings.Web;
using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.4：导入题目（txt/JSON，内容权威文件 + 索引 upsert 原子）
/// </summary>
/// <remarks>
/// BR-11 题库存在且 Owner | BR-12 文件 ≤10M / txt·json | BR-13 仅 Owner | BR-14 部分失败行号明细
/// BR-15 文件写入 + 索引 upsert 原子 | BR-16 自动提取判题关键词
/// txt 格式：`问题###答案`，`#` 注释行；切片以 RawText 为主（File 二进制为 WebApi 层抽取后传入）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class ImportQuestionsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int MaxFileBytes = 10 * 1024 * 1024; // BR-12：≤10M

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 解析导入文本 → 提取关键词 → 写内容权威文件 → upsert 题目索引
    /// </summary>
    public async Task<ImportQuestionsResDto> ExecuteAsync(ImportQuestionsReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-11/BR-13：题库存在且为 Owner
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new ImportQuestionsResDto { Success = false, ErrorCode = BankErrorCodes.BankNotFound };
        if (bank.OwnerId != ownerId)
            return new ImportQuestionsResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        // BR-12：预检（文件大小 / 文本长度）
        if (request.File is { Length: > MaxFileBytes })
            return new ImportQuestionsResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };
        if (string.IsNullOrWhiteSpace(request.RawText) && request.File == null)
            return new ImportQuestionsResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        var text = request.File != null
            ? System.Text.Encoding.UTF8.GetString(request.File)
            : request.RawText!;

        // 解析 txt（问题###答案，# 注释）+ 逐行失败明细（BR-14）
        var parsed = ParseQuestions(text, bank.BankId, request.Topic);
        var failures = parsed.Failures;

        if (parsed.Questions.Count == 0)
        {
            return new ImportQuestionsResDto
            {
                Success = failures.Count == 0,
                Imported = 0,
                Failed = failures.Count,
                Failures = failures.ToArray(),
            };
        }

        // 重复题检测（同库内 QuestionId 唯一）+ 关键词提取（BR-16）
        foreach (var q in parsed.Questions)
        {
            var exists = await QuestionsDs.EntityGetAsync(
                x => x.BankId == bank.BankId && x.QuestionId == q.QuestionId, ct);
            if (exists != null)
            {
                failures.Add($"题目 {q.QuestionId} 重复，跳过");
                continue;
            }

            // BR-16：自动提取判题关键词（答案分段 → KeywordGroup[]）
            var keywordGroups = ExtractKeywordGroups(q.Answer);

            // BR-15：写内容权威文件（含答案，权威源）
            var contentFile = await LoadOrCreateContentFileAsync(bank.JsonPath, ct);
            contentFile.Questions.Add(new ContentQuestionRecord(q.QuestionId, q.Stem, q.Answer, keywordGroups));
            ContentFileStore.Save(bank.JsonPath, SerializeContent(contentFile));

            // upsert 题目索引（Content 镜像不含答案；Keywords 服务端校验用）
            await QuestionsDs.EntityCreateAsync(new Questions
            {
                UId = UidGenerator.NewId(),
                QuestionId = q.QuestionId,
                BankId = bank.BankId,
                ChapterId = q.ChapterId,
                QType = q.QType,
                Content = JsonSerializer.Serialize(new { q.QuestionId, q.Stem, q.ChapterId }),
                Keywords = JsonSerializer.Serialize(keywordGroups),
                KnowledgePoints = q.KnowledgePoints,
                Difficulty = 0,
                Status = QuestionStatus.Active,
            }, ct);
        }

        var imported = parsed.Questions.Count - failures.Count(f => f.StartsWith("题目 "));
        return new ImportQuestionsResDto
        {
            Success = true,
            Imported = imported,
            Failed = failures.Count,
            Failures = failures.ToArray(),
        };
    }

    /// <summary>解析结果</summary>
    internal sealed record ParsedQuestion(string QuestionId, string Stem, string Answer, string? ChapterId, QuestionType QType, string[] KnowledgePoints);

    /// <summary>
    /// 解析 txt 内容：`问题###答案`，`#` 注释行；返回题目列表 + 失败明细（含行号）
    /// </summary>
    internal static (List<ParsedQuestion> Questions, List<string> Failures) ParseQuestions(string text, string bankId, string? topic)
    {
        var questions = new List<ParsedQuestion>();
        var failures = new List<string>();

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var seq = 1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var idx = line.IndexOf("###", StringComparison.Ordinal);
            if (idx <= 0 || idx >= line.Length - 3)
            {
                failures.Add($"行 {i + 1}: 格式错误（应为 问题###答案）");
                continue;
            }

            var stem = line[..idx].Trim();
            var answer = line[(idx + 3)..].Trim();
            if (stem.Length == 0 || answer.Length == 0)
            {
                failures.Add($"行 {i + 1}: 问题或答案为空");
                continue;
            }

            var questionId = $"Q-{SanitizeBankId(bankId)}-{seq:D4}";
            seq++;
            questions.Add(new ParsedQuestion(
                questionId, stem, answer, topic,
                QuestionType.R1, // 切片：txt 导入默认补全记忆题型
                [topic ?? "默认"]));
        }

        return (questions, failures);
    }

    /// <summary>
    /// BR-16：从答案提取判题关键词（按常见分隔符分段，≥2 字成组，默认权重 1 非必中）
    /// </summary>
    internal static List<KeywordGroup> ExtractKeywordGroups(string answer)
    {
        var segments = answer.Split(['，', '。', '、', '；', '：', ',', '.', ';', ' ', '\t', '！', '？', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var groups = new List<KeywordGroup>();
        foreach (var segment in segments)
        {
            if (segment.Length < 2)
                continue;
            groups.Add(new KeywordGroup([segment], Weight: 1d, Required: false));
        }
        return groups;
    }

    private static string SanitizeBankId(string bankId)
        => new string(bankId.Where(char.IsLetterOrDigit).ToArray());

    private sealed record ContentQuestionRecord(string QuestionId, string Stem, string Answer, List<KeywordGroup> Keywords);
    private sealed record ContentFileRecord(string BankId, string Version, List<ContentQuestionRecord> Questions);

    private static string SerializeContent(object content)
        => JsonSerializer.Serialize(content, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private async Task<ContentFileRecord> LoadOrCreateContentFileAsync(string jsonPath, CancellationToken ct)
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

/// <summary>导入题目请求 DTO</summary>
public sealed record ImportQuestionsReqDto
{
    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>上传文件（.txt/.json，WebApi 层二进制）</summary>
    public byte[]? File { get; init; }

    /// <summary>批量粘贴文本（txt 格式 问题###答案）</summary>
    public string? RawText { get; init; }

    /// <summary>目标章节</summary>
    public string? Topic { get; init; }
}

/// <summary>导入题目响应 DTO</summary>
public sealed record ImportQuestionsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>成功导入数</summary>
    public int Imported { get; init; }

    /// <summary>失败数</summary>
    public int Failed { get; init; }

    /// <summary>失败明细（行号 + 原因）</summary>
    public string[] Failures { get; init; } = [];
}
