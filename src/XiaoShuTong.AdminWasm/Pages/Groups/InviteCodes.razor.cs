using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Groups;

public partial class InviteCodes
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public IJSRuntime JS { get; set; } = null!;

    #endregion

    #region ─── 数据 ───

    private GroupListItemDto[] _Groups = [];
    private long _SelectedGroupId;
    private long _ImportId;
    private RosterPreviewResDto? _PreviewData;

    private bool _Generating;
    private bool _Exporting;
    private string? _ErrorMessage;
    private string? _CsvFileUrl;
    private DateTime? _ExpiresAt;

    protected override async Task OnInitializedAsync()
    {
        await LoadGroupsAsync();

        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = uri.Query.TrimStart('?');
        var importIdStr = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Split('=', 2))
            .Where(p => p.Length == 2 && p[0] == "importId")
            .Select(p => Uri.UnescapeDataString(p[1]))
            .FirstOrDefault();
        if (long.TryParse(importIdStr, out var importId))
        {
            _ImportId = importId;
            if (_Groups.Length > 0)
            {
                _SelectedGroupId = _Groups[0].GroupId;
                await LoadPreviewAsync();
            }
        }
    }

    private async Task LoadGroupsAsync()
    {
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
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 事件处理 ───

    private async Task OnGroupChanged(object value)
    {
        _ImportId = 0;
        _PreviewData = null;
        _CsvFileUrl = null;
        _ErrorMessage = null;
    }

    private async Task LoadPreviewAsync()
    {
        if (_ImportId <= 0 || _SelectedGroupId <= 0) return;

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

    private async Task OnGenerateAsync()
    {
        if (_ImportId <= 0)
        {
            Message.Warning("请先通过名单导入页面创建批次");
            return;
        }

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
                Message.Success($"成功生成 {res.GeneratedCount} 个邀请码");
                await LoadPreviewAsync();
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

    private async Task OnExportAsync()
    {
        if (_ImportId <= 0)
        {
            Message.Warning("请先创建批次");
            return;
        }

        _Exporting = true;
        _ErrorMessage = null;
        StateHasChanged();
        try
        {
            var svc = User.Use<IExportRosterCsvService>();
            var res = await svc.Execute(new ExportRosterCsvReqDto
            {
                GroupId = _SelectedGroupId,
                ImportId = _ImportId
            }, CancellationToken.None);

            if (res.Success)
            {
                _CsvFileUrl = res.CsvFileUrl;
                _ExpiresAt = res.ExpiresAt;
                Message.Success("CSV 导出成功");
            }
            else
            {
                _ErrorMessage = $"导出失败（错误码: {res.ErrorCode}）";
            }
        }
        catch (Exception ex)
        {
            _ErrorMessage = $"导出失败: {ex.Message}";
        }
        finally
        {
            _Exporting = false;
            StateHasChanged();
        }
    }

    private async Task CopyTemplateAsync()
    {
        var template = "【小书童内测】@手机号后四位 XXXX，你已被邀请加入群组，请用一次性邀请码 XXXX-XXXX 激活（7天内有效），激活即绑定微信。";
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", template);
        Message.Success("话术已复制到剪贴板");
    }

    #endregion
}
