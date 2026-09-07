using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Pk;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.4：PK 结果 + AI 点评
/// </summary>
/// <remarks>
/// BR-17 对局不存在 → 2001 | BR-18 AI 点评失败留空 + 兜底文案 | BR-19 合规：无正确率对比榜 | BR-20 胜负由 WinnerId 判定（NULL=平局）
/// 知识彩蛋（knowledgeEggs）响应态展示不落库（切片返回空数组）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetPkResultService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const string AiCommentFallback = "本次对战很精彩！";

    private PkMatchesDataService? _matchesDs;
    private PkMatchesDataService MatchesDs => _matchesDs ??= User.Use<PkMatchesDataService>();

    private PkPlayersDataService? _playersDs;
    private PkPlayersDataService PlayersDs => _playersDs ??= User.Use<PkPlayersDataService>();

    /// <summary>
    /// PK 结果（胜负 + 双方得分/用时/点评）
    /// </summary>
    public async Task<GetPkResultResDto> ExecuteAsync(GetPkResultReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-17：对局存在 → 2001
        var match = await MatchesDs.EntityGetAsync(x => x.UId == request.MatchUid, ct);
        if (match == null)
            return new GetPkResultResDto { Success = false, ErrorCode = PkErrorCodes.MatchNotFound };

        // 参赛者校验（BR-11 语义：仅参赛者可看结果）
        var players = await PlayersDs.EntitySelectAsync(x => x.MatchId == match.Id, ct: ct);
        if (!players.Any(p => p.UserId == userId))
            return new GetPkResultResDto { Success = false, ErrorCode = PkErrorCodes.NotParticipant };

        // 未结算（仍在进行）→ 返回进行中
        if (match.Status != PkMatchStatus.Finished)
            return new GetPkResultResDto { Success = true, Status = match.Status.ToString() };

        // BR-20：胜负由 WinnerId 判定（NULL=平局）
        var winner = match.WinnerId;
        var winReason = match.FinishReason == PkFinishReason.Score && players.Count == 2
            ? DetermineScoreWinReason(players)
            : match.FinishReason == PkFinishReason.Forfeit
                ? "对手离线弃权"
                : match.FinishReason == PkFinishReason.Timeout
                    ? "对局超时"
                    : null;

        // BR-18：AI 点评失败留空 → 兜底文案（切片：未接 LLM，直接兜底）
        var playerItems = players.Select(p => new PkPlayerResultDto
        {
            UserId = p.UserId,
            Nickname = string.Empty, // 账户域（跨模块），切片为空串
            Score = p.Score,
            TotalTimeMs = p.TotalTimeMs,
            AiComment = string.IsNullOrWhiteSpace(p.AiComment) ? AiCommentFallback : p.AiComment,
        }).ToList();

        // BR-19：合规——仅展示 ★数/用时，无正确率对比榜
        return new GetPkResultResDto
        {
            Success = true,
            Status = match.Status.ToString(),
            WinnerId = winner,
            FinishReason = match.FinishReason?.ToString() ?? string.Empty,
            WinReason = winReason,
            Players = playerItems,
            KnowledgeEggs = [], // 知识彩蛋响应态展示（切片不落库，返回空）
        };
    }

    private static string? DetermineScoreWinReason(List<PkPlayers> players)
    {
        if (players.Count < 2)
            return null;
        var sorted = players.OrderByDescending(p => p.Score).ThenBy(p => p.TotalTimeMs).ToList();
        return sorted[0].Score == sorted[1].Score ? "同分平局" : "同分比用时获胜";
    }
}

/// <summary>PK 结果请求 DTO</summary>
public sealed record GetPkResultReqDto
{
    /// <summary>对局外部键</summary>
    public string MatchUid { get; init; } = string.Empty;
}

/// <summary>PK 结果响应 DTO</summary>
public sealed record GetPkResultResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>对局状态（Finished 等）</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>胜方用户 Id（NULL=平局）</summary>
    public long? WinnerId { get; init; }

    /// <summary>结束原因（Score/Forfeit/Timeout）</summary>
    public string FinishReason { get; init; } = string.Empty;

    /// <summary>胜负说明</summary>
    public string? WinReason { get; init; }

    /// <summary>双方结果（合规：无正确率对比榜）</summary>
    public List<PkPlayerResultDto> Players { get; init; } = [];

    /// <summary>知识彩蛋（响应态展示，不落库）</summary>
    public List<object> KnowledgeEggs { get; init; } = [];
}

/// <summary>PK 玩家结果 DTO</summary>
public sealed record PkPlayerResultDto
{
    /// <summary>用户 Id</summary>
    public long UserId { get; init; }

    /// <summary>昵称（账户域，切片为空）</summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>得分</summary>
    public int Score { get; init; }

    /// <summary>总用时（毫秒）</summary>
    public int TotalTimeMs { get; init; }

    /// <summary>AI 趣味点评（失败兜底文案）</summary>
    public string AiComment { get; init; } = string.Empty;
}