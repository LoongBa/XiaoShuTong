using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.1：激活建群（群主凭内测邀请码）
/// </summary>
/// <remarks>
/// 事务范围：CROSS（写 Groups + 标记 BetaInviteCodes used），群组创建与码标记同生共死。
/// BR-01 内测码有效（Pending/未过期） | BR-02 一码一群组 | BR-03 名称/学科校验 | BR-04 可建多群 | BR-05 内测开关
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class CreateGroupService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BetaInviteCodesDataService? _betaCodeDs;
    private BetaInviteCodesDataService BetaCodeDs => _betaCodeDs ??= User.Use<BetaInviteCodesDataService>();

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    /// <summary>
    /// 激活建群
    /// </summary>
    public async Task<CreateGroupResDto> ExecuteAsync(CreateGroupReqDto request, CancellationToken ct = default)
    {
        // BR-05：内测未开放（平台开关）→ 5205
        if (!BetaAccessSettings.IsBetaOpen)
            return new CreateGroupResDto { Success = false, ErrorCode = GroupErrorCodes.BetaNotOpen };

        // BR-03：群组名称必填 ≤64；学科必填 → 1002
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 64
            || string.IsNullOrWhiteSpace(request.Subject))
            return new CreateGroupResDto { Success = false, ErrorCode = GroupErrorCodes.ParamInvalid };

        // BR-01/BR-02：校验内测码状态（Pending + 未过期 + 未被使用）
        var code = await BetaCodeDs.EntityGetAsync(
            x => x.Code == request.BetaCode, ct);
        if (code == null
            || code.Status != BetaCodeStatus.Pending
            || code.ExpiresAt <= DateTime.UtcNow)
            return new CreateGroupResDto { Success = false, ErrorCode = GroupErrorCodes.BetaCodeInvalid };

        var ownerId = User.UserInfo?.Id ?? 0;

        // 创建群组（OwnerId=当前群主、BetaCodeId=码 Id）
        var group = await GroupsDs.EntityCreateAsync(new Groups
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            Name = request.Name,
            Subject = request.Subject,
            Grade = request.Grade,
            Status = GroupStatus.Active,
            RankEnabled = true,
            BetaCodeId = code.Id,
        }, ct);

        // 标记内测码 Used + UsedAt + UsedByGroupId
        code.Status = BetaCodeStatus.Used;
        code.UsedAt = DateTime.UtcNow;
        code.UsedByGroupId = group.Id;
        await BetaCodeDs.EntityUpdateAsync(code, ct);

        return new CreateGroupResDto { Success = true, GroupId = group.Id };
    }
}

/// <summary>激活建群请求 DTO</summary>
public sealed record CreateGroupReqDto
{
    /// <summary>内测邀请码（16 位）</summary>
    public string BetaCode { get; init; } = string.Empty;

    /// <summary>群组名称（≤64）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>年级</summary>
    public string? Grade { get; init; }
}

/// <summary>激活建群响应 DTO</summary>
public sealed record CreateGroupResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时，SNAKE_CASE 语义名）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>新群组 Id</summary>
    public long GroupId { get; init; }
}
