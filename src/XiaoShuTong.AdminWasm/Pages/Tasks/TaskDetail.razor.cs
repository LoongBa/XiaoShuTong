using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Services.TaskManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Tasks;

public partial class TaskDetail
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public ModalService Modal { get; set; } = null!;
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

    #region ─── 编辑任务（V0.8.1 UpdateTaskService 消费端接入） ───

    private bool _EditVisible;
    private UpdateTaskReqDto _EditForm = new();
    private bool _Editing;
    private string? _EditSelectedBankId;
    private string _EditBankName = string.Empty;
    private List<string> _EditAllQuestionIds = [];
    private string[] _EditSelectedQuestionIds = [];
    private bool _LoadingEditBankDetail;

    private async Task ShowEditModalAsync()
    {
        // 回填：Title/Description/DeadlineAt/QuestionIds 直接来自 Task（TasksDto 契约含 QuestionIds）
        _EditForm = new UpdateTaskReqDto
        {
            TaskUid = TaskUid,
            Title = _TaskDetail?.Task?.Title,
            Description = _TaskDetail?.Task?.Description,
            DeadlineAt = _TaskDetail?.Task?.DeadlineAt,
            QuestionIds = _TaskDetail?.Task?.QuestionIds,
        };
        _EditSelectedQuestionIds = _TaskDetail?.Task?.QuestionIds ?? [];
        _EditSelectedBankId = _TaskDetail?.Task?.BankId;
        _EditBankName = string.Empty;
        _EditAllQuestionIds = [];
        _EditVisible = true;
        await LoadEditQuestionsAsync();
    }

    private void OnEditCancel()
    {
        _EditVisible = false;
    }

    /// <summary>加载当前题库题目列表（编辑题集用；题库 Disabled——BR-26 关联 BankId 不可变更）</summary>
    private async Task LoadEditQuestionsAsync()
    {
        if (string.IsNullOrEmpty(_EditSelectedBankId))
        {
            // 自由编排任务（无题库）→ 无题目列表，题集保持原值
            return;
        }

        _LoadingEditBankDetail = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGetBankDetailService>();
            var res = await svc.Execute(new GetBankDetailReqDto { BankId = _EditSelectedBankId }, CancellationToken.None);
            if (res.Success)
            {
                _EditBankName = res.Bank?.Name ?? string.Empty;
                var all = new List<string>();
                foreach (var topic in res.Topics ?? [])
                {
                    foreach (var qid in topic.QuestionIds ?? [])
                    {
                        all.Add(qid);
                    }
                }
                _EditAllQuestionIds = all;
                // 保持既有选中：仅保留仍在该题库内的题（题集变更以当前勾选为准）
                if (_EditSelectedQuestionIds.Length > 0)
                {
                    var set = _EditAllQuestionIds.ToHashSet(StringComparer.Ordinal);
                    _EditSelectedQuestionIds = _EditSelectedQuestionIds.Where(set.Contains).ToArray();
                }
            }
            else
            {
                Message.Error($"加载题库详情失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载题库详情失败: {ex.Message}");
        }
        finally
        {
            _LoadingEditBankDetail = false;
            StateHasChanged();
        }
    }

    private void OnEditQuestionsSelectionChanged(string[] values)
    {
        _EditSelectedQuestionIds = values;
        StateHasChanged();
    }

    private async Task OnEditOkAsync()
    {
        if (string.IsNullOrWhiteSpace(_EditForm.Title))
        {
            Message.Warning("请输入任务名称");
            return;
        }

        // Oracle C3：清空题集（与详情原题集不同且为空）→ 二次确认 + 提示不重算
        var originalIds = _TaskDetail?.Task?.QuestionIds ?? [];
        var clearing = _EditSelectedQuestionIds.Length == 0 && originalIds.Length > 0;
        if (clearing)
        {
            var ok = await Modal.ConfirmAsync(new ConfirmOptions
            {
                Title = "清空题集确认",
                Content = "清空后任务将无题目（题目数=0），已分配成员进度保持不变（不重算）。确认清空？",
                OkText = "确认清空",
                CancelText = "取消",
                OkType = ButtonType.Primary,
            });
            if (!ok) return;
        }

        // M2：原样回传题集（SetEquals 幂等跳过重算）；DatePicker 未选值（null）→ 不修改截止
        _EditForm.QuestionIds = _EditSelectedQuestionIds;
        _Editing = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IUpdateTaskService>();
            var res = await svc.Execute(_EditForm, CancellationToken.None);
            if (res.Success)
            {
                Message.Success(res.QuestionSetChanged
                    ? $"已保存，{res.RecalculatedAssignments} 名成员进度已重算"
                    : "已保存");
                _EditVisible = false;
                await LoadDataAsync();
            }
            else
            {
                Message.Error($"保存失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"保存失败: {ex.Message}");
        }
        finally
        {
            _Editing = false;
            StateHasChanged();
        }
    }

    #endregion
}
