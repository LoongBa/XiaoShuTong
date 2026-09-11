using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Bank.DTOs;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.1：题库列表
/// </summary>
/// <remarks>
/// BR-01 空列表正常返回 | BR-02 Subject/分页参数校验 | BR-03 可见性（官方+已加入群组公有；私域仅 Owner）
/// 群组可见性需群组域成员校验（跨模块），切片简化为：Public → 所有人；Private → 仅 Owner；Group → 仅 Owner。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class ListBanksService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 分页查询可见题库列表（学科/用途过滤 + 题量聚合）
    /// </summary>
    public async Task<ListBanksResDto> ExecuteAsync(ListBanksReqDto request, CancellationToken ct = default)
    {
        // BR-02：参数校验
        if (!string.IsNullOrWhiteSpace(request.Subject) && !Enum.TryParse<Subject>(request.Subject, true, out _))
            return new ListBanksResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };
        if (request.PageSize is < 1 or > 100)
            return new ListBanksResDto { Success = false, ErrorCode = BankErrorCodes.ParamInvalid };

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize;
        var userId = User.UserInfo?.Id ?? 0;

        // BR-03：可见性过滤（Public → 所有人；Private → 仅 Owner；Group → 仅 Owner，切片简化）
        System.Linq.Expressions.Expression<Func<Banks, bool>> predicate = x =>
            x.Status == BankStatus.Active
            && (string.IsNullOrWhiteSpace(request.Subject) || x.Subject.ToString() == request.Subject)
            && (string.IsNullOrWhiteSpace(request.Purpose) || x.Purpose.ToString() == request.Purpose)
            && (x.Privacy == BankPrivacy.Public || x.OwnerId == userId);

        var items = await BanksDs.EntitySelectAsync(
            predicate,
            (pageIndex - 1) * pageSize,
            pageSize,
            q => q.OrderByDescending(x => x.CreateTime),
            ct);
        var totalCount = await BanksDs.CountAsync(predicate, ct);

        // 一次 IN 查询替代 foreach N+1：按 BankId 聚合活跃题数
        var bankIds = items.Select(b => b.BankId).ToArray();
        var questionGroups = bankIds.Length == 0
            ? new List<Questions>()
            : await QuestionsDs.EntitySelectAsync(
                x => bankIds.Contains(x.BankId) && x.Status == QuestionStatus.Active, ct: ct);
        var questionCountByBank = questionGroups
            .GroupBy(x => x.BankId)
            .ToDictionary(g => g.Key, g => g.Count());

        var listItems = items.Select(bank => new BankListItemDto
        {
            Bank = bank.ToDto(),
            TopicCount = 0, // 知识点树统计：切片按 ChapterId 去重（B.2 展示），此处简化为 0
            QuestionCount = questionCountByBank.GetValueOrDefault(bank.BankId),
        }).ToList();

        return new ListBanksResDto
        {
            Success = true,
            Items = listItems,
            Total = (int)totalCount,
        };
    }
}

/// <summary>题库列表请求 DTO</summary>
public sealed record ListBanksReqDto
{
    /// <summary>学科筛选</summary>
    public string? Subject { get; init; }

    /// <summary>用途过滤（Memorize/Assess/Play）</summary>
    public string? Purpose { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页数（默认 20）</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>题库列表响应 DTO</summary>
public sealed record ListBanksResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>题库卡片列表</summary>
    public List<BankListItemDto> Items { get; init; } = [];

    /// <summary>总条数</summary>
    public int Total { get; init; }
}

/// <summary>题库卡片项 DTO（复用 BanksDto + 计数壳）</summary>
public sealed record BankListItemDto
{
    /// <summary>题库信息（复用 BanksDto）</summary>
    public BanksDto? Bank { get; init; }

    /// <summary>知识点数（切片简化为 0，B.2 展示完整树）</summary>
    public int TopicCount { get; init; }

    /// <summary>题量</summary>
    public int QuestionCount { get; init; }
}
