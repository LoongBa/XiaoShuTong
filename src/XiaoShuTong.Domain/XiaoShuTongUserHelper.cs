using TKW.Framework.Core.Hosting;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Session;
using TKW.Framework.Enumerations;
using System.Threading.Tasks;

namespace XiaoShuTong;

/// <summary>
/// 用户帮助器，负责创建 DomainUser。
/// TODO Agent: 按实际认证体系调整 OnUserLoginAsync 中的字段映射和验证逻辑。
/// 联调放行（V0.6.3）：OnLoginByWeChatAppletAsync 白名单返回固定身份，仅 Development 启用
/// （运行时守卫），生产须由 OnLoginByPasswordAsync/微信 OAuth 替代。
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

    /// <summary>
    /// 联调放行（V0.6.3）：白名单身份，仅 Development 启用（Oracle 闭环#1 运行时守卫）。
    /// 生产环境必须由 OnLoginByPasswordAsync/微信 OAuth 真实认证替代，严禁携带白名单上线。
    /// </summary>
    protected override async Task<XiaoShuTongUserInfo> OnLoginByWeChatAppletAsync(
        DomainUser<XiaoShuTongUserInfo> user,
        string userName,
        string credential,
        string authInfo)
    {
        // 运行时守卫：非 Development 环境直接拒绝（代码级防线，防白名单入生产）
        // DomainHost.Options 直接暴露 DomainOptions（Host.ServiceProvider 解析 DomainOptions 可能为 null——XML 无注册保证）
        if (Host?.Options is not { IsDevelopment: true })
            throw new NotSupportedException("联调放行（OnLoginByWeChatAppletAsync 白名单）仅 Development 环境启用");

        // 白名单映射：与 XiaoShuTongDomainInitializer.data.cs 种子身份 Id 对齐（T4）
        return (userName) switch
        {
            "xiaoming" => new XiaoShuTongUserInfo
            {
                Id = 10001,
                UserIdString = "10001",
                UserName = "xiaoming",
                DisplayName = "小明",
                LoginFrom = EnumLoginFrom.MobileWeb,
                Roles = new List<string> { "student" },
            },
            "owner01" => new XiaoShuTongUserInfo
            {
                Id = 10002,
                UserIdString = "10002",
                UserName = "owner01",
                DisplayName = "群主老师",
                LoginFrom = EnumLoginFrom.MobileWeb,
                Roles = new List<string> { "owner" },
            },
            "parent01" => new XiaoShuTongUserInfo
            {
                Id = 10003,
                UserIdString = "10003",
                UserName = "parent01",
                DisplayName = "家长",
                LoginFrom = EnumLoginFrom.MobileWeb,
                Roles = new List<string> { "parent" },
            },
            _ => throw new NotSupportedException($"联调白名单外账号：{userName}"),
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