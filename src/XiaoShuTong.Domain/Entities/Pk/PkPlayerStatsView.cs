using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Pk;

/// <summary>
/// PK 战绩视图（每用户一行：Finished 对局聚合）
/// </summary>
/// <remarks>
/// 替代 GetPkStatsService 的 C# 内存聚合（PkPlayers→PkMatches 多对一 JOIN，无 fan-out）。
/// 口径与 GetPkStatsService 注释一致：仅 Finished 计入（Pk-BR-25/26）。
/// Id 构造：方案 A 业务唯一键（UserId 透传，稳定）。
/// 胜率 = Wins / TotalMatches 由 Service API 层计算（Pk-BR-26，视图不含除法）。
/// </remarks>
[DomainGenerateCode(IsView = true,
    ViewSql = @"CREATE OR REPLACE VIEW ""vw_pk_player_stats"" AS
SELECT p.""UserId"" AS ""Id"",
       p.""UserId"" AS ""UserId"",
       COUNT(p.""Id"") AS ""TotalMatches"",
       COUNT(*) FILTER (WHERE m.""WinnerId"" = p.""UserId"") AS ""Wins"",
       COUNT(*) FILTER (WHERE m.""WinnerId"" IS NULL) AS ""Draws"",
       SUM(p.""Score"") AS ""TotalScore""
FROM ""PkPlayers"" p
INNER JOIN ""PkMatches"" m ON m.""Id"" = p.""MatchId""
WHERE m.""Status"" = 'Finished'
GROUP BY p.""UserId""")]
[Table(Name = "vw_pk_player_stats", DisableSyncStructure = true)]
public partial class PkPlayerStatsView
{
    /// <summary>业务唯一键（UserId 透传，稳定）</summary>
    [Column(IsPrimary = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>用户 Id（分组键 + RLS 过滤）</summary>
    [Column(Position = 2)]
    public long UserId { get; set; }

    /// <summary>总场次（仅 Finished 计入）</summary>
    [Column(Position = 3)]
    public long TotalMatches { get; set; }

    /// <summary>胜场</summary>
    [Column(Position = 4)]
    public long Wins { get; set; }

    /// <summary>平局场次</summary>
    [Column(Position = 5)]
    public long Draws { get; set; }

    /// <summary>累计分（Finished 对局 Score 之和）</summary>
    [Column(Position = 6)]
    public long TotalScore { get; set; }
}
