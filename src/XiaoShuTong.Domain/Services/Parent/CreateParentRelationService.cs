using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.1：家长关联孩子（授权链）
/// </summary>
/// <remarks>
/// BR-01 孩子角色须为学生（账户域未实施，切片跳过角色校验）| BR-02 同一 (家长,孩子) 幂等（UNIQUE）
/// StudentUid 参数（uuid）账户域未实施 → 直接 long StudentId。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class CreateParentRelationService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    /// <summary>
    /// 家长关联孩子（幂等）
    /// </summary>
    public async Task<CreateParentRelationResDto> ExecuteAsync(CreateParentRelationReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-02：幂等——已存在关联 → 返回原记录
        var existing = await RelationsDs.EntityGetAsync(
            x => x.ParentId == parentId && x.StudentId == request.StudentId, ct);
        if (existing != null)
            return new CreateParentRelationResDto { Success = true, RelationUid = existing.UId };

        var relation = Enum.TryParse<ParentRelation>(request.Relation, true, out var parsed)
            ? parsed : ParentRelation.Parent;

        var created = await RelationsDs.EntityCreateAsync(new ParentStudentRelations
        {
            UId = UidGenerator.NewId(),
            ParentId = parentId,
            StudentId = request.StudentId,
            Relation = relation,
        }, ct);

        return new CreateParentRelationResDto { Success = true, RelationUid = created.UId };
    }
}

/// <summary>家长关联孩子请求 DTO</summary>
public sealed record CreateParentRelationReqDto
{
    /// <summary>孩子 Id（账户域 Uid→Id 未实施，直接 long）</summary>
    public long StudentId { get; init; }

    /// <summary>关系（Parent/Guardian/Grandparent，默认 Parent）</summary>
    public string? Relation { get; init; }
}

/// <summary>家长关联孩子响应 DTO</summary>
public sealed record CreateParentRelationResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>关联外部键</summary>
    public string RelationUid { get; init; } = string.Empty;
}