using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;

namespace XiaoShuTong.Services.Parent;

/// <summary>
/// UC-10.9：薄弱知识点
/// </summary>
/// <remarks>
/// BR-25 未订阅 → 8001 | BR-26 数据未聚合时按需触发（KnowledgeMasteryAggregationJob 跨模块）| BR-27 State 映射家长端文案
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetWeaknessReportService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private ParentStudentRelationsDataService? _relationsDs;
    private ParentStudentRelationsDataService RelationsDs => _relationsDs ??= User.Use<ParentStudentRelationsDataService>();

    private SubscriptionsDataService? _subscriptionsDs;
    private SubscriptionsDataService SubscriptionsDs => _subscriptionsDs ??= User.Use<SubscriptionsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    /// <summary>
    /// 薄弱知识点矩阵（掌握度正确率升序 + 状态映射）
    /// </summary>
    public async Task<GetWeaknessReportResDto> ExecuteAsync(GetWeaknessReportReqDto request, CancellationToken ct = default)
    {
        var parentId = User.UserInfo?.Id ?? 0;

        // BR-25：订阅门控（无预览）
        var gate = await ParentReportGate.CheckAsync(RelationsDs, SubscriptionsDs, parentId, request.StudentId, allowPreview: false, ct);
        if (!gate.Allowed)
            return new GetWeaknessReportResDto { Success = false, ErrorCode = gate.ErrorCode };

        // BR-26：数据未聚合时按需触发掌握度聚合（跨模块 Job）
        var rows = await MasteryDs.EntitySelectAsync(
            x => x.UserId == request.StudentId, ct: ct);
        if (rows.Count == 0)
        {
            var job = User.Use<KnowledgeMasteryAggregationJob>();
            await job.ExecuteAsync(ct);
            rows = await MasteryDs.EntitySelectAsync(
                x => x.UserId == request.StudentId, ct: ct);
            if (rows.Count == 0)
                return new GetWeaknessReportResDto { Success = false, ErrorCode = ParentErrorCodes.NoStatsData };
        }

        // BR-27：State 映射家长端文案（0=✕未掌握/1=△模糊/2=○掌握/3=★熟练）
        var weakPoints = rows
            .OrderBy(r => r.Accuracy)
            .Select(r => new ParentWeakPointDto
            {
                Subject = r.Subject,
                KnowledgePoint = r.KnowledgePoint,
                Accuracy = r.Accuracy,
                StateText = MapStateText(r.State),
            })
            .ToList();

        return new GetWeaknessReportResDto { Success = true, WeakPoints = weakPoints };
    }

    private static string MapStateText(MemoryState state)
        => state switch
        {
            MemoryState.NotMastered => "未掌握",
            MemoryState.Fuzzy => "模糊",
            MemoryState.Mastered => "掌握",
            MemoryState.Proficient => "熟练",
            _ => "未掌握",
        };
}

/// <summary>薄弱知识点请求 DTO</summary>
public sealed record GetWeaknessReportReqDto
{
    /// <summary>孩子 Id</summary>
    public long StudentId { get; init; }
}

/// <summary>薄弱知识点响应 DTO</summary>
public sealed record GetWeaknessReportResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>薄弱知识点列表（正确率升序）</summary>
    public List<ParentWeakPointDto> WeakPoints { get; init; } = [];
}

/// <summary>薄弱点 DTO（Parent 版——重命名消 GraphQL 跨域同名冲突；含学科/状态文案，与共享版形状不同）</summary>
public sealed record ParentWeakPointDto
{
    /// <summary>学科</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>知识点</summary>
    public string KnowledgePoint { get; init; } = string.Empty;

    /// <summary>聚合正确率</summary>
    public double Accuracy { get; init; }

    /// <summary>状态家长端文案（未掌握/模糊/掌握/熟练）</summary>
    public string StateText { get; init; } = string.Empty;
}