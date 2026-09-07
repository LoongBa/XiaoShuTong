using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Judging;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Judging;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Judging;

/// <summary>
/// UC-J.2：判错反馈（"判错了"）
/// </summary>
/// <remarks>
/// BR-37 作答必须存在（跨模块学习域 Attempts）→ 9001 | BR-38 同一作答同用户同类型仅一条有效反馈（幂等）| BR-39 计入判题质量统计（切片不实施统计模块）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class SubmitJudgmentFeedbackService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    private JudgmentFeedbackDataService? _feedbackDs;
    private JudgmentFeedbackDataService FeedbackDs => _feedbackDs ??= User.Use<JudgmentFeedbackDataService>();

    /// <summary>
    /// 提交判错反馈（幂等：同作答同用户同类型仅一条）
    /// </summary>
    public async Task<SubmitJudgmentFeedbackResDto> ExecuteAsync(SubmitJudgmentFeedbackReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-37：作答记录必须存在（跨模块：Attempts.Uid）→ 9001
        var attempt = await AttemptsDs.EntityGetAsync(x => x.UId == request.AttemptUid, ct);
        if (attempt == null)
            return new SubmitJudgmentFeedbackResDto { Success = false, ErrorCode = JudgingErrorCodes.FeedbackNotFound };

        // BR-38：幂等——同一 (AttemptId, UserId, FeedbackType) 已存在 → 返回已有反馈
        var feedbackType = string.IsNullOrWhiteSpace(request.FeedbackType)
            ? FeedbackType.WrongJudgment
            : !Enum.TryParse<FeedbackType>(request.FeedbackType, true, out var parsed)
                ? FeedbackType.WrongJudgment
                : parsed;

        var existing = await FeedbackDs.EntityGetAsync(
            x => x.AttemptId == attempt.Id && x.UserId == userId && x.FeedbackType == feedbackType, ct);
        if (existing != null)
            return new SubmitJudgmentFeedbackResDto
            {
                Success = true,
                FeedbackUid = existing.UId,
                Status = existing.Status.ToString(),
            };

        // 写入反馈（Pending）
        var feedback = await FeedbackDs.EntityCreateAsync(new JudgmentFeedback
        {
            UId = UidGenerator.NewId(),
            AttemptId = attempt.Id,
            UserId = userId,
            FeedbackType = feedbackType,
            Status = FeedbackStatus.Pending,
        }, ct);

        return new SubmitJudgmentFeedbackResDto
        {
            Success = true,
            FeedbackUid = feedback.UId,
            Status = feedback.Status.ToString(),
        };
    }
}

/// <summary>判错反馈请求 DTO</summary>
public sealed record SubmitJudgmentFeedbackReqDto
{
    /// <summary>作答外部键（跨模块引用学习域）</summary>
    public string AttemptUid { get; init; } = string.Empty;

    /// <summary>反馈类型（默认 WrongJudgment）</summary>
    public string? FeedbackType { get; init; }
}

/// <summary>判错反馈响应 DTO</summary>
public sealed record SubmitJudgmentFeedbackResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>反馈外部键</summary>
    public string FeedbackUid { get; init; } = string.Empty;

    /// <summary>复核状态（Pending）</summary>
    public string Status { get; init; } = string.Empty;
}