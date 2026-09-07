using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Parent;

/// <summary>
/// 家长订阅（付费报告，每 (家长,孩子) 对一条）
/// </summary>
/// <remarks>
/// 软删除=否（状态流转变更）；授权链校验（StudentId 须在 ParentStudentRelations 有记录）为应用层校验。
/// 报告访问时惰性判定到期（TrialEndAt/PeriodEndAt 过期 → 视为无权益）。
/// </remarks>
[Table(Name = nameof(Subscriptions), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_subscriptions_uid", nameof(UId), IsUnique = true)]
[Index("idx_subscriptions_parentid_status", "ParentId,Status", IsUnique = false)]
[Index("idx_subscriptions_studentid", nameof(StudentId), IsUnique = false)]
[Index("idx_subscriptions_pair", "ParentId,StudentId", IsUnique = true)]
public partial class Subscriptions
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>家长</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long ParentId { get; set; }

    /// <summary>孩子（报告对象）</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long StudentId { get; set; }

    /// <summary>方案（Month/Year）</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 10)]
    public SubscriptionPlan Plan { get; set; }

    /// <summary>状态（Trialing/Active/Expired/Cancelled）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public SubscriptionStatus Status { get; set; }

    /// <summary>试用结束（试用期 7 天）</summary>
    [Column(Position = 7)]
    public DateTime? TrialEndAt { get; set; }

    /// <summary>当前计费周期结束</summary>
    [Column(Position = 8)]
    public DateTime? PeriodEndAt { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 9)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 10, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}