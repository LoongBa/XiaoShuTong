using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.3：开通试用
/// </summary>
/// <remarks>
/// BR-05 授权链校验 → 8002 | BR-06 已试用/已订阅幂等（UNIQUE）| BR-07 TrialEndAt = +7 天
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class StartTrialService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    /// <summary>
    /// 开通试用（Trialing，TrialEndAt=+7 天）
    /// </summary>
    public async Task<StartTrialResDto> ExecuteAsync(StartTrialReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-05：授权链校验（StudentId 须有关联记录）→ 8002
        var relation = await RelationsDs.EntityGetAsync(
            x => x.ParentId == parentId && x.StudentId == request.StudentId, ct);
        if (relation == null)
            return new StartTrialResDto { Success = false, ErrorCode = ParentErrorCodes.ParentStudentNotAuthorized };

        // BR-06：已试用/已订阅幂等 → 返回原记录
        var existing = await SubscriptionsDs.EntityGetAsync(
            x => x.ParentId == parentId && x.StudentId == request.StudentId, ct);
        if (existing != null)
            return new StartTrialResDto
            {
                Success = true,
                SubscriptionUid = existing.UId,
                Status = existing.Status.ToString(),
                TrialEndAt = existing.TrialEndAt,
            };

        // BR-07：试用 7 天
        var trialEnd = DateTime.UtcNow.AddDays(7);
        var created = await SubscriptionsDs.EntityCreateAsync(new Subscriptions
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = request.StudentId,
            Plan = SubscriptionPlan.Month,
            Status = SubscriptionStatus.Trialing,
            TrialEndAt = trialEnd,
        }, ct);

        return new StartTrialResDto
        {
            Success = true,
            SubscriptionUid = created.UId,
            Status = created.Status.ToString(),
            TrialEndAt = created.TrialEndAt,
        };
    }
}

/// <summary>开通试用请求 DTO</summary>
public sealed record StartTrialReqDto
{
    /// <summary>孩子 Id</summary>
    public long StudentId { get; init; }
}

/// <summary>开通试用响应 DTO</summary>
public sealed record StartTrialResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>订阅外部键</summary>
    public string SubscriptionUid { get; init; } = string.Empty;

    /// <summary>状态（Trialing）</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>试用结束（+7 天）</summary>
    public DateTime? TrialEndAt { get; init; }
}