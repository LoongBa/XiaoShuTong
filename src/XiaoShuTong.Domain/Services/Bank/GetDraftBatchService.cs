using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.7：草稿批次查询（AI 预处理产物，供人工校验前查看）
/// </summary>
/// <remarks>
/// BR-31 批次必须存在且属于当前题库（且为 Owner 可见）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetDraftBatchService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    /// <summary>
    /// 查询草稿批次（含逐条背诵点草稿，供人工校验）
    /// </summary>
    public async Task<GetDraftBatchResDto> ExecuteAsync(GetDraftBatchReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-31：批次必须存在
        var batch = DraftBackingPointStore.Get(request.BatchId);
        if (batch == null)
            return new GetDraftBatchResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        // BR-31：批次属于当前题库，且题库为当前用户
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == batch.BankId, ct);
        if (bank == null || bank.OwnerId != ownerId)
            return new GetDraftBatchResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        return new GetDraftBatchResDto
        {
            Success = true,
            BatchId = batch.BatchId,
            BankId = batch.BankId,
            Items = batch.Items.Select(x => new DraftItemDto
            {
                QuestionId = x.QuestionId,
                Stem = x.Stem,
                Answer = x.Answer,
                Subject = x.Subject,
                KnowledgePoint = x.KnowledgePoint,
                Keywords = x.Keywords,
                Reviewed = x.Reviewed,
            }).ToList(),
        };
    }
}

/// <summary>草稿批次查询请求 DTO</summary>
public sealed record GetDraftBatchReqDto
{
    /// <summary>草稿批次 ID</summary>
    public string BatchId { get; init; } = string.Empty;
}

/// <summary>草稿条目 DTO（外层壳 + 内嵌条目）</summary>
public sealed record DraftItemDto
{
    /// <summary>草稿题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>题干</summary>
    public string Stem { get; init; } = string.Empty;

    /// <summary>答案</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>知识点</summary>
    public string KnowledgePoint { get; init; } = string.Empty;

    /// <summary>关键词候选</summary>
    public string[] Keywords { get; init; } = [];

    /// <summary>是否已校验</summary>
    public bool Reviewed { get; init; }
}

/// <summary>草稿批次查询响应 DTO</summary>
public sealed record GetDraftBatchResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>草稿批次 ID</summary>
    public string BatchId { get; init; } = string.Empty;

    /// <summary>所属题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>草稿条目列表</summary>
    public List<DraftItemDto> Items { get; init; } = [];
}
