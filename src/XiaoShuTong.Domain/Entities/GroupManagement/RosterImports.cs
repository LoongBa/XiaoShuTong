using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.GroupManagement;

/// <summary>
/// 名单导入批次（Agent 整理记录）
/// </summary>
[Table(Name = nameof(RosterImports), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_rosterimports_uid", nameof(UId), IsUnique = true)]
[Index("idx_rosterimports_groupid", nameof(GroupId), IsUnique = false)]
public partial class RosterImports
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

    /// <summary>群主</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long OwnerId { get; set; }

    /// <summary>导入方式（Paste(批量粘贴) / File(文件导入)）</summary>
    [Column(Position = 5, StringLength = 16)]
    public string ImportMethod { get; set; } = string.Empty;

    /// <summary>原始行数</summary>
    [Column(Position = 6)]
    public int SourceCount { get; set; }

    /// <summary>Agent 整理后有效数（去重/格式校验）</summary>
    [Column(Position = 7)]
    public int CleanedCount { get; set; }

    /// <summary>去重剔除数</summary>
    [Column(Position = 8)]
    public int DuplicateCount { get; set; }

    /// <summary>非法手机号剔除数</summary>
    [Column(Position = 9)]
    public int InvalidCount { get; set; }

    /// <summary>状态（Processing/Ready/Exported/Failed）</summary>
    [Column(Position = 10, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public RosterImportStatus Status { get; set; } = RosterImportStatus.Processing;

    /// <summary>导出的 CSV（OSS）</summary>
    [Column(Position = 11, StringLength = 256)]
    public string? CsvFileUrl { get; set; }

    /// <summary>CSV 有效期（建议 7 天）</summary>
    [Column(Position = 12)]
    public DateTime? CsvExpiresAt { get; set; }

    /// <summary>创建时间</summary>
    [Column(Position = 13)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>整理完成时间</summary>
    [Column(Position = 14)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 名单手机号原始列表（JSON 数组，jsonb）。
    /// 导入时存原始列表；Agent 整理后覆盖为清洗后列表（去重 + 合法），供预览/生成一次性码使用。
    /// 说明：DS01 未定义原始名单存储字段，为支撑 UC-6.6/6.7/6.8 数据流补充（切片验证）。
    /// </summary>
    [Column(Position = 15, DbType = "jsonb")]
    public string? RawPhonesJson { get; set; }
}