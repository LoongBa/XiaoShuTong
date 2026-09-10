using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.TaskManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Tasks;

public partial class TaskDetail
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 路由参数 ───

    [Parameter] public string TaskUid { get; set; } = string.Empty;

    #endregion

    #region ─── 数据 ───

    private GetTaskDetailResDto? _TaskDetail;
    private bool _Loading;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _Loading = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGetTaskDetailService>();
            var res = await svc.Execute(new GetTaskDetailReqDto { TaskUid = TaskUid }, CancellationToken.None);
            if (res.Success)
            {
                _TaskDetail = res;
            }
            else
            {
                Message.Error($"加载任务详情失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载任务详情失败: {ex.Message}");
        }
        finally
        {
            _Loading = false;
            StateHasChanged();
        }
    }

    #endregion
}
