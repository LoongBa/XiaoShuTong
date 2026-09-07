using System.Text;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.9：导出 CSV 分发（仅后四位 + 码，脱敏合规）
/// </summary>
/// <remarks>
/// BR-24 CSV 仅含后四位+码（不含 openid/完整手机号/学习数据）| BR-25 CSV 存 OSS 7 天过期
/// 切片验证：OSS 上传以 mock 链接代替（生产由 WebApi/OSS 集成实现）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ExportRosterCsvService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const string CsvTtl = "mock://roster-csv";

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private RosterImportsDataService? _importsDs;
    private RosterImportsDataService ImportsDs => _importsDs ??= User.Use<RosterImportsDataService>();

    private OneTimeInviteCodesDataService? _codesDs;
    private OneTimeInviteCodesDataService CodesDs => _codesDs ??= User.Use<OneTimeInviteCodesDataService>();

    /// <summary>
    /// 生成 CSV（phone_last4, invite_code）并返回下载链接（7 天有效）
    /// </summary>
    public async Task<ExportRosterCsvResDto> ExecuteAsync(ExportRosterCsvReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new ExportRosterCsvResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        // BR-18：批次不存在/未属于本群组 → 5204
        var batch = await ImportsDs.EntityGetAsync(x => x.Id == request.ImportId, ct);
        if (batch == null || batch.GroupId != request.GroupId)
            return new ExportRosterCsvResDto { Success = false, ErrorCode = GroupErrorCodes.RosterNotReady };

        // BR-25：已有未过期 CSV → 直接复用；过期则重新导出
        var now = DateTime.UtcNow;
        if (batch.CsvFileUrl != null && batch.CsvExpiresAt is { } expiresAt && expiresAt > now)
        {
            return new ExportRosterCsvResDto
            {
                Success = true,
                CsvFileUrl = batch.CsvFileUrl,
                ExpiresAt = batch.CsvExpiresAt,
                Columns = ["phone_last4", "invite_code"],
            };
        }

        // BR-23：批次无码（未生成一次性码）→ 引导先生成 → 5204
        var codes = await CodesDs.EntitySelectAsync(
            x => x.RosterImportId == request.ImportId, ct: ct);
        if (codes.Count == 0)
            return new ExportRosterCsvResDto { Success = false, ErrorCode = GroupErrorCodes.RosterNotReady };

        // 构建 CSV 内容（BR-24：仅 phone_last4 + invite_code）
        var csv = BuildCsv(codes);

        // 切片验证：OSS 上传以 mock 链接代替；落库 CsvFileUrl/CsvExpiresAt（7 天）
        batch.CsvFileUrl = $"{CsvTtl}/{request.ImportId}.csv";
        batch.CsvExpiresAt = now.AddDays(7);
        await ImportsDs.EntityUpdateAsync(batch, ct);

        return new ExportRosterCsvResDto
        {
            Success = true,
            CsvFileUrl = batch.CsvFileUrl,
            ExpiresAt = batch.CsvExpiresAt,
            Columns = ["phone_last4", "invite_code"],
        };
    }

    /// <summary>
    /// 构建 CSV 内容（header + 行；仅后四位 + 码）
    /// </summary>
    private static string BuildCsv(List<OneTimeInviteCodes> codes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("phone_last4,invite_code");
        foreach (var code in codes)
            sb.AppendLine($"{code.PhoneLast4},{code.Code}");
        return sb.ToString();
    }
}

/// <summary>导出 CSV 请求 DTO</summary>
public sealed record ExportRosterCsvReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>批次 Id</summary>
    public long ImportId { get; init; }
}

/// <summary>导出 CSV 响应 DTO</summary>
public sealed record ExportRosterCsvResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>OSS 下载链接</summary>
    public string? CsvFileUrl { get; init; }

    /// <summary>过期时间</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>CSV 列名</summary>
    public string[] Columns { get; init; } = [];
}
