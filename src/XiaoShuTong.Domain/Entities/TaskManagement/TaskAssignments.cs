using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.TaskManagement;

/// <summary>
/// 任务分配与执行（每成员一条）
/// </summary>
/// <remarks>
/// 唯一约束：TaskId+UserId（每任务每成员唯一）；软删除=否。
/// </remarks>
[Table(Name = nameof(TaskAssignments), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_taskassignments_uid", nameof(UId), IsUnique = true)]
[Index("idx_taskassignments_taskid_status", "TaskId,Status", IsUnique = false)]
[Index("idx_taskassignments_userid_status", "UserId,Status", IsUnique = false)]
[Index("idx_taskassignments_taskid_userid", "TaskId,UserId", IsUnique = true)]
public partial class TaskAssignments
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>所属任务</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long TaskId { get; set; }

    /// <summary>学生</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>状态（Pending/InProgress/Completed/Overdue）</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;

    /// <summary>完成度 0-100（已完成题数/总题数）</summary>
    [Column(Position = 6)]
    public int Progress { get; set; }

    /// <summary>关联学习会话（执行任务时创建，跨模块）</summary>
    [Column(Position = 7)]
    public long? SessionId { get; set; }

    /// <summary>分配时间</summary>
    [Column(Position = 8)]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>首次开始时间</summary>
    [Column(Position = 9)]
    public DateTime? StartedAt { get; set; }

    /// <summary>全部完成时间</summary>
    [Column(Position = 10)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 11)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 12, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}