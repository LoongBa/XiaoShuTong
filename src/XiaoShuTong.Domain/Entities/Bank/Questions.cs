using FreeSql.DataAnnotations;
using System.Text.Json.Serialization;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Bank;

/// <summary>
/// 题目索引表（API 查询索引）
/// </summary>
/// <remarks>
/// 内容权威：答案仅在 JSON 文件 + 判题服务端，永不下发客户端（防爬 DRM）。
/// Content 为题目展示内容镜像（不含答案）；Keywords 为 KeywordGroup[]（判题关键词，服务端校验用）。
/// 软删除=否（改版走 SupersededBy 指向新题；下线走 Status=Hidden）。
/// </remarks>
[Table(Name = nameof(Questions), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_questions_uid", nameof(UId), IsUnique = true)]
[Index("idx_questions_questionid", nameof(QuestionId), IsUnique = true)]
[Index("idx_questions_bankid", nameof(BankId), IsUnique = false)]
[Index("idx_questions_bankid_chapter", "BankId,ChapterId", IsUnique = false)]
[Index("idx_questions_qtype", nameof(QType), IsUnique = false)]
// 注：KnowledgePoints(string[] JSONB) 不标 [Index]——xCodeGen 的 Conditions 生成器不支持数组列索引（GIN 索引由适配层配置）
public partial class Questions
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>业务键（Q-ch-7a-0001）</summary>
    [Column(Position = 3, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>题库业务键（外键引用 Banks.BankId）</summary>
    [Column(Position = 4, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string BankId { get; set; } = string.Empty;

    /// <summary>章节/单元（如 7a）</summary>
    [Column(Position = 5, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string? ChapterId { get; set; }

    /// <summary>题型键（R1/R2/O1...）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 10)]
    [DtoField(IsSearchable = true)]
    public QuestionType QType { get; set; }

    /// <summary>题目展示内容镜像（JSON，不含答案，防爬）</summary>
    [Column(Position = 7, DbType = "jsonb")]
    public string Content { get; set; } = "{}";

    /// <summary>KeywordGroup[] 判题关键词（JSON，服务端校验用，不下发）</summary>
    [Column(Position = 8, DbType = "jsonb")]
    [DtoFieldIgnore]
    [JsonIgnore]
    public string Keywords { get; set; } = "[]";

    /// <summary>知识点列表（JSONB GIN 索引）</summary>
    [Column(Position = 9, DbType = "jsonb")]
    public string[] KnowledgePoints { get; set; } = [];

    /// <summary>难度 0-5</summary>
    [Column(Position = 10)]
    public int Difficulty { get; set; }

    /// <summary>状态（Active/Superseded/Hidden）</summary>
    [Column(Position = 11, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public QuestionStatus Status { get; set; } = QuestionStatus.Active;

    /// <summary>题目改版替换（软删除）</summary>
    [Column(Position = 12, StringLength = 64)]
    public string? SupersededBy { get; set; }

    /// <summary>创建时间</summary>
    [Column(Position = 13)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    [Column(Position = 14, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
