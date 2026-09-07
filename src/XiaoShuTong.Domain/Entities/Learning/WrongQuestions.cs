using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Learning;

/// <summary>
/// 错题本（物化派生表，一人一题一行）
/// </summary>
/// <remarks>
/// 唯一约束：UserId+QuestionId；软删除=否（已掌握保留记录供复习回看）。
/// 派生来源：Attempts（Result=Wrong/Partial）归集；Mastered 连续 2 次 Correct 后置 true。
/// </remarks>
[Table(Name = nameof(WrongQuestions), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_wrongquestions_uid", nameof(UId), IsUnique = true)]
[Index("idx_wrongquestions_userid_mastered_lastwrongat", "UserId,Mastered,LastWrongAt", IsUnique = false)]
[Index("idx_wrongquestions_userid_subject", "UserId,Subject", IsUnique = false)]
[Index("idx_wrongquestions_userid_questionid", "UserId,QuestionId", IsUnique = true)]
public partial class WrongQuestions
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

    /// <summary>冗余学科字段（跨学科查询免 join）</summary>
    [Column(Position = 6, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>错误次数</summary>
    [Column(Position = 7)]
    public int WrongCount { get; set; } = 1;

    /// <summary>最近错误时间</summary>
    [Column(Position = 8)]
    public DateTime LastWrongAt { get; set; } = DateTime.UtcNow;

    /// <summary>连续 2 次答对 → true</summary>
    [Column(Position = 9)]
    public bool Mastered { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 10)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 11, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
