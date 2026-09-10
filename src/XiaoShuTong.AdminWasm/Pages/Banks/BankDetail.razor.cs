using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Entities.Bank.DTOs.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Banks;

public partial class BankDetail
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 路由参数 ───

    [Parameter] public string BankId { get; set; } = string.Empty;

    #endregion

    #region ─── 数据 ───

    private GetBankDetailResDto? _Detail;
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
            var svc = User.Use<IGetBankDetailService>();
            var res = await svc.Execute(new GetBankDetailReqDto { BankId = BankId }, CancellationToken.None);
            if (res.Success)
            {
                _Detail = res;
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

    private static string TruncateContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return "-";
        return content.Length > 100 ? content[..100] + "..." : content;
    }

    #endregion
}
