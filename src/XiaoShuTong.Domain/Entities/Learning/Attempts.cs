using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Learning;

/// <summary>
/// 作答记录（唯一事实源）
/// </summary>
/// <remarks>
/// 本域唯一事实源写入入口，MemoryStates/DailyStats/WrongQuestions 均由其派生。
/// 软删除=否（作答历史不删不改）；写入纪律见 DS01。
/// </remarks>
[Table(Name = nameof(Attempts), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_attempts_uid", nameof(UId), IsUnique = true)]
[Index("idx_attempts_userid_answeredat", "UserId,AnsweredAt", IsUnique = false)]
[Index("idx_attempts_sessionid", nameof(SessionId), IsUnique = false)]
[Index("idx_attempts_userid_questionid", "UserId,QuestionId", IsUnique = false)]
[Index("idx_attempts_userid_result", "UserId,Result", IsUnique = false)]
public partial class Attempts
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

    /// <summary>学习会话归属</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long? SessionId { get; set; }

    /// <summary>题目业务键（Q-ch-7a-0001）</summary>
    [Column(Position = 5, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>题库业务键</summary>
    [Column(Position = 6, StringLength = 64)]
    public string BankId { get; set; } = string.Empty;

    /// <summary>场景（LearningScenario，由会话派生）</summary>
    [Column(Position = 7, MapType = typeof(string), StringLength = 20)]
    public LearningScenario Scenario { get; set; } = LearningScenario.Memorize;

    /// <summary>题型键（R1/R2/R3a/R3b/O1...）</summary>
    [Column(Position = 8, StringLength = 20)]
    public string QType { get; set; } = string.Empty;

    /// <summary>作答前记忆状态（MemoryState）</summary>
    [Column(Position = 9, MapType = typeof(string), StringLength = 20)]
    public MemoryState PreState { get; set; } = MemoryState.NotMastered;

    /// <summary>作答后记忆状态（MemoryState）</summary>
    [Column(Position = 10, MapType = typeof(string), StringLength = 20)]
    public MemoryState PostState { get; set; } = MemoryState.NotMastered;

    /// <summary>判题结果（Correct/Partial/Wrong）</summary>
    [Column(Position = 11, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public JudgmentResult Result { get; set; } = JudgmentResult.Wrong;

    /// <summary>判题置信度（0-1）</summary>
    [Column(Position = 12)]
    public double? Confidence { get; set; }

    /// <summary>求助档位（None/Partial/Full）</summary>
    [Column(Position = 13, MapType = typeof(string), StringLength = 20)]
    public HintLevel HintLevel { get; set; } = HintLevel.None;

    /// <summary>作答耗时（服务端计时，毫秒）</summary>
    [Column(Position = 14)]
    public int? TimeCostMs { get; set; }

    /// <summary>作答时间</summary>
    [Column(Position = 15)]
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 16)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 17, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
