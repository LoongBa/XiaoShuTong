using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.8：生成一次性邀请码
/// </summary>
/// <remarks>
/// 事务范围：CROSS（批次置 Exported + 批量生成码），批量生成与批次状态更新须原子。
/// BR-20 每有效手机号一个码（码↔后四位同名单内唯一）| BR-21 码 8 位全局唯一 | BR-22 默认 30 天有效 | BR-23 已生成批次不可重复生成
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class GenerateInviteCodesService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int CodeLength = 8;
    private const int MaxCodeAttempts = 5;

    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private RosterImportsDataService? _importsDs;
    private RosterImportsDataService ImportsDs => _importsDs ??= User.Use<RosterImportsDataService>();

    private OneTimeInviteCodesDataService? _codesDs;
    private OneTimeInviteCodesDataService CodesDs => _codesDs ??= User.Use<OneTimeInviteCodesDataService>();

    /// <summary>
    /// 为批次内每个有效手机号生成 8 位一次性码（码↔后四位绑定），批次置 Exported
    /// </summary>
    public async Task<GenerateInviteCodesResDto> ExecuteAsync(GenerateInviteCodesReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07/BR-08：校验群组存在 + 当前用户为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.Id == request.GroupId, ct);
        if (group == null || group.OwnerId != ownerId)
            return new GenerateInviteCodesResDto { Success = false, ErrorCode = GroupErrorCodes.GroupNotFound };

        // BR-18/BR-23：批次必须就绪（Ready）且属于本群组，已生成（Exported）不可重复生成 → 5204
        var batch = await ImportsDs.EntityGetAsync(x => x.Id == request.ImportId, ct);
        if (batch == null || batch.GroupId != request.GroupId || batch.Status != RosterImportStatus.Ready)
            return new GenerateInviteCodesResDto { Success = false, ErrorCode = GroupErrorCodes.RosterNotReady };

        // BR-20：每个有效手机号一个码（RawPhonesJson 为 Agent 清洗后的有效列表）
        var phones = ParsePhones(batch.RawPhonesJson);
        if (phones.Count == 0)
            return new GenerateInviteCodesResDto { Success = false, ErrorCode = GroupErrorCodes.RosterNotReady };

        var now = DateTime.UtcNow;
        var generatedCount = 0;

        // BR-20：每个有效手机号一个码（RawPhonesJson 为 Agent 清洗后的有效列表）
        // BR-21：码 8 位全局唯一——生成一批码后一次 IN 批量验重（Oracle C2：码随机生成不可预 IN 查），冲突重新生成（最多 MaxCodeAttempts 轮）
        var codesByPhone = new Dictionary<string, string>(phones.Count);
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            // 为尚未分配唯一码的手机号生成候选码
            var pendingPhones = phones.Where(p => !codesByPhone.ContainsKey(p)).ToList();
            if (pendingPhones.Count == 0)
                break;

            var candidates = pendingPhones.ToDictionary(p => p, _ => InviteCodeGenerator.Generate(CodeLength));
            var candidateCodes = candidates.Values.ToArray();

            // 一次 IN 批量验重（含本批已生成候选，防批内重复）
            var existingCodes = await CodesDs.EntitySelectAsync(
                x => candidateCodes.Contains(x.Code), ct: ct);
            // 已占用码（DB）+ 本批已定稿码（跨轮防批内重复）
            var usedCodes = existingCodes.Select(c => c.Code)
                .Concat(codesByPhone.Values)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var phone in pendingPhones)
            {
                var code = candidates[phone];
                if (usedCodes.Contains(code))
                    continue; // 冲突，下一轮重新生成
                codesByPhone[phone] = code;
                usedCodes.Add(code); // 批内防重
            }
        }

        // 仍有手机号未分配到唯一码（MaxCodeAttempts 轮后仍冲突）→ 内部错误
        if (codesByPhone.Count != phones.Count)
            return new GenerateInviteCodesResDto { Success = false, ErrorCode = GroupErrorCodes.InternalError };

        // 批量创建（BR-20/21/22）
        foreach (var phone in phones)
        {
            var last4 = phone.Length >= 4 ? phone[^4..] : phone;
            await CodesDs.EntityCreateAsync(new OneTimeInviteCodes
            {
                UId = UidGenerator.NewId(),
                GroupId = request.GroupId,
                Code = codesByPhone[phone],
                PhoneLast4 = last4,
                Status = OneTimeCodeStatus.Unused,
                GeneratedBy = ownerId,
                RosterImportId = request.ImportId,
                GeneratedAt = now,
                ExpiresAt = now.AddDays(30), // BR-22：默认 30 天有效
            }, ct);
            generatedCount++;
        }

        // 批次置 Exported（BR-23：已生成不可重复生成）
        batch.Status = RosterImportStatus.Exported;
        await ImportsDs.EntityUpdateAsync(batch, ct);

        return new GenerateInviteCodesResDto { Success = true, GeneratedCount = generatedCount };
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
}

/// <summary>生成一次性邀请码请求 DTO</summary>
public sealed record GenerateInviteCodesReqDto
{
    /// <summary>群组 Id</summary>
    public long GroupId { get; init; }

    /// <summary>批次 Id</summary>
    public long ImportId { get; init; }

    /// <summary>确认标识（防误触）</summary>
    public bool Confirm { get; init; }
}

/// <summary>生成一次性邀请码响应 DTO</summary>
public sealed record GenerateInviteCodesResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>生成数量</summary>
    public int GeneratedCount { get; init; }
}
