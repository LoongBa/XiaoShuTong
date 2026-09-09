// ─
// 登录页面 —— 密码登录
//
// 推荐用法（生产）：
//   var ctx = AuthContext.Password(deviceId: "xst-web");
//   var result = await User.LoginAsAsync(userName, password, ctx);
// ─

using XiaoShuTong.AdminWasm.Models;
using Microsoft.AspNetCore.Components;
using TKW.Framework.Domain;

namespace XiaoShuTong.AdminWasm.Pages.User
{
    public partial class Login
    {
        private readonly LoginParamsType _Model = new();
        private string? _returnUrl;

        [Inject] public NavigationManager NavigationManager { get; set; } = null!;
        [Inject] public DomainClientUser User { get; set; } = null!;
        [Inject] public MessageService Message { get; set; } = null!;

        protected override void OnInitialized()
        {
            _returnUrl = new Uri(NavigationManager.Uri)
                .Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Split('=', 2))
                .Where(p => p.Length == 2 && p[0] == "returnUrl")
                .Select(p => Uri.UnescapeDataString(p[1]))
                .FirstOrDefault();
        }

        /// <summary>
        /// 登录入口 —— 密码登录。
        /// 成功路径调用 DomainClientUser.LoginAsAsync()（统一认证入口，走服务端 loginByContext mutation）。
        /// </summary>
        private async Task HandleSubmit()
        {
            try
            {
                // AuthContext 用法：指定客户端类型和设备标识，服务端按 deviceId 管理多端会话
                var ctx = AuthContext.Password(deviceId: "xst-web");
                var result = await User.LoginAsAsync(_Model.UserName, _Model.Password, ctx);

                if (result.Success)
                {
                    var safeUrl = _returnUrl;
                    if (!string.IsNullOrEmpty(safeUrl) && !safeUrl.StartsWith('/'))
                        safeUrl = null; // open redirect guard
                    NavigationManager.NavigateTo(safeUrl ?? "/");
                }
                else
                {
                    await Message.ErrorAsync("登录失败：用户名或密码错误");
                }
            }
            catch (System.Exception ex)
            {
                await Message.ErrorAsync($"登录失败：{ex.ToDisplayMessage()}");
            }
        }
    }
}
