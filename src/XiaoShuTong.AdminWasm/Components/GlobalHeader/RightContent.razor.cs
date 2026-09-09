using AntDesign.Extensions.Localization;
using AntDesign.ProLayout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Globalization;

namespace XiaoShuTong.AdminWasm.Components
{
    public partial class RightContent : IDisposable
    {
        private string _DisplayName = "未登录";

        private AvatarMenuItem[] AvatarMenuItems =>
            [
                new() { Key = "logout", IconType = "logout", Option = "退出登录" }
            ];

        [Inject] protected NavigationManager NavigationManager { get; set; } = null!;
        [Inject] protected DomainClientUser User { get; set; } = null!;
        [Inject] private ILocalizationService LocalizationService { get; set; } = null!;
        [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            SetClassMap();

            // 双重验证：始终检查 IsAuthenticated，不依赖缓存
            RefreshUserInfo();

            // 订阅认证状态变化（登录/注销/会话过期），实时刷新用户信息
            AuthStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        }

        private void RefreshUserInfo()
        {
            if (User.IsAuthenticated)
            {
                _DisplayName = User.DisplayName ?? User.UserName ?? "用户";
            }
            else
            {
                // 未登录/会话过期 → 显示占位信息，不读取旧数据
                _DisplayName = "未登录";
            }
        }

        private async void OnAuthStateChanged(Task<AuthenticationState> task)
        {
            try
            {
                await InvokeAsync(() =>
                {
                    RefreshUserInfo();
                    StateHasChanged();
                });
            }
            catch
            {
                // 忽略组件已释放时的异常
            }
        }

        protected void SetClassMap()
        {
            ClassMapper
                .Clear()
                .Add("right");
        }

        public async Task HandleSelectUser(MenuItem item)
        {
            if (item.Key == "logout")
            {
                await User.LogoutAsync();
                RefreshUserInfo();
                NavigationManager.NavigateTo("/user/login");
            }
        }

        public void HandleSelectLang(MenuItem item)
        {
            LocalizationService.SetLanguage(CultureInfo.GetCultureInfo(item.Key));
        }

        public new void Dispose()
        {
            AuthStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
            base.Dispose();
        }
    }
}
