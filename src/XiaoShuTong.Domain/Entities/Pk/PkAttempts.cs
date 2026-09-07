using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Pk;

/// <summary>
/// PK 答题明细（防重复提交：MatchId+PlayerId+QuestionId 唯一）
/// </summary>
/// <remarks>
/// 唯一约束：MatchId+PlayerId+QuestionId（防同题重复提交，本切片补充）；软删除=否。
/// </remarks>
[Table(Name = nameof(PkAttempts), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_pkattempts_uid", nameof(UId), IsUnique = true)]
[Index("idx_pkattempts_matchid", nameof(MatchId), IsUnique = false)]
[Index("idx_pkattempts_dedup", "MatchId,PlayerId,QuestionId", IsUnique = true)]
public partial class PkAttempts
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

    /// <summary>参赛者成绩记录</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long PlayerId { get; set; }

    /// <summary>答题用户</summary>
    [Column(Position = 5)]
    public long UserId { get; set; }

    /// <summary>题目业务键</summary>
    [Column(Position = 6, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>用户答案</summary>
    [Column(Position = 7, StringLength = 512)]
    public string? Answer { get; set; }

    /// <summary>判题结果（Correct/Partial/Wrong）</summary>
    [Column(Position = 8, MapType = typeof(string), StringLength = 20)]
    public PkAttemptResult Result { get; set; }

    /// <summary>答对标记（Correct=true；Partial/Wrong=false）</summary>
    [Column(Position = 9)]
    public bool IsCorrect { get; set; }

    /// <summary>答题用时（毫秒，客户端上送）</summary>
    [Column(Position = 10)]
    public int TimeCostMs { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 11)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 12, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}