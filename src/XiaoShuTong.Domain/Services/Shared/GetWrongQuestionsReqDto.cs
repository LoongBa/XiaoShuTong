namespace XiaoShuTong.Services.Shared;

/// <summary>
/// 错题本查询请求（跨域共享：学习域 UC-4.7 与统计域 UC-6.4 契约形状一致）
/// </summary>
/// <remarks>
/// 统一 ReqDto 防两域漂移（ControllerName 已消歧，请求形状统一）。
/// 响应壳保持各域分页口径（Learning: TotalCount/PageIndex/PageSize；Stats: Total）。
/// </remarks>
public sealed record GetWrongQuestionsReqDto
{
    /// <summary>分组过滤（false=待掌握，true=已掌握）</summary>
    public bool Mastered { get; init; }

    /// <summary>学科过滤（可空）</summary>
    public string? Subject { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页数（1~100，默认 20）</summary>
    public int PageSize { get; init; } = 20;
}