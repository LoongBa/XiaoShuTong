using AntDesign.Extensions.Localization;
using AntDesign.ProLayout;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net.Http.Json;

namespace XiaoShuTong.AdminWasm.Layouts
{
    public partial class BasicLayout() : LayoutComponentBase, IDisposable
    {
        private MenuDataItem[] _MenuData = [];

        [Inject] private ReuseTabsService TabService { get; set; } = null!;

        [Inject] private ILocalizationService LocalizationService { get; set; } = null!;

        [Inject] private HttpClient HttpClient { get; set; } = null!;

        private EventHandler<CultureInfo> _LocalizationChanged = (_, _) => { };


        protected override async Task OnInitializedAsync()
        {
            _LocalizationChanged = (sender, args) => InvokeAsync(StateHasChanged);
            LocalizationService.LanguageChanged += _LocalizationChanged;
            _MenuData = await HttpClient.GetFromJsonAsync<MenuDataItem[]>("data/menu.json") ?? [];
        }

        void Reload()
        {
            TabService.ReloadPage();
        }

        public void Dispose()
        {
            LocalizationService.LanguageChanged -= _LocalizationChanged;
            GC.SuppressFinalize(this);
        }

    }
}
