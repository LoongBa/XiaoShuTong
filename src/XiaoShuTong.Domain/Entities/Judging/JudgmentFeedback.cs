using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Judging;

/// <summary>
/// 判错纠错队列（判题质量反馈）
/// </summary>
/// <remarks>
/// 同一 (AttemptId, UserId, FeedbackType) 仅一条有效反馈（应用层幂等校验）。软删除=否。
/// 数据权限：学生仅本人可见；复核人视角平台后台。
/// </remarks>
[Table(Name = nameof(JudgmentFeedback), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_judgmentfeedback_uid", nameof(UId), IsUnique = true)]
[Index("idx_judgmentfeedback_attemptid", nameof(AttemptId), IsUnique = false)]
[Index("idx_judgmentfeedback_status", nameof(Status), IsUnique = false)]
public partial class JudgmentFeedback
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>关联作答记录（跨模块，学习域）</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long AttemptId { get; set; }

    /// <summary>反馈用户</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>反馈类型（WrongJudgment/Other）</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 20)]
    public FeedbackType FeedbackType { get; set; } = FeedbackType.WrongJudgment;

    /// <summary>复核状态（Pending/Reviewed/Resolved）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;

    /// <summary>复核人（人工队列）</summary>
    [Column(Position = 7, StringLength = 64)]
    public string? ReviewedBy { get; set; }

    /// <summary>复核时间</summary>
    [Column(Position = 8)]
    public DateTime? ReviewedAt { get; set; }

    /// <summary>创建时间</summary>
    [Column(Position = 9)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    [Column(Position = 10, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
