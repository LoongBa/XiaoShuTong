using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Bank;

/// <summary>
/// 题库元数据（API 查询索引）
/// </summary>
/// <remarks>
/// 内容权威 = JsonPath 指向的 JSON 文件；DB 行仅为查询索引（列表/详情/筛选），题目内容镜像不含答案（防爬）。
/// privacy=private 按 OwnerId 行级安全；官方题库 OwnerId=NULL 只读。软删除=否（停用走 Status=Archived）。
/// </remarks>
[Table(Name = nameof(Banks), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_banks_uid", nameof(UId), IsUnique = true)]
[Index("idx_banks_bankid", nameof(BankId), IsUnique = true)]
[Index("idx_banks_subject", nameof(Subject), IsUnique = false)]
[Index("idx_banks_ownerid", nameof(OwnerId), IsUnique = false)]
// 注：Tags(string[] JSONB) 不标 [Index]——xCodeGen 的 Conditions 生成器不支持数组列索引（GIN 索引由适配层配置）
public partial class Banks
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>业务键（chinese-7to9-pep）</summary>
    [Column(Position = 3, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string BankId { get; set; } = string.Empty;

    /// <summary>题库名（≤128）</summary>
    [Column(Position = 4, StringLength = 128)]
    [DtoField(IsSearchable = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>学科</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public Subject Subject { get; set; } = Subject.Chinese;

    /// <summary>内容版本（MinVer）</summary>
    [Column(Position = 6, StringLength = 20)]
    public string Version { get; set; } = "V1.0";

    /// <summary>用途（Memorize/Assess/Play）</summary>
    [Column(Position = 7, MapType = typeof(string), StringLength = 20)]
    public BankPurpose Purpose { get; set; } = BankPurpose.Memorize;

    /// <summary>隐私（Private/Public/Group）</summary>
    [Column(Position = 8, MapType = typeof(string), StringLength = 20)]
    public BankPrivacy Privacy { get; set; } = BankPrivacy.Private;

    /// <summary>私域题库 owner（官方题库 NULL）</summary>
    [Column(Position = 9)]
    [DtoField(IsSearchable = true)]
    public long? OwnerId { get; set; }

    /// <summary>bank.v1.json 文件路径（内容权威）</summary>
    [Column(Position = 10, StringLength = 256)]
    public string JsonPath { get; set; } = string.Empty;

    /// <summary>标签（JSONB，如 ["高频考点"]）</summary>
    [Column(Position = 11, DbType = "jsonb")]
    public string[] Tags { get; set; } = [];

    /// <summary>状态（Active/Hidden/Archived）</summary>
    [Column(Position = 12, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public BankStatus Status { get; set; } = BankStatus.Active;

    /// <summary>创建时间</summary>
    [Column(Position = 13)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    [Column(Position = 14, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
