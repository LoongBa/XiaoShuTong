using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Learning;

/// <summary>
/// 每日统计（热力图/连续天数数据源，一人一日一行）
/// </summary>
/// <remarks>
/// 唯一约束：UserId+StatDate；软删除=否。连续天数由 StatDate 连续性 SQL 派生（不建 UserStreaks 表）。
/// </remarks>
[Table(Name = nameof(DailyStats), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_dailystats_uid", nameof(UId), IsUnique = true)]
[Index("idx_dailystats_userid_statdate", "UserId,StatDate", IsUnique = true)]
public partial class DailyStats
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>学生用户</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>UTC+8 业务日期</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public DateOnly StatDate { get; set; }

    /// <summary>学习题数</summary>
    [Column(Position = 5)]
    public int LearnedCount { get; set; }

    /// <summary>点亮★数（热力图）</summary>
    [Column(Position = 6)]
    public int StarredCount { get; set; }

    /// <summary>复习题数</summary>
    [Column(Position = 7)]
    public int ReviewCount { get; set; }

    /// <summary>当日正确率</summary>
    [Column(Position = 8)]
    public double? Accuracy { get; set; }

    /// <summary>学习时长（秒）</summary>
    [Column(Position = 9)]
    public int StudySeconds { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 10)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 11, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
