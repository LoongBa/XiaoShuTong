using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Services.TaskManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Tasks;

public partial class WeeklyReport
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;

    #endregion

    #region ─── 群组 ───

    private GroupListItemDto[] _Groups = [];
    private string? _SelectedGroupUid;
    private string? _SelectedGroupName;

    #endregion

    #region ─── 周窗口 ───

    private DateOnly? _WeekStart;

    #endregion

    #region ─── 报告数据 ───

    private ExportWeeklyReportResDto? _Report;
    private bool _Generating;

    #endregion

    #region ─── 生命周期 ───

    protected override async Task OnInitializedAsync()
    {
        _WeekStart = GetMonday(DateOnly.FromDateTime(DateTime.Today));
        await LoadGroupsAsync();
    }

    #endregion

    #region ─── 数据加载 ───

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
                    _SelectedGroupName = _Groups[0].Name;
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
        finally
        {
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 事件处理 ───

    private void OnGroupChanged(object value)
    {
        _Report = null;
        if (value is GroupListItemDto group)
        {
            _SelectedGroupUid = group.GroupUid;
            _SelectedGroupName = group.Name;
        }
    }

    private void OnWeekStartChanged(DateOnly? date)
    {
        _WeekStart = date;
        _Report = null;
    }

    private async Task OnGenerateAsync()
    {
        if (string.IsNullOrEmpty(_SelectedGroupUid))
        {
            Message.Warning("请选择群组");
            return;
        }

        _Generating = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IExportWeeklyReportService>();
            var res = await svc.Execute(new ExportWeeklyReportReqDto
            {
                GroupUid = _SelectedGroupUid,
                WeekStart = _WeekStart
            }, CancellationToken.None);

            if (res.Success)
            {
                _Report = res;
                Message.Success("周报生成成功");
            }
            else
            {
                Message.Error($"生成失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"生成失败: {ex.Message}");
        }
        finally
        {
            _Generating = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 辅助方法 ───

    private static DateOnly GetMonday(DateOnly date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    #endregion
}
