using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.TaskManagement;

/// <summary>
/// 任务主表（群主布置的背书任务）
/// </summary>
/// <remarks>
/// 群主私域数据，DataService 查询默认带 OwnerId 过滤（Service 层实施）。软删除=否（关闭走 Status=Closed）。
/// QuestionIds 为任务出题范围（jsonb，学习域取题跨模块读取）。
/// </remarks>
[Table(Name = nameof(Tasks), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_tasks_uid", nameof(UId), IsUnique = true)]
[Index("idx_tasks_ownerid_status", "OwnerId,Status", IsUnique = false)]
[Index("idx_tasks_groupid_status", "GroupId,Status", IsUnique = false)]
[Index("idx_tasks_deadlineat", nameof(DeadlineAt), IsUnique = false)]
// 注：QuestionIds(string[] JSONB) 不标 [Index]——xCodeGen 的 Conditions 生成器不支持数组列索引
public partial class Tasks
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>群主（发布者）</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long OwnerId { get; set; }

    /// <summary>目标群组</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long GroupId { get; set; }

    /// <summary>关联题库（官方/PGC/自定义，业务键）</summary>
    [Column(Position = 5, StringLength = 64)]
    public string? BankId { get; set; }

    /// <summary>任务标题（≤128）</summary>
    [Column(Position = 6, StringLength = 128)]
    [DtoField(IsSearchable = true)]
    public string Title { get; set; } = string.Empty;

    /// <summary>任务说明</summary>
    [Column(Position = 7, StringLength = 512)]
    public string? Description { get; set; }

    /// <summary>题目 ID 列表（jsonb，任务题集）</summary>
    [Column(Position = 8, DbType = "jsonb")]
    public string[] QuestionIds { get; set; } = [];

    /// <summary>题目数量</summary>
    [Column(Position = 9)]
    public int QuestionCount { get; set; }

    /// <summary>场景（Memorize/Assess）</summary>
    [Column(Position = 10, MapType = typeof(string), StringLength = 20)]
    public TaskScenario Scenario { get; set; } = TaskScenario.Memorize;

    /// <summary>会话类型（Progressive/Free/Assembled）</summary>
    [Column(Position = 11, MapType = typeof(string), StringLength = 20)]
    public TaskSessionType SessionType { get; set; } = TaskSessionType.Progressive;

    /// <summary>是否允许重做（重做不改变 Completed 状态）</summary>
    [Column(Position = 12)]
    public bool AllowRedo { get; set; }

    /// <summary>任务开始时间</summary>
    [Column(Position = 13)]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>截止时间（NULL=无截止）</summary>
    [Column(Position = 14)]
    public DateTime? DeadlineAt { get; set; }

    /// <summary>状态（Draft/Active/Closed）</summary>
    [Column(Position = 15, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public TaskStatus Status { get; set; } = TaskStatus.Active;

    /// <summary>创建时间</summary>
    [Column(Position = 16)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    [Column(Position = 17, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}