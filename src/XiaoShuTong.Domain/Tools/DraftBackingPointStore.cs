namespace XiaoShuTong.Tools;

/// <summary>AI 生成背诵点草稿（待人工校验，B.7/B.8）</summary>
public sealed record DraftBackingPoint(
    string QuestionId,
    string Stem,
    string Answer,
    string Subject,
    string KnowledgePoint,
    string[] Keywords,
    bool Reviewed = false);

/// <summary>校验批次（AI 预处理产物，不直接入库）</summary>
public sealed record DraftBatch(
    string BatchId,
    string BankId,
    List<DraftBackingPoint> Items,
    DateTime CreatedAt);

/// <summary>
/// AI 背诵点草稿批次存储（跨模块桩：生产为内容文件/草稿表，切片验证用内存字典）
/// </summary>
/// <remarks>
/// BR-26/29：AI 生成背诵点必须人工校验后才能入库（内容准确性人工把关）。
/// </remarks>
public static class DraftBackingPointStore
{
    private static readonly Dictionary<string, DraftBatch> Batches = new(StringComparer.Ordinal);

    /// <summary>保存批次</summary>
    public static void Put(DraftBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        Batches[batch.BatchId] = batch;
    }

    /// <summary>取批次（不存在返回 null）</summary>
    public static DraftBatch? Get(string batchId)
        => Batches.TryGetValue(batchId, out var batch) ? batch : null;

    /// <summary>清空（测试隔离）</summary>
    public static void Reset() => Batches.Clear();
}
