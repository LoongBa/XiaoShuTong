namespace XiaoShuTong.Services.Shared;

/// <summary>
/// 薄弱知识点（跨域共享：群主看板/学习报告/家长端均消费）
/// </summary>
/// <remarks>
/// 统一形状 (KnowledgePoint, Accuracy)——TaskMgmt 看板与 Stats 学习报告的薄弱点口径一致。
/// 家长端 GetWeaknessReport 另有 Subject/StateText 变体，属不同契约形状，不并入本共享类型。
/// </remarks>
public sealed record WeakPointDto
{
    /// <summary>知识点</summary>
    public string KnowledgePoint { get; init; } = string.Empty;

    /// <summary>聚合正确率</summary>
    public double Accuracy { get; init; }
}