using AntDesign.Extensions.Localization;
using AntDesign.ProLayout;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net.Http.Json;

namespace XiaoShuTong.AdminWasm.Layouts
{
    public partial class BasicLayout() : LayoutComponentBase, IDisposable
    {
        private MenuDataItem[] _MenuData;

        [Inject] private ReuseTabsService TabService { get; set; }

        [Inject] private ILocalizationService LocalizationService { get; set; }

        [Inject] private HttpClient HttpClient { get; set; }

        private EventHandler<CultureInfo> _LocalizationChanged;


        protected override async Task OnInitializedAsync()
        {
            _LocalizationChanged = (sender, args) => InvokeAsync(StateHasChanged);
            LocalizationService.LanguageChanged += _LocalizationChanged;
            _MenuData = await HttpClient.GetFromJsonAsync<MenuDataItem[]>("data/menu.json");
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
