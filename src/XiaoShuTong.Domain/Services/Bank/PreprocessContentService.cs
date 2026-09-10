using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.7：资料上传与 AI 预处理触发（包装 BankContentPreprocessJob 为可调用接口）
/// </summary>
/// <remarks>
/// BR-11 题库存在且 Owner | BR-24 格式（PDF/Word/txt）+ 大小预检 | BR-25 幂等（同批次不重复处理）| BR-26 AI 草稿必须人工校验后才能入库
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class PreprocessContentService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    /// <summary>
    /// 上传资料 → AI 预处理 → 返回草稿批次 ID（待人工校验，B.8 入库）
    /// </summary>
    public async Task<PreprocessContentResDto> ExecuteAsync(PreprocessContentReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-11：题库存在且为 Owner
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new PreprocessContentResDto { Success = false, ErrorCode = BankErrorCodes.BankNotFound };
        if (bank.OwnerId != ownerId)
            return new PreprocessContentResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        // BR-24/25/26：格式/大小预检 + 幂等 + 草稿生成（Job 内实现），返回批次 ID
        var batchId = await new BankContentPreprocessJob(User)
            .ExecuteAsync(request.BankId, request.FileName, request.Content, ct);

        if (batchId == null)
            return new PreprocessContentResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        return new PreprocessContentResDto { Success = true, BatchId = batchId };
    }
}

/// <summary>预处理触发请求 DTO</summary>
public sealed record PreprocessContentReqDto
{
    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>文件名（用于格式预检，PDF/Word/txt）</summary>
    public string? FileName { get; init; }

    /// <summary>资料文本内容（≤10M）</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>预处理触发响应 DTO</summary>
public sealed record PreprocessContentResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>草稿批次 ID（供人工校验）</summary>
    public string? BatchId { get; init; }
}
