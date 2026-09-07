using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.6：知识卡片
/// </summary>
/// <remarks>
/// BR-21 题目必须存在 → 1502 | BR-22 无关联卡片返回空 | BR-23 卡片按题库模板渲染
/// 卡片内容从题目 Content 镜像的 card/cardType 字段提取（切片验证）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetKnowledgeCardService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 取题目关联知识卡片
    /// </summary>
    public async Task<GetKnowledgeCardResDto> ExecuteAsync(GetKnowledgeCardReqDto request, CancellationToken ct = default)
    {
        // BR-21：题目必须存在 → 1502
        var question = await QuestionsDs.EntityGetAsync(x => x.QuestionId == request.QuestionId, ct);
        if (question == null)
            return new GetKnowledgeCardResDto { Success = false, ErrorCode = BankErrorCodes.QuestionNotInBank };

        // BR-22/BR-23：从 Content 镜像提取卡片（无则返回空 content，不报错）
        var (cardType, content) = ExtractCard(question.Content);

        return new GetKnowledgeCardResDto
        {
            Success = true,
            CardType = cardType,
            Content = content,
        };
    }

    private static (string CardType, string Content) ExtractCard(string contentJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            var cardType = root.TryGetProperty("cardType", out var t) ? t.GetString() : "authorCard";
            var content = root.TryGetProperty("card", out var c) ? c.GetRawText() : "{}";
            return (cardType ?? "authorCard", content);
        }
        catch (System.Text.Json.JsonException)
        {
            return ("authorCard", "{}");
        }
    }
}

/// <summary>知识卡片请求 DTO</summary>
public sealed record GetKnowledgeCardReqDto
{
    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;
}

/// <summary>知识卡片响应 DTO</summary>
public sealed record GetKnowledgeCardResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>卡片类型（authorCard/wordCard/eventCard）</summary>
    public string CardType { get; init; } = "authorCard";

    /// <summary>卡片内容（按题库模板）</summary>
    public string Content { get; init; } = "{}";
}