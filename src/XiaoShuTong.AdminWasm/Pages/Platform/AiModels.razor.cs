using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.Platform.Api;
using XiaoShuTong.Entities.Platform.DTOs.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Platform;

public partial class AiModels
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;

    #endregion

    #region ─── 列表数据 ───

    private AiModelConfigDto[] _Items = [];
    private bool _Loading;
    private int _Total;

    protected override async Task OnInitializedAsync()
    {
        await LoadListAsync();
    }

    private async Task LoadListAsync()
    {
        _Loading = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IAiModelConfigService>();
            var res = await svc.ListModels(new ListAiModelConfigReqDto
            {
                PageIndex = 1,
                PageSize = 100,
            }, CancellationToken.None);
            if (res.Success)
            {
                _Items = res.Items?.OrderBy(x => x.SortOrder).ThenBy(x => x.CreateTime).ToArray() ?? [];
                _Total = res.Total;
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

    #region ─── 新增 ───

    private bool _CreateVisible;
    private CreateAiModelConfigReqDto _CreateForm = new();
    private bool _Creating;

    private void ShowCreateModal()
    {
        _CreateForm = new CreateAiModelConfigReqDto
        {
            Enabled = true,
            SortOrder = 0,
            TimeoutSeconds = 30,
        };
        _CreateVisible = true;
    }

    private void OnCreateCancel()
    {
        _CreateVisible = false;
    }

    private async Task OnCreateOkAsync()
    {
        if (string.IsNullOrWhiteSpace(_CreateForm.Name)
            || string.IsNullOrWhiteSpace(_CreateForm.BaseUrl)
            || string.IsNullOrWhiteSpace(_CreateForm.ApiKey)
            || string.IsNullOrWhiteSpace(_CreateForm.ModelName))
        {
            Message.Warning("请填写必填项");
            return;
        }

        _Creating = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IAiModelConfigService>();
            var res = await svc.CreateModel(_CreateForm, CancellationToken.None);
            if (res.Success)
            {
                Message.Success("新增成功");
                _CreateVisible = false;
                await LoadListAsync();
            }
            else
            {
                Message.Error($"新增失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"新增失败: {ex.Message}");
        }
        finally
        {
            _Creating = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 编辑 ───

    private bool _EditVisible;
    private UpdateAiModelConfigReqDto _EditForm = new();
    private bool _Editing;

    private void ShowEditModal(AiModelConfigDto item)
    {
        _EditForm = new UpdateAiModelConfigReqDto
        {
            UId = item.UId,
            Name = item.Name,
            Provider = item.Provider.ToString(),
            BaseUrl = item.BaseUrl,
            ModelName = item.ModelName,
            Enabled = item.Enabled,
            SortOrder = item.SortOrder,
            TimeoutSeconds = item.TimeoutSeconds,
            Remark = item.Remark,
            ApiKey = null,
        };
        _EditVisible = true;
    }

    private void OnEditCancel()
    {
        _EditVisible = false;
    }

    private async Task OnEditOkAsync()
    {
        _Editing = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<IAiModelConfigService>();
            var res = await svc.UpdateModel(_EditForm, CancellationToken.None);
            if (res.Success)
            {
                Message.Success("更新成功");
                _EditVisible = false;
                await LoadListAsync();
            }
            else
            {
                Message.Error($"更新失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"更新失败: {ex.Message}");
        }
        finally
        {
            _Editing = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ─── 启停 ───

    private async Task OnEnabledChangedAsync(AiModelConfigDto item, bool enabled)
    {
        try
        {
            var svc = User.Use<IAiModelConfigService>();
            var res = await svc.SetEnabled(new SetEnabledReqDto
            {
                UId = item.UId,
                Enabled = enabled,
            }, CancellationToken.None);
            if (res.Success)
            {
                Message.Success(enabled ? "已启用" : "已停用");
                await LoadListAsync();
            }
            else
            {
                Message.Error($"操作失败: {res.ErrorCode}");
                await LoadListAsync();
            }
        }
        catch (Exception ex)
        {
            Message.Error($"操作失败: {ex.Message}");
            await LoadListAsync();
        }
    }

    #endregion

    #region ─── 删除 ───

    private async Task OnDeleteAsync(AiModelConfigDto item)
    {
        try
        {
            var svc = User.Use<IAiModelConfigService>();
            var res = await svc.DeleteModel(new DeleteAiModelConfigReqDto
            {
                UId = item.UId,
            }, CancellationToken.None);
            if (res.Success)
            {
                Message.Success("删除成功");
                await LoadListAsync();
            }
            else
            {
                Message.Error($"删除失败: {res.ErrorCode}");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"删除失败: {ex.Message}");
        }
    }

    #endregion
}
