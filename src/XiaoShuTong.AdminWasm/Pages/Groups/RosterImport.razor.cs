using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Groups;

public partial class RosterImport
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 数据 ───

    private GroupListItemDto[] _Groups = [];
    private long _SelectedGroupId;
    private string _ActiveTab = "paste";
    private string? _RawText;
    private IBrowserFile? _SelectedFile;

    private bool _Loading;
    private bool _Importing;
    private bool _Generating;
    private bool _Generated;
    private long _ImportId;
    private string? _ErrorMessage;

    private RosterPreviewResDto? _PreviewData;

    protected override async Task OnInitializedAsync()
    {
        await LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        _Loading = true;
        try
        {
            var svc = User.Use<IListGroupsService>();
            var res = await svc.Execute(new ListGroupsReqDto { PageIndex = 1, PageSize = 100 }, CancellationToken.None);
            if (res.Success)
            {
                _Groups = res.Items?.ToArray() ?? [];
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

    #region ─── 事件处理 ───

    private void OnTabChange(string activeKey)
    {
        _ActiveTab = activeKey;
    }

    private void OnGroupChanged(object value)
    {
        _PreviewData = null;
        _Generated = false;
        _ImportId = 0;
        _ErrorMessage = null;
    }

    private async Task ImportAsync(string method)
    {
        if (_SelectedGroupId <= 0)
        {
            Message.Warning("请先选择群组");
            return;
        }

        string? rawText = method switch
        {
            "Paste" => _RawText,
            "File" => _SelectedFile != null ? await ReadFileAsync(_SelectedFile) : null,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(rawText))
        {
            Message.Warning("请输入或上传名单内容");
            return;
        }

        _Importing = true;
        _ErrorMessage = null;
        StateHasChanged();
        try
        {
            var svc = User.Use<IImportRosterService>();
            var res = await svc.Execute(new ImportRosterReqDto
            {
                GroupId = _SelectedGroupId,
                Method = method,
                RawText = rawText
            }, CancellationToken.None);

            if (res.Success)
            {
                _ImportId = res.ImportId;
                Message.Success($"导入成功，共 {res.SourceCount} 条记录");
                await LoadPreviewAsync();
            }
            else
            {
                _ErrorMessage = $"导入失败（错误码: {res.ErrorCode}）";
            }
        }
        catch (Exception ex)
        {
            _ErrorMessage = $"导入失败: {ex.Message}";
        }
        finally
        {
            _Importing = false;
            StateHasChanged();
        }
    }

    private async Task LoadPreviewAsync()
    {
        if (_ImportId <= 0) return;

        try
        {
            var svc = User.Use<IGetRosterPreviewService>();
            var res = await svc.Execute(new GetRosterPreviewReqDto
            {
                GroupId = _SelectedGroupId,
                ImportId = _ImportId
            }, CancellationToken.None);

            if (res.Success)
            {
                _PreviewData = res;
            }
            else
            {
                _ErrorMessage = $"预览加载失败（错误码: {res.ErrorCode}）";
            }
        }
        catch (Exception ex)
        {
            _ErrorMessage = $"预览加载失败: {ex.Message}";
        }
        StateHasChanged();
    }

    private async Task OnRefreshPreview()
    {
        await LoadPreviewAsync();
    }

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _SelectedFile = e.File;
        StateHasChanged();
    }

    private async Task<string> ReadFileAsync(IBrowserFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024));
        return await reader.ReadToEndAsync();
    }

    private async Task OnGenerateInviteCodesAsync()
    {
        _Generating = true;
        _ErrorMessage = null;
        StateHasChanged();
        try
        {
            var svc = User.Use<IGenerateInviteCodesService>();
            var res = await svc.Execute(new GenerateInviteCodesReqDto
            {
                GroupId = _SelectedGroupId,
                ImportId = _ImportId,
                Confirm = true
            }, CancellationToken.None);

            if (res.Success)
            {
                _Generated = true;
                Message.Success($"成功生成 {res.GeneratedCount} 个邀请码");
            }
            else
            {
                _ErrorMessage = $"生成失败（错误码: {res.ErrorCode}）";
            }
        }
        catch (Exception ex)
        {
            _ErrorMessage = $"生成失败: {ex.Message}";
        }
        finally
        {
            _Generating = false;
            StateHasChanged();
        }
    }

    #endregion
}
