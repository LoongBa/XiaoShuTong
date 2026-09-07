using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Pk;

/// <summary>
/// 比赛主表（PK 竞技对局）
/// </summary>
/// <remarks>
/// 软删除=否（历史对局保留）。
/// 决策：forfeit/timeout 时 Status 置 Finished（对齐 PkPlayerStats 视图过滤口径）。
/// </remarks>
[Table(Name = nameof(PkMatches), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_pkmatches_uid", nameof(UId), IsUnique = true)]
[Index("idx_pkmatches_status_createdat", "Status,CreateTime", IsUnique = false)]
[Index("idx_pkmatches_winnerid", nameof(WinnerId), IsUnique = false)]
[Index("idx_pkmatches_invitecode", nameof(InviteCode), IsUnique = false)]
public partial class PkMatches
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>学科</summary>
    [Column(Position = 3, MapType = typeof(string), StringLength = 20)]
    public Subject Subject { get; set; } = Subject.Chinese;

    /// <summary>题库业务键</summary>
    [Column(Position = 4, StringLength = 64)]
    public string BankId { get; set; } = string.Empty;

    /// <summary>知识点/主题（按知识点自动出题）</summary>
    [Column(Position = 5, StringLength = 128)]
    public string? Topic { get; set; }

    /// <summary>题量（5/10/20）</summary>
    [Column(Position = 6)]
    public int QuestionCount { get; set; } = 10;

    /// <summary>每题限时（秒）</summary>
    [Column(Position = 7)]
    public int PerQuestionTimeS { get; set; } = 30;

    /// <summary>总时长上限（秒）</summary>
    [Column(Position = 8)]
    public int TotalTimeLimitS { get; set; } = 300;

    /// <summary>模式（Sync/Async）</summary>
    [Column(Position = 9, MapType = typeof(string), StringLength = 10)]
    public PkMode Mode { get; set; } = PkMode.Sync;

    /// <summary>状态（Pending/Ongoing/Finished/Cancelled）</summary>
    [Column(Position = 10, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public PkMatchStatus Status { get; set; } = PkMatchStatus.Pending;

    /// <summary>胜方（NULL=平局）</summary>
    [Column(Position = 11)]
    [DtoField(IsSearchable = true)]
    public long? WinnerId { get; set; }

    /// <summary>4 位对战码（方式 B 加入）</summary>
    [Column(Position = 12, StringLength = 4)]
    [DtoField(IsSearchable = true)]
    public string? InviteCode { get; set; }

    /// <summary>结束原因（Score/Forfeit/Timeout）</summary>
    [Column(Position = 13, MapType = typeof(string), StringLength = 20)]
    public PkFinishReason? FinishReason { get; set; }

    /// <summary>创建时间（D02 原 CreatedAt）</summary>
    [Column(Position = 14)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>结束时间</summary>
    [Column(Position = 15)]
    public DateTime? FinishedAt { get; set; }

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 16, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}