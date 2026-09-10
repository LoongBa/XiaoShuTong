using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using XiaoShuTong.Services.Bank.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Banks;

public partial class ImportQuestions
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 路由参数 ───

    [SupplyParameterFromQuery] public string? BankId { get; set; }

    #endregion

    #region ─── 数据 ───

    private BankListItemDto[] _Banks = [];
    private string? _SelectedBankId;
    private string? _RawText;
    private string? _Topic;
    private bool _Importing;
    private IBrowserFile? _SelectedFile;
    private ImportQuestionsResDto? _Result;

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
        }
        catch (Exception ex)
        {
            Message.Error($"加载题库失败: {ex.Message}");
        }
    }

    #endregion

    #region ─── 文件上传 ───

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        _SelectedFile = e.File;
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task<byte[]> ReadFileBytesAsync(IBrowserFile file)
    {
        using var stream = new MemoryStream();
        await file.OpenReadStream(10 * 1024 * 1024).CopyToAsync(stream);
        return stream.ToArray();
    }

    #endregion

    #region ─── 导入 ───

    private async Task OnImportAsync()
    {
        if (string.IsNullOrEmpty(_SelectedBankId))
        {
            Message.Warning("请选择题库");
            return;
        }
        if (string.IsNullOrWhiteSpace(_RawText) && _SelectedFile == null)
        {
            Message.Warning("请输入文本或上传文件");
            return;
        }

        _Importing = true;
        _Result = null;
        StateHasChanged();
        try
        {
            var svc = User.Use<IImportQuestionsService>();
            byte[]? fileBytes = _SelectedFile != null ? await ReadFileBytesAsync(_SelectedFile) : null;
            var res = await svc.Execute(new ImportQuestionsReqDto
            {
                BankId = _SelectedBankId,
                Topic = _Topic,
                File = fileBytes,
                RawText = fileBytes == null ? _RawText : null,
            }, CancellationToken.None);
            _Result = res;

            if (res.Success && res.Failed == 0)
            {
                Message.Success($"导入 {res.Imported} 条，全部成功");
            }
            else if (res.Imported > 0)
            {
                Message.Warning($"导入 {res.Imported} 条，失败 {res.Failed} 条");
            }
            else
            {
                Message.Error($"导入失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"导入失败: {ex.Message}");
        }
        finally
        {
            _Importing = false;
            StateHasChanged();
        }
    }

    #endregion
}
