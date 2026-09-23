using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.2b：结束学习会话（会话收尾：EndedAt/CorrectCount/TotalTimeMs 写入，G-4 ADR-009 决策二）
/// </summary>
/// <remarks>
/// BR-39 会话必须存在且属于当前用户 → 3001 | 幂等：EndedAt 已置则回读当前值不覆盖（BR-05 语义配合）
/// 聚合口径与 GetSessionResultService 一致（SessionResultAggregator 共享，ADR-009 决策二）。
/// BR-24 TaskAssignments.Progress 归 SubmitAttempt 职责（V0.6.0+），本服务不联动。
/// EndReason 为契约参数预留（语义标记 NaturalExhaust/UserExit/Timeout），V0.5.0 接收即丢弃不落库（V0.6.0+ 评估加列）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class EndStudySessionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    /// <summary>
    /// 结束会话：聚合写回 EndedAt/CorrectCount/TotalTimeMs（幂等，已结束回读）
    /// </summary>
    public async Task<EndStudySessionResDto> ExecuteAsync(EndStudySessionReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-39：会话必须存在且属于当前用户 → 3001
        var session = await SessionsDs.EntityGetAsync(x => x.UId == request.SessionUid, ct);
        if (session == null || session.UserId != userId)
            return new EndStudySessionResDto { Success = false, ErrorCode = LearningErrorCodes.SessionNotFound, SessionUid = request.SessionUid };

        // 幂等：已结束 → 回读当前值，不重复聚合不覆盖（BR-05 语义配合；前端重复调用友好）
        if (session.EndedAt.HasValue)
        {
            return new EndStudySessionResDto
            {
                Success = true,
                SessionUid = session.UId,
                EndedAt = session.EndedAt,
                CorrectCount = session.CorrectCount,
                TotalTimeMs = session.TotalTimeMs,
            };
        }

        // 聚合（BR-40 空会话：attempts 为空 → Correct=0/TotalTimeMs=0，仍写 EndedAt 标记结束）
        var attempts = await AttemptsDs.EntitySelectAsync(x => x.SessionId == session.Id, ct: ct);
        var (correctCount, totalTimeMs, _, _) = SessionResultAggregator.AggregateFromAttempts(attempts);

        // 写入收尾三字段（BR-24 Progress 归 SubmitAttempt，V0.6.0+ 不联动）
        session.EndedAt = DateTime.UtcNow;
        session.CorrectCount = correctCount;
        session.TotalTimeMs = (int)totalTimeMs;
        await SessionsDs.EntityUpdateAsync(session, ct);

        return new EndStudySessionResDto
        {
            Success = true,
            SessionUid = session.UId,
            EndedAt = session.EndedAt,
            CorrectCount = session.CorrectCount,
            TotalTimeMs = session.TotalTimeMs,
        };
    }
}

/// <summary>结束学习会话请求 DTO</summary>
public sealed record EndStudySessionReqDto
{
    /// <summary>会话外部键（createStudySession 产出）</summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>可选结束原因（NaturalExhaust/UserExit/Timeout 语义标记；V0.5.0 预留不落库，V0.6.0+ 评估加列）</summary>
    public string? EndReason { get; init; }
}

/// <summary>结束学习会话响应 DTO（独立响应形状：回显写回值，供前端结果页兜底）</summary>
public sealed record EndStudySessionResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>会话外部键回显</summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>结束时间（ISO8601 UTC；幂等时回读原值）</summary>
    public DateTime? EndedAt { get; init; }

    /// <summary>答对数（写回值回显）</summary>
    public int CorrectCount { get; init; }

    /// <summary>总耗时毫秒（写回值回显）</summary>
    public long TotalTimeMs { get; init; }
}
