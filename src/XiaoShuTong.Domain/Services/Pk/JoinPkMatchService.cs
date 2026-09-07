using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.2：加入 PK（方式 B：对战码）
/// </summary>
/// <remarks>
/// BR-06 对局不存在 → 2001 | BR-07 对局已结束/不可加入 → 2002 | BR-08 人数已满 → 2003
/// BR-09 与发起者互为 accepted 搭子 → 2005 | BR-10 InviteCode 匹配校验
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class JoinPkMatchService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int MaxPlayers = 2;

    private PkMatchesDataService? _matchesDs;
    private PkMatchesDataService MatchesDs => _matchesDs ??= User.Use<PkMatchesDataService>();

    private PkPlayersDataService? _playersDs;
    private PkPlayersDataService PlayersDs => _playersDs ??= User.Use<PkPlayersDataService>();

    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    /// <summary>
    /// 凭对战码加入 PK（校验状态/人数/搭子资格 → 写入加入方参赛记录）
    /// </summary>
    public async Task<JoinPkMatchResDto> ExecuteAsync(JoinPkMatchReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-06/BR-10：对局存在 + 对战码匹配 → 2001
        var match = await MatchesDs.EntityGetAsync(x => x.UId == request.MatchUid, ct);
        if (match == null || match.InviteCode != request.InviteCode)
            return new JoinPkMatchResDto { Success = false, ErrorCode = PkErrorCodes.MatchNotFound };

        // BR-07：对局状态可加入（Pending）→ 2002
        if (match.Status != PkMatchStatus.Pending)
            return new JoinPkMatchResDto { Success = false, ErrorCode = PkErrorCodes.MatchClosed };

        // BR-08：人数已满（最多 2 人）→ 2003
        var playerCount = await PlayersDs.CountAsync(x => x.MatchId == match.Id, ct);
        if (playerCount >= MaxPlayers)
            return new JoinPkMatchResDto { Success = false, ErrorCode = PkErrorCodes.MatchFull };

        // BR-09：与发起者互为 accepted 搭子 → 2005
        var creator = await PlayersDs.EntityGetAsync(
            x => x.MatchId == match.Id, ct: ct);
        if (creator == null)
            return new JoinPkMatchResDto { Success = false, ErrorCode = PkErrorCodes.MatchNotFound };
        var buddy = await BuddiesDs.EntityGetAsync(
            x => (x.InviterId == creator.UserId && x.InviteeId == userId)
                 || (x.InviterId == userId && x.InviteeId == creator.UserId), ct);
        if (buddy == null || buddy.Status != BuddyStatus.Accepted)
            return new JoinPkMatchResDto { Success = false, ErrorCode = PkErrorCodes.NotBuddyPk };

        // 写入加入方参赛记录 + 对局置 Ongoing
        await PlayersDs.EntityCreateAsync(new PkPlayers
        {
            UId = UidGenerator.NewId(),
            MatchId = match.Id,
            UserId = userId,
            Score = 0,
            CorrectCount = 0,
            TotalTimeMs = 0,
        }, ct);

        match.Status = PkMatchStatus.Ongoing;
        await MatchesDs.EntityUpdateAsync(match, ct);

        return new JoinPkMatchResDto
        {
            Success = true,
            MatchUid = match.UId,
            Mode = match.Mode.ToString(),
            QuestionCount = match.QuestionCount,
            PerQuestionTimeS = match.PerQuestionTimeS,
        };
    }
}

/// <summary>加入 PK 请求 DTO</summary>
public sealed record JoinPkMatchReqDto
{
    /// <summary>对局外部键</summary>
    public string MatchUid { get; init; } = string.Empty;

    /// <summary>4 位对战码</summary>
    public string InviteCode { get; init; } = string.Empty;
}

/// <summary>加入 PK 响应 DTO</summary>
public sealed record JoinPkMatchResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>对局外部键</summary>
    public string MatchUid { get; init; } = string.Empty;

    /// <summary>模式（Sync/Async）</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>题量</summary>
    public int QuestionCount { get; init; }

    /// <summary>每题时长（秒）</summary>
    public int PerQuestionTimeS { get; init; }
}