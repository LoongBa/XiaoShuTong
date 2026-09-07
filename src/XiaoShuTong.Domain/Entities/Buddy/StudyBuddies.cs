using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Buddy;

/// <summary>
/// 学习搭子（双向同意制，同一对用户仅一条关系记录）
/// </summary>
/// <remarks>
/// 软删除=否（removed/expired 状态保留历史）。
/// ≤5 校验为应用层事务 FOR UPDATE（数量约束无法用 UNIQUE 表达）。
/// </remarks>
[Table(Name = nameof(StudyBuddies), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_studybuddies_uid", nameof(UId), IsUnique = true)]
[Index("idx_studybuddies_inviter", nameof(InviterId), IsUnique = false)]
[Index("idx_studybuddies_invitee", nameof(InviteeId), IsUnique = false)]
[Index("idx_studybuddies_pair", "InviterId,InviteeId", IsUnique = true)]
public partial class StudyBuddies
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>邀请方</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long InviterId { get; set; }

    /// <summary>被邀请方</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long InviteeId { get; set; }

    /// <summary>状态（Pending/Accepted/Rejected/Removed/Expired）</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public BuddyStatus Status { get; set; } = BuddyStatus.Pending;

    /// <summary>邀请时间</summary>
    [Column(Position = 6)]
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

    /// <summary>同意时间</summary>
    [Column(Position = 7)]
    public DateTime? AcceptedAt { get; set; }

    /// <summary>邀请过期（InvitedAt + 7 天）</summary>
    [Column(Position = 8)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>创建时间（框架审计字段）</summary>
    [Column(Position = 9)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 10, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}