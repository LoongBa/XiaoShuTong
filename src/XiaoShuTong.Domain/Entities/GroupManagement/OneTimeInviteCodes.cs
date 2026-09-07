using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.GroupManagement;

/// <summary>
/// 一次性邀请码（群主按名单生成给成员）
/// </summary>
/// <remarks>
/// 一次性（激活成功 Status=Used 不可复用）；过期 → Expired。
/// 唯一约束：码全局唯一；同一名单内码↔手机号后四位唯一（业务级）。
/// </remarks>
[Table(Name = nameof(OneTimeInviteCodes), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_onetimeinvitecodes_uid", nameof(UId), IsUnique = true)]
[Index("idx_onetimeinvitecodes_groupid", nameof(GroupId), IsUnique = false)]
[Index("idx_onetimeinvitecodes_code", nameof(Code), IsUnique = true)]
[Index("idx_onetimeinvitecodes_importid", nameof(RosterImportId), IsUnique = false)]
public partial class OneTimeInviteCodes
{
    /// <summary>主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>所属群组</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long GroupId { get; set; }

    /// <summary>一次性邀请码（8 位）</summary>
    [Column(Position = 4, StringLength = 8)]
    [DtoField(IsSearchable = true)]
    public string Code { get; set; } = string.Empty;

    /// <summary>名单手机号后四位（脱敏定向标识）</summary>
    [Column(Position = 5, StringLength = 4)]
    public string PhoneLast4 { get; set; } = string.Empty;

    /// <summary>状态（Unused/Used/Expired）</summary>
    [Column(Position = 6, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public OneTimeCodeStatus Status { get; set; } = OneTimeCodeStatus.Unused;

    /// <summary>群主（生成者）</summary>
    [Column(Position = 7)]
    public long GeneratedBy { get; set; }

    /// <summary>来源名单批次</summary>
    [Column(Position = 8)]
    [DtoField(IsSearchable = true)]
    public long? RosterImportId { get; set; }

    /// <summary>生成时间</summary>
    [Column(Position = 9)]
    [DtoField(CanModify = false)]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>有效期（可空：内测期默认 30 天）</summary>
    [Column(Position = 10)]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>激活时间</summary>
    [Column(Position = 11)]
    public DateTime? UsedAt { get; set; }

    /// <summary>激活绑定（成员微信 OpenId → UserId）</summary>
    [Column(Position = 12)]
    public long? BoundUserId { get; set; }
}