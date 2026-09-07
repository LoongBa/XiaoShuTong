using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.GroupManagement;

/// <summary>
/// 群组成员（学生/家长）
/// </summary>
/// <remarks>
/// 移除成员 = 删除行，学习数据不删；软删除=否。
/// 唯一约束：同一群组同一用户同一角色唯一。
/// </remarks>
[Table(Name = nameof(GroupMembers), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_groupmembers_uid", nameof(UId), IsUnique = true)]
[Index("idx_groupmembers_groupid_role", "GroupId,Role", IsUnique = false)]
[Index("idx_groupmembers_userid", nameof(UserId), IsUnique = false)]
[Index("idx_groupmembers_invitecodeid", nameof(InviteCodeId), IsUnique = false)]
[Index("idx_groupmembers_groupid_userid_role", "GroupId,UserId,Role", IsUnique = true)]
public partial class GroupMembers
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

    /// <summary>成员用户</summary>
    [Column(Position = 4)]
    [DtoField(IsSearchable = true)]
    public long UserId { get; set; }

    /// <summary>成员角色（Student/Parent）</summary>
    [Column(Position = 5, MapType = typeof(string), StringLength = 20)]
    [DtoField(IsSearchable = true)]
    public MemberRole Role { get; set; } = MemberRole.Student;

    /// <summary>群内昵称（学生号/姓名）</summary>
    [Column(Position = 6, StringLength = 64)]
    public string? Nickname { get; set; }

    /// <summary>加入所用一次性邀请码（溯源）</summary>
    [Column(Position = 7)]
    [DtoField(IsSearchable = true)]
    public long? InviteCodeId { get; set; }

    /// <summary>加入时间</summary>
    [Column(Position = 8)]
    [DtoField(CanModify = false)]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
