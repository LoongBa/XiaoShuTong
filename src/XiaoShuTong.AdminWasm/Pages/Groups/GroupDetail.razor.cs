using Microsoft.AspNetCore.Components;
using XiaoShuTong.Services.GroupManagement.Api;
using XiaoShuTong.Api;

namespace XiaoShuTong.AdminWasm.Pages.Groups;

public partial class GroupDetail
{
    #region ─── DI ───

    [Inject] public DomainClientUser User { get; set; } = null!;
    [Inject] public MessageService Message { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region ─── 路由参数 ───

    [Parameter] public long GroupId { get; set; }

    #endregion

    #region ─── 数据 ───

    private GroupDetailData? _GroupDetail;
    private bool _Loading;
    private bool _SavingRank;
    private bool _RankEnabled;

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
            var svc = User.Use<IManageGroupMembersService>();
            var res = await svc.GetMembers(new GetMembersReqDto { GroupId = GroupId }, CancellationToken.None);
            if (res.Success)
            {
                _GroupDetail = new GroupDetailData
                {
                    GroupId = res.GroupId,
                    Members = res.Members?.ToList() ?? []
                };
                _RankEnabled = true;
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

    #region ─── 事件处理 ───

    private async Task OnRankToggleAsync(bool value)
    {
        _SavingRank = true;
        StateHasChanged();
        try
        {
            var svc = User.Use<ISetRankEnabledService>();
            var res = await svc.Execute(new SetRankEnabledReqDto
            {
                GroupId = GroupId,
                RankEnabled = value
            }, CancellationToken.None);

            if (res.Success)
            {
                _RankEnabled = res.RankEnabled;
                Message.Success($"排名已{(value ? "开启" : "关闭")}");
            }
            else
            {
                _RankEnabled = !value;
                Message.Error($"设置失败（错误码: {res.ErrorCode}）");
            }
        }
        catch (Exception ex)
        {
            _RankEnabled = !value;
            Message.Error($"设置失败: {ex.Message}");
        }
        finally
        {
            _SavingRank = false;
            StateHasChanged();
        }
    }

    private async Task OnRemoveMemberAsync(long userId)
    {
        try
        {
            var svc = User.Use<IManageGroupMembersService>();
            var res = await svc.RemoveMember(new RemoveMemberReqDto
            {
                GroupId = GroupId,
                UserId = userId
            }, CancellationToken.None);

            if (res.Success && res.Removed)
            {
                Message.Success("成员已移除");
                await LoadDataAsync();
            }
            else
            {
                Message.Error($"移除失败（错误码: {res.ErrorCode}）");
            }
        }
        catch (Exception ex)
        {
            Message.Error($"移除失败: {ex.Message}");
        }
    }

    #endregion

    #region ─── 内部模型 ───

    private class GroupDetailData
    {
        public long GroupId { get; set; }
        public string? GroupName { get; set; }
        public string? Subject { get; set; }
        public string? Grade { get; set; }
        public List<MemberItemDto> Members { get; set; } = [];
    }

    #endregion
}
