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
                    // 先渲染选项、再写选中值：同帧写入会触发 Select 的 Value/Options 时序竞态——
                    // 组件经 bind-Value 自清值（绕过 OnGroupChanged），选择器只显示占位符。
                    // 拆成两帧后时序等同用户手选路径，标签可正确回显。
                    StateHasChanged();
                    await Task.Yield();
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
        // Ant Select 在 Value/Items 绑定时序中会同步回调 null（非用户操作：Select 未开 AllowClear，UI 无清除路径）；
        // 忽略该时序性 null，避免把已选群组误清空导致看板请求 groupUid 为 null（400）。
        if (value is null)
            return;
        _SelectedGroupUid = value?.ToString();
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        // 快照局部变量：守卫通过后 StateHasChanged 触发同步渲染，可能经 OnGroupChanged 回调改写
        // _SelectedGroupUid（TOCTOU），DTO 构造须与守卫读同一值，防止序列化为 null。
        var groupUid = _SelectedGroupUid;
        if (string.IsNullOrEmpty(groupUid))
            return;

        _Loading = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGetOwnerDashboardService>();
            var res = await svc.Execute(new GetOwnerDashboardReqDto { GroupUid = groupUid }, CancellationToken.None);
            if (res.Success)
            {
                _DashboardData = res;
                // 自动选组路径首渲染时 Select 的 Value/Options 同帧到位，组件可能经 bind-Value 自清字段
                // （绕过 OnGroupChanged）；数据加载成功后回写快照值，保持选择器显示与看板一致。
                if (_SelectedGroupUid != groupUid)
                    _SelectedGroupUid = groupUid;
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
