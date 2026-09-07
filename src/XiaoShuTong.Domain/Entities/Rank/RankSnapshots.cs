using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Rank;

/// <summary>
/// 排名快照（排行榜趋势数据源，一人一范围一学科一指标一日一行）
/// </summary>
/// <remarks>
/// 软删除=否（快照为冻结历史）；不实时写（Hangfire 每日 0:00 UTC+8 批量冻结）。
/// 唯一约束：UserId+ScopeType+ScopeId+Subject+MetricType+SnapshotDate。
/// </remarks>
[Table(Name = nameof(RankSnapshots), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_ranksnapshots_uid", nameof(UId), IsUnique = true)]
[Index("idx_ranksnapshots_query", "UserId,ScopeType,ScopeId,Subject,MetricType,SnapshotDate", IsUnique = true)]
[Index("idx_ranksnapshots_date", nameof(SnapshotDate), IsUnique = false)]
public partial class RankSnapshots
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>用户</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>范围类型（Group/Grade）</summary>
    [Column(Position = 4, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public RankScopeType ScopeType { get; set; }

    /// <summary>group_id（群组）或 grade_key（年级）</summary>
    [Column(Position = 5, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string? ScopeId { get; set; }

    /// <summary>学科筛选维度（All/语文/…/政治）</summary>
    [Column(Position = 6, StringLength = 20)]
    public string Subject { get; set; } = "All";

    /// <summary>指标类型（Streak/Volume/PkWins/Accuracy/Mastery/Stars）</summary>
    [Column(Position = 7, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public RankMetricType MetricType { get; set; }

    /// <summary>冻结的指标原始值</summary>
    [Column(Position = 8, DbType = "numeric(18,4)")]
    public decimal MetricValue { get; set; }

    /// <summary>冻结的排名（不回溯）</summary>
    [Column(Position = 9)]
    public int Rank { get; set; }

    /// <summary>快照日期</summary>
    [Column(Position = 10)]
    [DtoField(IsSearchable = true)]
    public DateOnly SnapshotDate { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 11)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 12, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}