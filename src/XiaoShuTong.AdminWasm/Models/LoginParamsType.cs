// ─
// 登录参数模型 — 密码登录
//
// LoginType:
//   Password = 用户名+密码登录（默认，✅ 测试通过）
// ─

using System.ComponentModel.DataAnnotations;
using TKW.Framework.Enumerations;

namespace XiaoShuTong.AdminWasm.Models
{
    public class LoginParamsType
    {
        [Required] public string UserName { get; set; } = "admin";

        [Required] public string Password { get; set; } = "123456";

        public EnumLoginAuthType LoginType { get; set; } = EnumLoginAuthType.Password;
    }
}
