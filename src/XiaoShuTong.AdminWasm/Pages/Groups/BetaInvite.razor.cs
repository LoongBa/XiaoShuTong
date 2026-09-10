using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Groups;

public partial class BetaInvite
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 表单模型 ───

    private class CreateGroupFormModel
    {
        public string BetaCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Grade { get; set; }
    }

    private readonly CreateGroupFormModel _formModel = new();
    private bool _Submitting;
    private bool _SubmitDisabled;
    private string? _ErrorMessage;

    private static readonly string[] _Subjects =
        ["语文", "数学", "英语", "物理", "化学", "生物", "历史", "地理", "政治"];

    #endregion

    #region ─── 提交 ───

    private async Task OnFinishAsync()
    {
        _Submitting = true;
        _ErrorMessage = null;
        StateHasChanged();
        try
        {
            var svc = User.Use<ICreateGroupService>();
            var res = await svc.Execute(new CreateGroupReqDto
            {
                BetaCode = _formModel.BetaCode.Trim(),
                Name = _formModel.Name.Trim(),
                Subject = _formModel.Subject,
                Grade = string.IsNullOrWhiteSpace(_formModel.Grade) ? null : _formModel.Grade.Trim()
            }, CancellationToken.None);

            if (res.Success)
            {
                Message.Success("激活建群成功");
                Navigation.NavigateTo($"/owner/groups/detail/{res.GroupId}");
            }
            else
            {
                _ErrorMessage = GetErrorMessage(res.ErrorCode);
            }
        }
        catch (Exception ex)
        {
            _ErrorMessage = $"激活失败: {ex.Message}";
        }
        finally
        {
            _Submitting = false;
            StateHasChanged();
        }
    }

    private static string GetErrorMessage(string? errorCode) => errorCode switch
    {
        "5201" => "邀请码无效或已使用或已过期",
        "5205" => "内测暂未开放",
        _ => $"激活失败（错误码: {errorCode}）"
    };

    #endregion
}
