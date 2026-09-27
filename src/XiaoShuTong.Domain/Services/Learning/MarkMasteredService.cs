using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.7 联动：手动标记错题已掌握/移回（学习-BR-52，V0.6.14）
/// </summary>
/// <remarks>
/// BR-52 手动标记已掌握：翻转 WrongQuestions.Mastered（学习域，激励-BR-15 域边界）；
/// 幂等（User+QuestionId 唯一）；BR-23 作答事件照常驱动不豁免（答错重置为 false）；
/// 并发以"最后写入胜出 + BR-23 作答事件权威"处置。
/// RLS：仅当前用户可翻转自己的错题记录（无记录 → 4007 类错误，见 LearningErrorCodes）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class MarkMasteredService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private WrongQuestionsDataService? _wrongDs;
    private WrongQuestionsDataService WrongDs => _wrongDs ??= User.Use<WrongQuestionsDataService>();

    /// <summary>
    /// 手动翻转错题已掌握状态（true=标记已掌握 / false=移回错题本）
    /// </summary>
    public async Task<MarkMasteredResDto> ExecuteAsync(MarkMasteredReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-52：RLS 定位（User+QuestionId 唯一，幂等基础）
        var wrong = await WrongDs.EntityGetAsync(
            x => x.UserId == userId && x.QuestionId == request.QuestionId, ct);
        if (wrong == null)
            return new MarkMasteredResDto { Success = false, ErrorCode = LearningErrorCodes.WrongNotFound };

        // 幂等：同值标记直接返回当前状态（不重复 Update）
        if (wrong.Mastered == request.Mastered)
            return new MarkMasteredResDto { Success = true, Mastered = wrong.Mastered };

        // 翻转 Mastered（BR-52；BR-23 作答事件后续仍权威覆写——答错强制 false）
        wrong.Mastered = request.Mastered;
        await WrongDs.EntityUpdateAsync(wrong, ct);

        return new MarkMasteredResDto { Success = true, Mastered = wrong.Mastered };
    }
}

/// <summary>手动标记错题已掌握请求 DTO</summary>
public sealed record MarkMasteredReqDto
{
    /// <summary>题目业务键（当前用户错题记录定位）</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>目标状态（true=标记已掌握 / false=移回错题本）</summary>
    public bool Mastered { get; init; }
}

/// <summary>手动标记错题已掌握响应 DTO</summary>
public sealed record MarkMasteredResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>翻转后状态（幂等返回当前值）</summary>
    public bool Mastered { get; init; }
}
