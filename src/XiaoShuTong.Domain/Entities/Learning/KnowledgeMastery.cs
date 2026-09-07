using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Learning;

/// <summary>
/// 知识点掌握度（聚合视图落库，一人一知识点一行）
/// </summary>
/// <remarks>
/// 唯一约束：UserId+Subject+KnowledgePoint；软删除=否。
/// 写入时机：Hangfire 每小时批量 / 家长报告按需触发（BackgroundJob）。
/// </remarks>
[Table(Name = nameof(KnowledgeMastery), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_knowledgemastery_uid", nameof(UId), IsUnique = true)]
[Index("idx_knowledgemastery_userid_subject", "UserId,Subject", IsUnique = false)]
[Index("idx_knowledgemastery_userid_subject_point", "UserId,Subject,KnowledgePoint", IsUnique = true)]
public partial class KnowledgeMastery
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

    /// <summary>学科</summary>
    [Column(Position = 4, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>知识点</summary>
    [Column(Position = 5, StringLength = 128)]
    [DtoField(IsSearchable = true)]
    public string KnowledgePoint { get; set; } = string.Empty;

    /// <summary>聚合状态（取题目中位/最差，MemoryState）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 20)]
    public MemoryState State { get; set; } = MemoryState.NotMastered;

    /// <summary>聚合正确率</summary>
    [Column(Position = 7)]
    public double Accuracy { get; set; }

    /// <summary>作答次数</summary>
    [Column(Position = 8)]
    public int AttemptCount { get; set; }

    /// <summary>最近复习时间</summary>
    [Column(Position = 9)]
    public DateTime? LastReviewedAt { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 10)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 11, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
