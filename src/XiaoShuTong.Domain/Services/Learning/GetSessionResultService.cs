using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Learning.DTOs;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.6：会话结果
/// </summary>
/// <remarks>
/// BR-39 会话必须存在且属于当前用户 → 3001 | BR-40 空会话返回空统计
/// BR-41 NewStarCount = PostState=Proficient 且 PreState&lt;Proficient 的次数 | BR-42 BlockedPoints = ✕/△ 题目 + 知识点
/// 知识点映射（跨模块题库域）以 LearningQuestionRegistry 桩代替。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetSessionResultService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    /// <summary>
    /// 聚合会话作答统计（答对数/新增★/卡壳知识点）
    /// </summary>
    public async Task<GetSessionResultResDto> ExecuteAsync(GetSessionResultReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-39：会话必须存在且属于当前用户 → 3001
        var session = await SessionsDs.EntityGetAsync(x => x.UId == request.SessionUid, ct);
        if (session == null || session.UserId != userId)
            return new GetSessionResultResDto { Success = false, ErrorCode = LearningErrorCodes.SessionNotFound };

        var attempts = await AttemptsDs.EntitySelectAsync(
            x => x.SessionId == session.Id, ct: ct);

        // BR-40：空会话 → 返回空统计
        if (attempts.Count == 0)
        {
            return new GetSessionResultResDto
            {
                Success = true,
                CorrectCount = 0,
                TotalCount = 0,
                NewStarCount = 0,
                BlockedPoints = [],
            };
        }

        var correctCount = attempts.Count(a => a.Result == JudgmentResult.Correct);

        // BR-41：新增 ★ = PostState=Proficient 且 PreState<Proficient 的次数
        var newStarCount = attempts.Count(a =>
            a.PostState == MemoryState.Proficient && a.PreState != MemoryState.Proficient);

        // BR-42：卡壳知识点 = ✕/△ 题目 + 知识点（跨模块映射桩）
        var blockedPoints = attempts
            .Where(a => a.PostState is MemoryState.NotMastered or MemoryState.Fuzzy)
            .GroupBy(a => a.QuestionId)
            .Select(g =>
            {
                var meta = LearningQuestionRegistry.Get(g.Key);
                return new BlockedPointDto
                {
                    QuestionId = g.Key,
                    KnowledgePoint = meta?.KnowledgePoint ?? string.Empty,
                    State = g.OrderByDescending(x => x.AnsweredAt).First().PostState.ToString(),
                };
            })
            .ToList();

        return new GetSessionResultResDto
        {
            Success = true,
            CorrectCount = correctCount,
            TotalCount = attempts.Count,
            NewStarCount = newStarCount,
            BlockedPoints = blockedPoints,
        };
    }
}

/// <summary>会话结果请求 DTO</summary>
public sealed record GetSessionResultReqDto
{
    /// <summary>会话外部键</summary>
    public string SessionUid { get; init; } = string.Empty;
}

/// <summary>会话结果响应 DTO</summary>
public sealed record GetSessionResultResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>答对数</summary>
    public int CorrectCount { get; init; }

    /// <summary>总题数</summary>
    public int TotalCount { get; init; }

    /// <summary>新增 ★ 数</summary>
    public int NewStarCount { get; init; }

    /// <summary>下次重点复习（卡壳知识点）</summary>
    public List<BlockedPointDto> BlockedPoints { get; init; } = [];
}

/// <summary>卡壳知识点 DTO</summary>
public sealed record BlockedPointDto
{
    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>知识点</summary>
    public string KnowledgePoint { get; init; } = string.Empty;

    /// <summary>记忆状态</summary>
    public string State { get; init; } = string.Empty;
}
