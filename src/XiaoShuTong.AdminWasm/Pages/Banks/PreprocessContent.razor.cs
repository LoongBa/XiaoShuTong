using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Banks;

public partial class PreprocessContent
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 数据 ───

    private BankListItemDto[] _Banks = [];
    private string? _SelectedBankId;
    private string? _RawText;
    private bool _Processing;
    private PreprocessContentResDto? _Result;
    private string? _SelectedFileName;

    private bool CanSubmit
        => !string.IsNullOrEmpty(_SelectedBankId)
           && (!string.IsNullOrWhiteSpace(_RawText) || _SelectedFileName != null)
           && !_Processing;

    #endregion

    #region ─── 路由参数 ───

    [SupplyParameterFromQuery] public string? BankId { get; set; }

    #endregion

    #region ─── 生命周期 ───

    protected override async Task OnInitializedAsync()
    {
        await LoadBanksAsync();
        if (!string.IsNullOrEmpty(BankId))
        {
            _SelectedBankId = BankId;
        }
    }

    private async Task LoadBanksAsync()
    {
        try
        {
            var svc = User.Use<IListBanksService>();
            var res = await svc.Execute(new ListBanksReqDto { PageIndex = 1, PageSize = 100 }, CancellationToken.None);
            if (res.Success)
            {
                _Banks = res.Items?.ToArray() ?? [];
            }
            else
            {
                Message.Error($"加载题库失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"加载题库失败: {ex.Message}");
        }
    }

    private void OnBankSelectedAsync(string? bankId)
    {
        _SelectedBankId = bankId;
        _Result = null;
    }

    #endregion

    #region ─── 文件上传 ───

    private IBrowserFile? _SelectedFile;

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        _SelectedFile = e.File;
        _SelectedFileName = e.File.Name;
        _Result = null;
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task<string?> ReadFileTextAsync(IBrowserFile file)
    {
        using var stream = new MemoryStream();
        await file.OpenReadStream(10 * 1024 * 1024).CopyToAsync(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    #endregion

    #region ─── 预处理 ───

    private async Task OnPreprocessAsync()
    {
        if (string.IsNullOrEmpty(_SelectedBankId))
        {
            Message.Warning("请选择题库");
            return;
        }

        _Processing = true;
        _Result = null;
        StateHasChanged();

        try
        {
            var content = _RawText;
            if (string.IsNullOrWhiteSpace(content) && _SelectedFile != null)
            {
                content = await ReadFileTextAsync(_SelectedFile);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Message.Warning("请粘贴文本或上传文件");
                return;
            }

            var svc = User.Use<IPreprocessContentService>();
            var res = await svc.Execute(new PreprocessContentReqDto
            {
                BankId = _SelectedBankId,
                FileName = _SelectedFileName,
                Content = content,
            }, CancellationToken.None);

            _Result = res;
            if (res.Success)
            {
                Message.Success($"预处理完成，草稿批次：{res.BatchId}");
            }
            else
            {
                Message.Error($"预处理失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"预处理失败: {ex.Message}");
        }
        finally
        {
            _Processing = false;
            StateHasChanged();
        }
    }

    private void OnResetAsync()
    {
        _RawText = null;
        _SelectedFile = null;
        _SelectedFileName = null;
        _Result = null;
        StateHasChanged();
    }

    #endregion
}
