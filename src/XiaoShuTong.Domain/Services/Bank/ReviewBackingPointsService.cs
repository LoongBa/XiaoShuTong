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
/// UC-B.8：背诵点校验入库（AI 草稿 → 人工校验 → 写权威文件 + upsert 索引）
/// </summary>
/// <remarks>
/// BR-28 未校验条目需显式确认跳过 | BR-29 AI 草稿不可静默入库（人工校验硬门槛）| BR-30 文件+索引原子 | BR-31 批次必须存在且属当前题库
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class ReviewBackingPointsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 校验并入库背诵点（仅显式确认条目）
    /// </summary>
    public async Task<ReviewBackingPointsResDto> ExecuteAsync(ReviewBackingPointsReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-11 复用：题库存在且为 Owner
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new ReviewBackingPointsResDto { Success = false, ErrorCode = BankErrorCodes.BankNotFound };
        if (bank.OwnerId != ownerId)
            return new ReviewBackingPointsResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        // BR-31：校验批次必须存在且属于当前题库
        var batch = DraftBackingPointStore.Get(request.BatchId);
        if (batch == null || batch.BankId != request.BankId)
            return new ReviewBackingPointsResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        // BR-28：存在未校验条目时需显式确认跳过
        var approvedIds = request.Items.Select(i => i.QuestionId).ToHashSet(StringComparer.Ordinal);
        var unreviewedCount = batch.Items.Count(i => !i.Reviewed && !approvedIds.Contains(i.QuestionId));
        if (unreviewedCount > 0 && !request.SkipUnreviewed)
            return new ReviewBackingPointsResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        // BR-29/BR-30：仅显式确认（通过/编辑）条目入库——写权威文件 + upsert 索引
        var imported = 0;
        var skipped = unreviewedCount;

        var contentFile = LoadOrCreateContentFileAsync(bank.JsonPath);
        foreach (var item in request.Items)
        {
            var draft = batch.Items.FirstOrDefault(d => d.QuestionId == item.QuestionId);
            if (draft == null)
                continue;

            var stem = string.IsNullOrWhiteSpace(item.EditedStem) ? draft.Stem : item.EditedStem;
            var answer = string.IsNullOrWhiteSpace(item.EditedAnswer) ? draft.Answer : item.EditedAnswer;
            var keywordGroups = ImportQuestionsService.ExtractKeywordGroups(answer);

            contentFile.Questions.Add(new ContentQuestionRecord(item.QuestionId, stem, answer, keywordGroups));
            ContentFileStore.Save(bank.JsonPath, SerializeContent(contentFile));

            await QuestionsDs.EntityCreateAsync(new Questions
            {
                UId = UidGenerator.NewId(),
                QuestionId = item.QuestionId,
                BankId = bank.BankId,
                ChapterId = null,
                QType = QuestionType.R1,
                Content = JsonSerializer.Serialize(new { item.QuestionId, Stem = stem }),
                Keywords = JsonSerializer.Serialize(keywordGroups),
                KnowledgePoints = draft.KnowledgePoint.Length > 0 ? [draft.KnowledgePoint] : [],
                Difficulty = 0,
                Status = QuestionStatus.Active,
            }, ct);

            imported++;
        }

        return new ReviewBackingPointsResDto { Success = true, Imported = imported, Skipped = skipped };
    }

    private sealed record ContentQuestionRecord(string QuestionId, string Stem, string Answer, List<KeywordGroup> Keywords);
    private sealed record ContentFileRecord(string BankId, string Version, List<ContentQuestionRecord> Questions);

    private static string SerializeContent(object content)
        => JsonSerializer.Serialize(content, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private ContentFileRecord LoadOrCreateContentFileAsync(string jsonPath)
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

/// <summary>校验入库请求 DTO</summary>
public sealed record ReviewBackingPointsReqDto
{
    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>校验批次 ID</summary>
    public string BatchId { get; init; } = string.Empty;

    /// <summary>逐条（通过/编辑后内容）</summary>
    public List<ReviewItemInputDto> Items { get; init; } = [];

    /// <summary>跳过未校验确认</summary>
    public bool SkipUnreviewed { get; init; }
}

/// <summary>校验条目输入 DTO</summary>
public sealed record ReviewItemInputDto
{
    /// <summary>草稿题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>编辑后题干（空=用草稿原样）</summary>
    public string? EditedStem { get; init; }

    /// <summary>编辑后答案（空=用草稿原样）</summary>
    public string? EditedAnswer { get; init; }
}

/// <summary>校验入库响应 DTO</summary>
public sealed record ReviewBackingPointsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>入库数</summary>
    public int Imported { get; init; }

    /// <summary>跳过数</summary>
    public int Skipped { get; init; }
}