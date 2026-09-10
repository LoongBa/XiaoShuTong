using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;
using XiaoShuTong.Entities.Pk;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.6：PK 战绩
/// </summary>
/// <remarks>
/// BR-24 无记录零值 | BR-25 仅当前用户（RLS）| BR-26 胜率 API 层计算 | BR-27 无正确率对比榜（合规）
/// 数据源：PkPlayerStatsView（vw_pk_player_stats，仅 Finished 计入）——替代切片验证期 Service 层内存聚合。
/// 注入 IEntityReadOnlyDAC&lt;PkPlayerStatsView&gt;（VEntity 只读通道；测试可注入同 scope 实例）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetPkStatsService(DomainUser<XiaoShuTongUserInfo> user,
    IEntityReadOnlyDAC<PkPlayerStatsView> playerStatsDac)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private readonly IEntityReadOnlyDAC<PkPlayerStatsView> _playerStatsDac = playerStatsDac;

    /// <summary>
    /// 当前用户 PK 战绩（场次/胜/平/胜率/累计分）
    /// </summary>
    public Task<GetPkStatsResDto> ExecuteAsync(GetPkStatsReqDto request, CancellationToken ct = default)
    {
        // BR-25：仅当前用户（RLS；UserUid 参数账户域未实施，默认本人）
        var userId = User.UserInfo?.Id ?? 0;

        // 查询视图（vw_pk_player_stats 已按 UserId 聚合，仅 Finished 计入）
        var row = _playerStatsDac.Query.FirstOrDefault(x => x.UserId == userId);

        // BR-24：无记录 → 零值；BR-26：胜率 API 层计算（视图不含除法）
        if (row == null)
            return Task.FromResult(new GetPkStatsResDto { Success = true });

        var winRate = row.TotalMatches == 0 ? 0d : Math.Round((double)row.Wins / row.TotalMatches, 2);

        return Task.FromResult(new GetPkStatsResDto
        {
            Success = true,
            TotalMatches = (int)row.TotalMatches,
            Wins = (int)row.Wins,
            Draws = (int)row.Draws,
            WinRate = winRate,
            TotalScore = (int)row.TotalScore,
        });
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
