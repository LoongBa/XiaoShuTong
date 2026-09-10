using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Bank.Api;
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

    #region ─── 题库/题目选择 ───

    private BankListItemDto[] _Banks = [];
    private List<TopicNodeDto> _Topics = [];
    private List<SelectableQuestion> _AllQuestions = [];
    private string[] _SelectedQuestionIds = [];
    private bool _LoadingBankDetail;

    private sealed class SelectableQuestion
    {
        public string QuestionId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
    }

    #endregion

    #region ─── 生命周期 ───

    protected override async Task OnInitializedAsync()
    {
        await LoadGroupsAndBanksAsync();
    }

    #endregion

    #region ─── 数据加载 ───

    private async Task LoadGroupsAndBanksAsync()
    {
        _Loading = true;
        StateHasChanged();
        try
        {
            var groupsTask = User.Use<IListGroupsService>()
                .Execute(new ListGroupsReqDto { PageIndex = 1, PageSize = 100 }, CancellationToken.None);
            var banksTask = User.Use<IListBanksService>()
                .Execute(new ListBanksReqDto { PageIndex = 1, PageSize = 100 }, CancellationToken.None);

            await Task.WhenAll(groupsTask, banksTask);

            var groupsRes = groupsTask.Result;
            if (groupsRes.Success)
            {
                _Groups = groupsRes.Items?.ToArray() ?? [];
                if (_Groups.Length > 0)
                {
                    _SelectedGroupUid = _Groups[0].GroupUid;
                    _SelectedGroupName = _Groups[0].Name;
                }
            }
            else
            {
                Message.Error($"加载群组失败: {groupsRes.ErrorCode}");
            }

            var banksRes = banksTask.Result;
            if (banksRes.Success)
            {
                _Banks = banksRes.Items?.ToArray() ?? [];
            }
            else
            {
                Message.Error($"加载题库失败: {banksRes.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载数据失败: {ex.Message}");
        }
        finally
        {
            _Loading = false;
            StateHasChanged();
        }
    }

    private async Task OnBankSelectedAsync(string? bankId)
    {
        _SelectedBankId = bankId;
        _SelectedQuestionIds = [];
        _AllQuestions = [];
        _Topics = [];

        if (string.IsNullOrEmpty(bankId))
        {
            StateHasChanged();
            return;
        }

        _LoadingBankDetail = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGetBankDetailService>();
            var res = await svc.Execute(new GetBankDetailReqDto { BankId = bankId }, CancellationToken.None);
            if (res.Success)
            {
                _Topics = res.Topics ?? [];
                var allQuestions = new List<SelectableQuestion>();
                foreach (var topic in _Topics)
                {
                    foreach (var qid in topic.QuestionIds)
                    {
                        allQuestions.Add(new SelectableQuestion
                        {
                            QuestionId = qid,
                            Content = qid,
                            TopicTitle = topic.Title
                        });
                    }
                }
                _AllQuestions = allQuestions;
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
            _LoadingBankDetail = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 题目选择 ───

    private void OnSelectAllQuestions(bool isChecked)
    {
        _SelectedQuestionIds = isChecked
            ? _AllQuestions.Select(q => q.QuestionId).ToArray()
            : [];
        StateHasChanged();
    }

    private void OnQuestionsSelectionChanged(string[] values)
    {
        _SelectedQuestionIds = values;
        StateHasChanged();
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
                QuestionIds = _SelectedQuestionIds,
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
