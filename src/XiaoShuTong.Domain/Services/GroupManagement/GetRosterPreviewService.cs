using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.7：查看名单整理预览
/// </summary>
/// <remarks>
/// BR-18 批次未就绪（Processing）不可生成一次性码 → 5204（预览返回当前状态供轮询，批次不存在 → 5204）
/// BR-19 手机号脱敏展示（前缀 + **** + 后四位）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetRosterPreviewService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private RosterImportsDataService? _importsDs;
    private RosterImportsDataService ImportsDs => _importsDs ??= User.Use<RosterImportsDataService>();

    /// <summary>
    /// 查询名单整理预览（脱敏手机号列表 + 统计）
    /// </summary>
    public async Task<RosterPreviewResDto> ExecuteAsync(GetRosterPreviewReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new RosterPreviewResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        // BR-18：批次不存在 → 5204
        var batch = await ImportsDs.EntityGetAsync(x => x.Id == request.ImportId, ct);
        if (batch == null || batch.GroupId != request.GroupId)
            return new RosterPreviewResDto { Success = false, ErrorCode = GroupErrorCodes.RosterNotReady };

        // Processing 中 → 返回当前状态（前端轮询）；Ready/Exported/Failed → 返回统计 + 脱敏预览
        var preview = batch.Status == RosterImportStatus.Processing
            ? []
            : MaskPhones(ParsePhones(batch.RawPhonesJson));

        return new RosterPreviewResDto
        {
            Success = true,
            ImportId = batch.Id,
            Status = batch.Status.ToString(),
            SourceCount = batch.SourceCount,
            CleanedCount = batch.CleanedCount,
            DuplicateCount = batch.DuplicateCount,
            InvalidCount = batch.InvalidCount,
            Preview = preview,
        };
    }

    /// <summary>
    /// 解析手机号列表（JSON 数组）
    /// </summary>
    private static List<string> ParsePhones(string? rawPhonesJson)
    {
        if (string.IsNullOrWhiteSpace(rawPhonesJson))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(rawPhonesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 手机号脱敏：前缀 3 位 + **** + 后 4 位（BR-19）
    /// </summary>
    internal static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
            return phone;
        return $"{phone[..3]}****{phone[^4..]}";
    }

    /// <summary>
    /// 批量脱敏
    /// </summary>
    private static string[] MaskPhones(List<string> phones)
        => phones.Select(MaskPhone).ToArray();
}

/// <summary>名单整理预览请求 DTO</summary>
public sealed record GetRosterPreviewReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>批次 Id</summary>
    public long ImportId { get; init; }
}

/// <summary>名单整理预览响应 DTO</summary>
public sealed record RosterPreviewResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>批次 Id</summary>
    public long ImportId { get; init; }

    /// <summary>批次状态（Ready/Processing/Failed/Exported）</summary>
    public string? Status { get; init; }

    /// <summary>原始行数</summary>
    public int SourceCount { get; init; }

    /// <summary>整理后有效数</summary>
    public int CleanedCount { get; init; }

    /// <summary>去重剔除数</summary>
    public int DuplicateCount { get; init; }

    /// <summary>非法剔除数</summary>
    public int InvalidCount { get; init; }

    /// <summary>脱敏手机号列表（前缀 + 后四位）</summary>
    public string[] Preview { get; init; } = [];
}
