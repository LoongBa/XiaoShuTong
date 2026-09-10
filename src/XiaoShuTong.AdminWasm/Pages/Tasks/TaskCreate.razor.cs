using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Services.TaskManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Tasks;

public partial class TaskCreate
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 步骤状态 ───

    private int _CurrentStep;
    private bool _Loading;
    private bool _Submitting;

    #endregion

    #region ─── 表单数据 ───

    private GroupListItemDto[] _Groups = [];
    private string? _SelectedGroupUid;
    private string? _SelectedGroupName;
    private string? _SelectedBankId;
    private string _Title = string.Empty;
    private string? _Description;
    private DateTime? _DeadlineAt;
    private bool _AllowRedo;

    #endregion

    #region ─── 生命周期 ───

    protected override async Task OnInitializedAsync()
    {
        await LoadGroupsAsync();
    }

    #endregion

    #region ─── 数据加载 ───

    private async Task LoadGroupsAsync()
    {
        _Loading = true;
        StateHasChanged();
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
            _Loading = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 提交 ───

    private async Task OnSubmitAsync()
    {
        if (string.IsNullOrEmpty(_SelectedGroupUid))
        {
            Message.Warning("请选择群组");
            return;
        }

        if (string.IsNullOrWhiteSpace(_Title))
        {
            Message.Warning("请输入任务名称");
            return;
        }

        _Submitting = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<ICreateTaskService>();
            var res = await svc.Execute(new CreateTaskReqDto
            {
                GroupUid = _SelectedGroupUid,
                BankId = _SelectedBankId,
                Title = _Title,
                Description = _Description,
                QuestionIds = [], // TODO: 需要题目选择功能（迭代3实现）
                AllowRedo = _AllowRedo,
                DeadlineAt = _DeadlineAt,
            }, CancellationToken.None);

            if (res.Success)
            {
                Message.Success($"已分配给 {res.AssignedCount} 名学生");
                Navigation.NavigateTo($"/owner/tasks/detail/{res.TaskUid}");
            }
            else
            {
                Message.Error($"发布失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"发布失败: {ex.Message}");
        }
        finally
        {
            _Submitting = false;
            StateHasChanged();
        }
    }

    #endregion
}
