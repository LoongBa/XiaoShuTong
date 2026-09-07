using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Learning;

/// <summary>
/// 记忆状态（状态机运行表，一人一题一行）
/// </summary>
/// <remarks>
/// 唯一约束：UserId+QuestionId；软删除=否。
/// 复习队列：由 GetReviewQueueService 按 NextReviewAt &lt;= now + State &lt;&gt; Proficient 筛选（DS01 ⑥）。
/// </remarks>
[Table(Name = nameof(MemoryStates), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_memorystates_uid", nameof(UId), IsUnique = true)]
[Index("idx_memorystates_userid_nextreviewat", "UserId,NextReviewAt", IsUnique = false)]
[Index("idx_memorystates_userid_state", "UserId,State", IsUnique = false)]
[Index("idx_memorystates_userid_questionid", "UserId,QuestionId", IsUnique = true)]
public partial class MemoryStates
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

    /// <summary>题目业务键</summary>
    [Column(Position = 4, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>题库业务键</summary>
    [Column(Position = 5, StringLength = 64)]
    public string BankId { get; set; } = string.Empty;

    /// <summary>记忆状态（NotMastered/Fuzzy/Mastered/Proficient）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public MemoryState State { get; set; } = MemoryState.NotMastered;

    /// <summary>连续独立答对（2 次→★）</summary>
    [Column(Position = 7)]
    public int ConsecutiveCorrect { get; set; }

    /// <summary>近 20 次正确率（间隔系数 0.5~1.5）</summary>
    [Column(Position = 8)]
    public double HistoryAccuracy { get; set; }

    /// <summary>预留（Phase2 SM-2）</summary>
    [Column(Position = 9)]
    public double EaseFactor { get; set; } = 2.5;

    /// <summary>下次复习（驱动队列）</summary>
    [Column(Position = 10)]
    public DateTime NextReviewAt { get; set; } = DateTime.UtcNow;

    /// <summary>最近一次作答</summary>
    [Column(Position = 11)]
    public long? LastAttemptId { get; set; }

    /// <summary>最近求助档位（None/Partial/Full）</summary>
    [Column(Position = 12, MapType = typeof(string), StringLength = 20)]
    public HintLevel LastHintLevel { get; set; } = HintLevel.None;

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 13)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 14, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
