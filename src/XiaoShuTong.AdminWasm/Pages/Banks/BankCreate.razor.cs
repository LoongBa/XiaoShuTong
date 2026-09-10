using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Banks;

public partial class BankCreate
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 表单数据 ───

    private string _Name = string.Empty;
    private string? _Subject;
    private string? _Purpose;
    private string? _Privacy;
    private bool _Submitting;

    private readonly SubjectOption[] _SubjectOptions =
    [
        new("Chinese", "语文"),
        new("Math", "数学"),
        new("English", "英语"),
        new("Physics", "物理"),
        new("Chemistry", "化学"),
        new("Biology", "生物"),
        new("History", "历史"),
        new("Geography", "地理"),
        new("Politics", "政治"),
    ];

    private readonly PurposeOption[] _PurposeOptions =
    [
        new("Memorize", "背诵"),
        new("Assess", "检验"),
        new("Play", "趣味"),
    ];

    private readonly PrivacyOption[] _PrivacyOptions =
    [
        new("Private", "私有"),
        new("Public", "公开"),
        new("Group", "群组"),
    ];

    #endregion

    #region ─── 提交 ───

    private async Task OnSubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_Name))
        {
            Message.Warning("请输入题库名称");
            return;
        }
        if (string.IsNullOrWhiteSpace(_Subject))
        {
            Message.Warning("请选择学科");
            return;
        }

        _Submitting = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<ICreateBankService>();
            var res = await svc.Execute(new CreateBankReqDto
            {
                Name = _Name,
                Subject = _Subject,
                Purpose = _Purpose,
                Privacy = _Privacy,
            }, CancellationToken.None);

            if (res.Success)
            {
                Message.Success("题库创建成功");
                Navigation.NavigateTo($"/owner/banks/detail/{res.BankId}");
            }
            else
            {
                Message.Error($"创建失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"创建失败: {ex.Message}");
        }
        finally
        {
            _Submitting = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 内部模型 ───

    private sealed record SubjectOption(string Value, string Label);
    private sealed record PurposeOption(string Value, string Label);
    private sealed record PrivacyOption(string Value, string Label);

    #endregion
}
