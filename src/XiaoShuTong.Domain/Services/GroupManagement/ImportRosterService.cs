using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.5：导入成员名单（创建批次 + 触发异步整理）
/// </summary>
/// <remarks>
/// BR-14 名单为空不可提交 → 1002 | BR-15 导入后异步 Agent 整理（去重/格式校验/非法剔除）
/// 切片验证：File 方式由 WebApi 层先抽取文本，Service 统一接收 RawText；整理任务见 UC-6.6。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ImportRosterService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private RosterImportsDataService? _importsDs;
    private RosterImportsDataService ImportsDs => _importsDs ??= User.Use<RosterImportsDataService>();

    /// <summary>
    /// 导入成员名单（Paste/File 文本 → 创建批次 Processing）
    /// </summary>
    public async Task<ImportRosterResDto> ExecuteAsync(ImportRosterReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new ImportRosterResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        // BR-14：名单为空不可提交 → 1002
        var phones = ParsePhoneLines(request.RawText);
        if (phones.Count == 0)
            return new ImportRosterResDto { Success = false, ErrorCode = GroupErrorCodes.ParamInvalid };

        // 创建批次（Processing，统计原始行数）
        var batch = await ImportsDs.EntityCreateAsync(new RosterImports
        {
            UId = UidGenerator.NewId(),
            GroupId = request.GroupId,
            OwnerId = ownerId,
            ImportMethod = request.Method,
            SourceCount = phones.Count,
            Status = RosterImportStatus.Processing,
            RawPhonesJson = JsonSerializer.Serialize(phones),
        }, ct);

        // BR-15：异步 Agent 整理由调度器触发（UC-6.6 RosterCleanupJob）；切片验证时测试直接调用 Job

        return new ImportRosterResDto
        {
            Success = true,
            ImportId = batch.Id,
            Status = batch.Status.ToString(),
            SourceCount = phones.Count,
        };
    }

    /// <summary>
    /// 解析批量粘贴文本为手机号行（每行一个，去除空行/空白）
    /// </summary>
    private static List<string> ParsePhoneLines(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        return rawText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();
    }
}

/// <summary>导入成员名单请求 DTO</summary>
public sealed record ImportRosterReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>导入方式（Paste / File）</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>批量粘贴内容（File 方式由 WebApi 层抽取文本后传入）</summary>
    public string? RawText { get; init; }
}

/// <summary>导入成员名单响应 DTO</summary>
public sealed record ImportRosterResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>批次 Id</summary>
    public long ImportId { get; init; }

    /// <summary>批次状态（Processing）</summary>
    public string? Status { get; init; }

    /// <summary>原始行数</summary>
    public int SourceCount { get; init; }
}
