using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Bank.DTOs;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.2：题库详情（知识点树 + 题目预览）
/// </summary>
/// <remarks>
/// BR-04 题库不存在 → 1501 | BR-05 私域仅 Owner | BR-06 预览题目不含答案（Content 镜像本身无答案）
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetBankDetailService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 题库详情（元数据 + 知识点树 + 篇目预览）
    /// </summary>
    public async Task<GetBankDetailResDto> ExecuteAsync(GetBankDetailReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-04：题库不存在 → 1501
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new GetBankDetailResDto { Success = false, ErrorCode = BankErrorCodes.BankNotFound };

        // BR-05：私域题库仅 Owner 可访问详情
        if (bank.Privacy == BankPrivacy.Private && bank.OwnerId != userId)
            return new GetBankDetailResDto { Success = false, ErrorCode = BankErrorCodes.Forbidden };

        var questions = await QuestionsDs.EntitySelectAsync(
            x => x.BankId == request.BankId && x.Status == QuestionStatus.Active, ct: ct);

        // 知识点树：按 ChapterId 分组（SubTopics = 知识点列表）
        var topics = questions
            .GroupBy(x => x.ChapterId ?? "默认")
            .Select(g => new TopicNodeDto
            {
                ChapterId = g.Key,
                Title = g.Key,
                SubTopics = g.SelectMany(x => x.KnowledgePoints).Distinct().ToArray(),
                QuestionIds = g.Select(x => x.QuestionId).ToArray(),
            })
            .ToList();

        // BR-06：篇目预览（Content 镜像不含答案，防剧透）
        // DTO 最小化：复用自动生成 QuestionsDto（Keywords 已 [DtoFieldIgnore] 不下发；Answer 本实体无——防爬 DRM）
        var previews = questions
            .Take(5)
            .Select(x => x.ToDto())
            .ToList();

        return new GetBankDetailResDto
        {
            Success = true,
            Bank = bank.ToDto(),
            Topics = topics,
            PreviewQuestions = previews,
        };
    }
}

/// <summary>题库详情请求 DTO</summary>
public sealed record GetBankDetailReqDto
{
    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;
}

/// <summary>题库详情响应 DTO（外层壳：Success/ErrorCode + 内嵌实体 Dto）</summary>
public sealed record GetBankDetailResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>题库信息（复用 BanksDto）</summary>
    public BanksDto? Bank { get; init; }

    /// <summary>知识点树</summary>
    public List<TopicNodeDto> Topics { get; init; } = [];

    /// <summary>篇目示例（不含答案）</summary>
    public List<QuestionsDto> PreviewQuestions { get; init; } = [];
}

/// <summary>知识点树节点 DTO</summary>
public sealed record TopicNodeDto
{
    /// <summary>章节/单元</summary>
    public string ChapterId { get; init; } = string.Empty;

    /// <summary>章节标题</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>子知识点</summary>
    public string[] SubTopics { get; init; } = [];

    /// <summary>题目业务键列表</summary>
    public string[] QuestionIds { get; init; } = [];
}
