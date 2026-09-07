using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.GroupManagement;

/// <summary>
/// 群组（群主管理）
/// </summary>
/// <remarks>
/// 群主私域数据，DataService 查询默认带 OwnerId 过滤（Service 层实施）。
/// 群组停用走 Status=Archived，软删除=否。
/// </remarks>
[Table(Name = nameof(Groups), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_groups_uid", nameof(UId), IsUnique = true)]
[Index("idx_groups_ownerid_status", "OwnerId,Status", IsUnique = false)]
[Index("idx_groups_betacodeid", nameof(BetaCodeId), IsUnique = false)]
public partial class Groups
{
    /// <summary>主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>群主（原老师/班主任）</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long OwnerId { get; set; }

    /// <summary>群组名称（≤64，如"七(3)班"）</summary>
    [Column(Position = 4, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>学科（history/geography/biology/daodeyufazhi）</summary>
    [Column(Position = 5, StringLength = 32)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>年级（七年级/八年级/九年级）</summary>
    [Column(Position = 6, StringLength = 16)]
    public string? Grade { get; set; }

    /// <summary>状态（Active/Archived）</summary>
    [Column(Position = 7, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public GroupStatus Status { get; set; } = GroupStatus.Active;

    /// <summary>群组战绩榜开关（false → 战绩榜入口隐藏）</summary>
    [Column(Position = 8)]
    public bool RankEnabled { get; set; } = true;

    /// <summary>激活本群所用的内测邀请码</summary>
    [Column(Position = 9)]
    [DtoField(IsSearchable = true)]
    public long? BetaCodeId { get; set; }

    /// <summary>创建时间</summary>
    [Column(Position = 10)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    [Column(Position = 11, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
