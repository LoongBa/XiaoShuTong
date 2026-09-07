using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.2：查询我的群组列表
/// </summary>
/// <remarks>
/// BR-06 仅返回当前群主的群组 | BR-07 群组不存在 → 1003（本 UC 列表场景）
/// 执行率（ExecutionRate）依赖模块 2（学习Session域），切片暂返回 0。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListGroupsService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    /// <summary>
    /// 按 OwnerId 分页查询群组列表（含成员数聚合）
    /// </summary>
    public async Task<ListGroupsResDto> ExecuteAsync(ListGroupsReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var items = await GroupsDs.EntitySelectAsync(
            x => x.OwnerId == ownerId,
            (pageIndex - 1) * pageSize,
            pageSize,
            q => q.OrderByDescending(x => x.CreateTime),
            ct);

        var totalCount = await GroupsDs.CountAsync(x => x.OwnerId == ownerId, ct);

        var listItems = new List<GroupListItemDto>();
        foreach (var group in items)
        {
            var memberCount = await MembersDs.CountAsync(
                x => x.GroupId == group.Id, ct);
            listItems.Add(new GroupListItemDto
            {
                GroupId = group.Id,
                Name = group.Name,
                Subject = group.Subject,
                Grade = group.Grade,
                MemberCount = (int)memberCount,
                ExecutionRate = 0, // 依赖模块 2（学习Session域），切片暂不实现
                RankEnabled = group.RankEnabled,
            });
        }

        return new ListGroupsResDto
        {
            Success = true,
            Items = listItems,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = (int)totalCount,
        };
    }
}

/// <summary>查询群组列表请求 DTO</summary>
public sealed record ListGroupsReqDto
{
    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页条数（默认 20）</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>查询群组列表响应 DTO</summary>
public sealed record ListGroupsResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>群组卡片列表</summary>
    public List<GroupListItemDto> Items { get; init; } = [];

    /// <summary>页码</summary>
    public int PageIndex { get; init; }

    /// <summary>每页条数</summary>
    public int PageSize { get; init; }

    /// <summary>总记录数</summary>
    public int TotalCount { get; init; }
}

/// <summary>群组卡片项 DTO</summary>
public sealed record GroupListItemDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>群组名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>年级</summary>
    public string? Grade { get; init; }

    /// <summary>成员数</summary>
    public int MemberCount { get; init; }

    /// <summary>任务执行率（依赖模块 2，切片为 0）</summary>
    public int ExecutionRate { get; init; }

    /// <summary>战绩榜开关</summary>
    public bool RankEnabled { get; init; }
}
