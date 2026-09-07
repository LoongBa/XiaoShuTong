using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.3：创建题库（群主）
/// </summary>
/// <remarks>
/// BR-07 名称必填 ≤128、学科必填合法 | BR-08 Privacy=Group 必须指定群组（跨模块校验切片简化）| BR-09 可建多库
/// 官方题库只读（BR-10）本切片不涉及（无官方题库写入入口）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class CreateBankService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    /// <summary>
    /// 创建题库（OwnerId=当前群主，默认私有）
    /// </summary>
    public async Task<CreateBankResDto> ExecuteAsync(CreateBankReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-07：名称必填 ≤128；学科必填合法
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 128
            || string.IsNullOrWhiteSpace(request.Subject)
            || !Enum.TryParse<Subject>(request.Subject, true, out var subject))
            return new CreateBankResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        // BR-08：Privacy=Group 必须指定群组（群主在组校验跨模块，切片校验非空）
        var privacy = string.IsNullOrWhiteSpace(request.Privacy)
            ? BankPrivacy.Private
            : !Enum.TryParse<BankPrivacy>(request.Privacy, true, out var parsedPrivacy)
                ? BankPrivacy.Private
                : parsedPrivacy;
        if (privacy == BankPrivacy.Group && (request.GroupIds == null || request.GroupIds.Length == 0))
            return new CreateBankResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        // 生成业务键（BankId）+ 内容权威路径
        var bankId = $"{subject.ToString().ToLowerInvariant()}-{ownerId}-{Guid.NewGuid():N}"[..Math.Min(48, 64)];
        var jsonPath = $"bank.{bankId}.json";

        var bank = await BanksDs.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = request.Name,
            Subject = subject,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? BankPurpose.Memorize : Enum.Parse<BankPurpose>(request.Purpose, true),
            Privacy = privacy,
            OwnerId = ownerId,
            JsonPath = jsonPath,
            Tags = [],
            Status = BankStatus.Active,
        }, ct);

        return new CreateBankResDto { Success = true, BankUid = bank.UId, BankId = bank.BankId };
    }
}

/// <summary>创建题库请求 DTO</summary>
public sealed record CreateBankReqDto
{
    /// <summary>题库名（≤128）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>用途（默认 Memorize）</summary>
    public string? Purpose { get; init; }

    /// <summary>隐私（默认 Private）</summary>
    public string? Privacy { get; init; }

    /// <summary>群组 ID 列表（Privacy=Group 时必传）</summary>
    public string[]? GroupIds { get; init; }
}

/// <summary>创建题库响应 DTO</summary>
public sealed record CreateBankResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>题库外部键</summary>
    public string BankUid { get; init; } = string.Empty;

    /// <summary>题库业务键（系统生成）</summary>
    public string BankId { get; init; } = string.Empty;
}
