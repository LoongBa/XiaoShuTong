using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.4：配置群组排名开关（RankEnabled）
/// </summary>
/// <remarks>
/// BR-12 开关默认 true；关闭后战绩榜入口隐藏、榜单不展示 | BR-13 战力榜不受 RankEnabled 影响（模块 7 依赖，切片不校验）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class SetRankEnabledService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 更新群组战绩榜开关（即时生效）
    /// </summary>
    public async Task<SetRankEnabledResDto> ExecuteAsync(SetRankEnabledReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new SetRankEnabledResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        group.RankEnabled = request.RankEnabled;
        await GroupsDs.EntityUpdateAsync(group, ct);

        return new SetRankEnabledResDto
        {
            Success = true,
            GroupId = group.Id,
            RankEnabled = group.RankEnabled,
        };
    }
}

/// <summary>设置排名开关请求 DTO</summary>
public sealed record SetRankEnabledReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>开关值（默认 true）</summary>
    public bool RankEnabled { get; init; } = true;
}

/// <summary>设置排名开关响应 DTO</summary>
public sealed record SetRankEnabledResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>更新后开关值</summary>
    public bool RankEnabled { get; init; }
}
