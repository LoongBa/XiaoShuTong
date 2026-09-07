using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Pk;

/// <summary>
/// 参赛者成绩（每对局每参赛者唯一）
/// </summary>
/// <remarks>
/// 唯一约束：MatchId+UserId；软删除=否。得分规则：每题答对 +10（Partial 0 分，本切片决策）。
/// </remarks>
[Table(Name = nameof(PkPlayers), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_pkplayers_uid", nameof(UId), IsUnique = true)]
[Index("idx_pkplayers_userid_matchid", "UserId,MatchId", IsUnique = false)]
[Index("idx_pkplayers_matchid_userid", "MatchId,UserId", IsUnique = true)]
public partial class PkPlayers
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>对局归属</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long MatchId { get; set; }

    /// <summary>参赛者</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>总分（每题 +10）</summary>
    [Column(Position = 5)]
    public int Score { get; set; }

    /// <summary>答对数</summary>
    [Column(Position = 6)]
    public int CorrectCount { get; set; }

    /// <summary>总用时（毫秒，同分比用时）</summary>
    [Column(Position = 7)]
    public int TotalTimeMs { get; set; }

    /// <summary>AI 趣味点评（失败留空 + 兜底文案）</summary>
    [Column(Position = 8, StringLength = 512)]
    public string? AiComment { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 9)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 10, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}