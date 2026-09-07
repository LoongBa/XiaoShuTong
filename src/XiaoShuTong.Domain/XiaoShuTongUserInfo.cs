using TKW.Framework.Domain.Interfaces;

namespace XiaoShuTong;

/// <summary>
/// 项目级用户信息。
/// 继承自 SimpleUserInfo，提供 UserIdString/UserName/DisplayName/IsInRole 等基础实现。
/// 按业务需求增补其他属性（如 OwnerId、OwnerName、RealName 等）。
/// </summary>
/// <remarks>可以使用 DomainUser{T}，项目里不是必须创建新的 UserInfo 子类</remarks>
public class XiaoShuTongUserInfo : SimpleUserInfo, IUserInfo
{
    public XiaoShuTongUserInfo()
    {
    }

    public XiaoShuTongUserInfo(string userIdString, string userName) : base(userIdString, userName)
    {
    }

    public XiaoShuTongUserInfo(int userId, string userName) : base(userId, userName)
    {
    }

    public XiaoShuTongUserInfo(Guid userGuid, string userName) : base(userGuid, userName)
    {
    }

    /// <summary>
    /// 用户内部主键（long，对应 Users 表 Id）。
    /// 群组域实体（Groups.OwnerId / GroupMembers.UserId 等）以此作为外键。
    /// 由认证体系（模块 1）在登录时填充；测试中直接赋值。
    /// </summary>
    public long Id { get; set; }
}