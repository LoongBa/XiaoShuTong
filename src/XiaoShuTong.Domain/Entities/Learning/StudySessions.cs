using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Learning;

/// <summary>
/// 学习会话（单次学习批次）
/// </summary>
/// <remarks>
/// 学生私域数据，DataService 查询默认带 UserId 过滤（Service 层实施）。软删除=否。
/// </remarks>
[Table(Name = nameof(StudySessions), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_studysessions_uid", nameof(UId), IsUnique = true)]
[Index("idx_studysessions_userid_startedat", "UserId,StartedAt", IsUnique = false)]
[Index("idx_studysessions_taskid", nameof(TaskId), IsUnique = false)]
public partial class StudySessions
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>学生用户</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>场景（Memorize/Assess/PlayPk/PlayDaily）</summary>
    [Column(Position = 4, MapType = typeof(string), StringLength = 20)]
    public LearningScenario Scenario { get; set; } = LearningScenario.Memorize;

    /// <summary>题库业务键（跨模块引用 Banks）</summary>
    [Column(Position = 5, StringLength = 64)]
    public string BankId { get; set; } = string.Empty;

    /// <summary>会话类型（Progressive/Free/Assembled/Level/Pk）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 20)]
    public SessionType SessionType { get; set; }

    /// <summary>计划题数</summary>
    [Column(Position = 7)]
    public int QuestionCount { get; set; }

    /// <summary>本次答对题数</summary>
    [Column(Position = 8)]
    public int CorrectCount { get; set; }

    /// <summary>累计用时（毫秒）</summary>
    [Column(Position = 9)]
    public int TotalTimeMs { get; set; }

    /// <summary>群组任务归属（个人背诵 NULL）</summary>
    [Column(Position = 10)]
    [DtoField(IsSearchable = true)]
    public long? TaskId { get; set; }

    /// <summary>会话开始时间</summary>
    [Column(Position = 11)]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>会话结束时间</summary>
    [Column(Position = 12)]
    public DateTime? EndedAt { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 13)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 14, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
