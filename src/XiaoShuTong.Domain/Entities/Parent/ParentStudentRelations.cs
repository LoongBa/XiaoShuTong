using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Parent;

/// <summary>
/// 家长-学生关联（授权链，同一家长同一孩子唯一）
/// </summary>
/// <remarks>
/// 软删除=否（授权链关系，历史保留）；家长私域数据，查询默认带 ParentId 过滤。
/// </remarks>
[Table(Name = nameof(ParentStudentRelations), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_parentrelations_uid", nameof(UId), IsUnique = true)]
[Index("idx_parentrelations_parentid", nameof(ParentId), IsUnique = false)]
[Index("idx_parentrelations_studentid", nameof(StudentId), IsUnique = false)]
[Index("idx_parentrelations_pair", "ParentId,StudentId", IsUnique = true)]
public partial class ParentStudentRelations
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>家长</summary>
    [Column(Position = 3)]
    [DtoField(IsSearchable = true)]
    public long ParentId { get; set; }

    /// <summary>孩子</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long StudentId { get; set; }

    /// <summary>关系（Parent/Guardian/Grandparent）</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 20)]
    public ParentRelation Relation { get; set; } = ParentRelation.Parent;

    /// <summary>创建时间（D02 原 CreatedAt）</summary>
    [Column(Position = 6)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（框架审计字段）</summary>
    [Column(Position = 7, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}