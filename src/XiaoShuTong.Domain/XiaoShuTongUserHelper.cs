using TKW.Framework.Domain;
using TKW.Framework.Domain.Session;
using TKW.Framework.Enumerations;
using System.Threading.Tasks;

namespace XiaoShuTong;

/// <summary>
/// 用户帮助器，负责创建 DomainUser。
/// TODO Agent: 按实际认证体系调整 OnUserLoginAsync 中的字段映射和验证逻辑。
/// </summary>
public class XiaoShuTongUserHelper : DomainUserHelperBase<XiaoShuTongUserInfo>
{
    protected override async Task<XiaoShuTongUserInfo> OnNewGuestSessionCreatedAsync(SessionInfo session)
    {
        // 游客身份，最小化信息
        return new XiaoShuTongUserInfo
        {
            UserIdString = "GUEST",
            UserName     = "Guest",
            DisplayName  = "游客",
            LoginFrom    = EnumLoginFrom.Unset,
            Roles        = new List<string>()
        };
    }

    protected override async Task<XiaoShuTongUserInfo> OnLoginByPasswordAsync(
        DomainUser<XiaoShuTongUserInfo> user,
        string userName,
        string credential,
        EnumLoginFrom loginFrom)
    {
        // TODO Agent: 实现登录验证逻辑（查数据库、验证密码等）
        throw new System.NotImplementedException("登录验证逻辑待实现");
    }
}