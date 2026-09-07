using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Pk;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.6：PK 战绩
/// </summary>
/// <remarks>
/// BR-24 无记录零值 | BR-25 仅当前用户（RLS）| BR-26 胜率 API 层计算 | BR-27 无正确率对比榜（合规）
/// PkPlayerStats 视图（VEntity 设计规格）：切片验证期 Service 层聚合实现（内存 DAC 不支持 SQL 视图），口径与视图一致（仅 Finished 计入）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetPkStatsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private PkMatchesDataService? _matchesDs;
    private PkMatchesDataService MatchesDs => _matchesDs ??= User.Use<PkMatchesDataService>();

    private PkPlayersDataService? _playersDs;
    private PkPlayersDataService PlayersDs => _playersDs ??= User.Use<PkPlayersDataService>();

    /// <summary>
    /// 当前用户 PK 战绩（场次/胜/平/胜率/累计分）
    /// </summary>
    public async Task<GetPkStatsResDto> ExecuteAsync(GetPkStatsReqDto request, CancellationToken ct = default)
    {
        // BR-25：仅当前用户（RLS；UserUid 参数账户域未实施，默认本人）
        var userId = User.UserInfo?.Id ?? 0;

        // 本人参与的所有对局（PkPlayers 反查 MatchId）
        var myPlayers = await PlayersDs.EntitySelectAsync(x => x.UserId == userId, ct: ct);
        if (myPlayers.Count == 0)
            return new GetPkStatsResDto { Success = true };

        var matchIds = myPlayers.Select(p => p.MatchId).ToArray();
        var finishedMatches = await MatchesDs.EntitySelectAsync(
            x => x.Status == PkMatchStatus.Finished && matchIds.Contains(x.Id), ct: ct);
        var finishedIds = finishedMatches.Select(m => m.Id).ToHashSet();

        // 战绩口径（与 vw_pk_player_stats 一致：仅 Finished 计入）
        var totalMatches = finishedMatches.Count;
        var wins = finishedMatches.Count(m => m.WinnerId == userId);
        var draws = finishedMatches.Count(m => m.WinnerId == null);

        // 累计分（Finished 对局中本人 PkPlayers.Score 之和）
        var totalScore = myPlayers
            .Where(p => finishedIds.Contains(p.MatchId))
            .Sum(p => p.Score);

        // BR-24：无记录 → 零值；BR-26：胜率 API 层计算
        var winRate = totalMatches == 0 ? 0d : Math.Round((double)wins / totalMatches, 2);

        return new GetPkStatsResDto
        {
            Success = true,
            TotalMatches = totalMatches,
            Wins = wins,
            Draws = draws,
            WinRate = winRate,
            TotalScore = totalScore,
        };
    }
}

/// <summary>PK 战绩请求 DTO</summary>
public sealed record GetPkStatsReqDto
{
    /// <summary>用户 Uid（账户域未实施，默认当前用户）</summary>
    public string? UserUid { get; init; }
}

/// <summary>PK 战绩响应 DTO（合规：无正确率对比榜）</summary>
public sealed record GetPkStatsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>总场数</summary>
    public int TotalMatches { get; init; }

    /// <summary>胜</summary>
    public int Wins { get; init; }

    /// <summary>平</summary>
    public int Draws { get; init; }

    /// <summary>胜率（API 层计算）</summary>
    public double WinRate { get; init; }

    /// <summary>累计分</summary>
    public int TotalScore { get; init; }
}