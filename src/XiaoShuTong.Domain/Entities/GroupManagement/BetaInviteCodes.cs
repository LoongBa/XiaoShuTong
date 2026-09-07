using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.GroupManagement;

/// <summary>
/// 内测邀请码（平台发放给群主）
/// </summary>
/// <remarks>
/// 平台后台批量生成发放（用户端无生成入口）；一个内测码只能激活一个群组。
/// 唯一约束：码全局唯一。
/// </remarks>
[Table(Name = nameof(BetaInviteCodes), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_betainvitecodes_uid", nameof(UId), IsUnique = true)]
[Index("idx_betainvitecodes_code", nameof(Code), IsUnique = true)]
[Index("idx_betainvitecodes_status", nameof(Status), IsUnique = false)]
public partial class BetaInviteCodes
{
    /// <summary>主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>内测邀请码（平台生成，16 位）</summary>
    [Column(Position = 3, StringLength = 16)]
    [DtoField(IsSearchable = true)]
    public string Code { get; set; } = string.Empty;

    /// <summary>状态（Pending/Used/Expired）</summary>
    [Column(Position = 4, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public BetaCodeStatus Status { get; set; } = BetaCodeStatus.Pending;

    /// <summary>发放对象（群主，可 NULL=未指定）</summary>
    [Column(Position = 5)]
    public long? IssuedToUserId { get; set; }

    /// <summary>发放时间</summary>
    [Column(Position = 6)]
    public DateTime? IssuedAt { get; set; }

    /// <summary>内测期码有效期</summary>
    [Column(Position = 7)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>群主激活建群时间</summary>
    [Column(Position = 8)]
    public DateTime? UsedAt { get; set; }

    /// <summary>激活的群组</summary>
    [Column(Position = 9)]
    public long? UsedByGroupId { get; set; }
}