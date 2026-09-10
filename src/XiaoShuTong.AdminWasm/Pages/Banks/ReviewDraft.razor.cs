using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Banks;

public partial class ReviewDraft
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 路由参数 ───

    [SupplyParameterFromQuery] public string? BatchId { get; set; }

    #endregion

    #region ─── 数据 ───

    private GetDraftBatchResDto? _Draft;
    private List<bool> _Approved = [];
    private List<string> _EditedStems = [];
    private List<string> _EditedAnswers = [];
    private bool _Loading;
    private bool _Submitting;
    private ReviewBackingPointsResDto? _Result;

    private List<int> ApprovedItems
    {
        get
        {
            var list = new List<int>();
            for (var i = 0; i < _Approved.Count; i++)
            {
                if (_Approved[i]) list.Add(i);
            }
            return list;
        }
    }

    private readonly (string Value, string Label)[] _Subjects =
    [
        ("Chinese", "语文"),
        ("Math", "数学"),
        ("English", "英语"),
        ("Physics", "物理"),
        ("Chemistry", "化学"),
        ("Biology", "生物"),
        ("History", "历史"),
        ("Geography", "地理"),
        ("Politics", "政治"),
    ];

    #endregion

    #region ─── 生命周期 ───

    protected override async Task OnInitializedAsync()
    {
        await LoadDraftAsync();
    }

    private async Task LoadDraftAsync()
    {
        if (string.IsNullOrEmpty(BatchId))
        {
            Message.Warning("缺少批次 ID");
            return;
        }

        _Loading = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGetDraftBatchService>();
            var res = await svc.Execute(new GetDraftBatchReqDto { BatchId = BatchId }, CancellationToken.None);
            if (res.Success)
            {
                _Draft = res;
                _Approved = res.Items.Select(_ => false).ToList();
                _EditedStems = res.Items.Select(x => x.Stem).ToList();
                _EditedAnswers = res.Items.Select(x => x.Answer).ToList();
            }
            else
            {
                Message.Error($"加载草稿失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载草稿失败: {ex.Message}");
        }
        finally
        {
            _Loading = false;
            StateHasChanged();
        }
    }

    private void SelectAllAsync()
    {
        for (var i = 0; i < _Approved.Count; i++)
        {
            _Approved[i] = true;
        }
        StateHasChanged();
    }

    private string GetSubjectLabel(string subject)
        => _Subjects.FirstOrDefault(s => s.Value.Equals(subject, StringComparison.OrdinalIgnoreCase)).Label
           ?? subject;

    #endregion

    #region ─── 提交入库 ───

    private async Task OnSubmitAsync()
    {
        if (_Draft == null || ApprovedItems.Count == 0)
        {
            Message.Warning("请至少勾选一条通过");
            return;
        }

        _Submitting = true;
        _Result = null;
        StateHasChanged();
        try
        {
            var svc = User.Use<IReviewBackingPointsService>();
            var items = ApprovedItems
                .Select(i => new ReviewItemInputDto
                {
                    QuestionId = _Draft!.Items[i].QuestionId,
                    EditedStem = _EditedStems[i] == _Draft.Items[i].Stem ? null : _EditedStems[i],
                    EditedAnswer = _EditedAnswers[i] == _Draft.Items[i].Answer ? null : _EditedAnswers[i],
                })
                .ToList();

            var res = await svc.Execute(new ReviewBackingPointsReqDto
            {
                BankId = _Draft.BankId,
                BatchId = _Draft.BatchId,
                Items = items,
                SkipUnreviewed = true,
            }, CancellationToken.None);

            _Result = res;
            if (res.Success)
            {
                Message.Success($"入库 {res.Imported} 条，跳过 {res.Skipped} 条");
            }
            else
            {
                Message.Error($"入库失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"入库失败: {ex.Message}");
        }
        finally
        {
            _Submitting = false;
            StateHasChanged();
        }
    }

    #endregion
}
