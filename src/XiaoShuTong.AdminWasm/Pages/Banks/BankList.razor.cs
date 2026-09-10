using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Banks;

public partial class BankList
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 数据 ───

    private BankListItemDto[] _Banks = [];
    private bool _Loading;
    private string? _SelectedSubject;

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
            var svc = User.Use<IListBanksService>();
            var res = await svc.Execute(new ListBanksReqDto
            {
                Subject = _SelectedSubject,
                PageIndex = 1,
                PageSize = 100
            }, CancellationToken.None);
            if (res.Success)
            {
                _Banks = res.Items?.ToArray() ?? [];
            }
            else
            {
                Message.Error($"加载失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载失败: {ex.Message}");
        }
        finally
        {
            _Loading = false;
            StateHasChanged();
        }
    }

    private async Task OnSubjectFilterAsync(string? subject)
    {
        _SelectedSubject = subject;
        await LoadDataAsync();
    }

    #endregion

    #region ─── 辅助方法 ───

    private static string GetSubjectLabel(string value) => value switch
    {
        "Chinese" => "语文",
        "Math" => "数学",
        "English" => "英语",
        "Physics" => "物理",
        "Chemistry" => "化学",
        "Biology" => "生物",
        "History" => "历史",
        "Geography" => "地理",
        "Politics" => "政治",
        _ => value,
    };

    private static string GetPurposeLabel(string value) => value switch
    {
        "Memorize" => "背诵",
        "Assess" => "检验",
        "Play" => "趣味",
        _ => value,
    };

    private static string GetPrivacyLabel(string value) => value switch
    {
        "Private" => "私有",
        "Public" => "公开",
        "Group" => "群组",
        _ => value,
    };

    #endregion
}
