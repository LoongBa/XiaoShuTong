using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Services.TaskManagement.Api;
using XiaoShuTong.Services.Shared;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Dashboard;

public partial class Index
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 数据 ───

    private GroupListItemDto[] _Groups = [];
    private string? _SelectedGroupUid;
    private GetOwnerDashboardResDto? _DashboardData;
    private bool _Loading;

    protected override async Task OnInitializedAsync()
    {
        await LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var svc = User.Use<IListGroupsService>();
            var res = await svc.Execute(new ListGroupsReqDto { PageIndex = 1, PageSize = 100 }, CancellationToken.None);
            if (res.Success)
            {
                _Groups = res.Items?.ToArray() ?? [];
                if (_Groups.Length > 0)
                {
                    _SelectedGroupUid = _Groups[0].GroupUid;
                    await LoadDashboardAsync();
                }
            }
            else
            {
                Message.Error($"加载群组失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载群组失败: {ex.Message}");
        }
    }

    private async Task OnGroupChanged(object value)
    {
        _SelectedGroupUid = value?.ToString();
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        if (string.IsNullOrEmpty(_SelectedGroupUid))
            return;

        _Loading = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGetOwnerDashboardService>();
            var res = await svc.Execute(new GetOwnerDashboardReqDto { GroupUid = _SelectedGroupUid }, CancellationToken.None);
            if (res.Success)
            {
                _DashboardData = res;
            }
            else
            {
                Message.Error($"加载看板失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载看板失败: {ex.Message}");
        }
        finally
        {
            _Loading = false;
            StateHasChanged();
        }
    }

    #endregion
}
