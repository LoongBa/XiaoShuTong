using TKW.Framework.Domain;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.7：资料上传与 AI 预处理（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度：群主上传资料后异步触发（切片验证：测试直接调用）。
/// BR-24 格式（PDF/Word/txt）+ 大小预检 | BR-25 幂等（同批次不重复处理）| BR-26 AI 草稿必须人工校验后才能入库
/// AI 生成背诵点草稿以内存 DraftBackingPointStore 存储（跨模块桩）。
/// </remarks>
internal class BankContentPreprocessJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const long MaxContentBytes = 10 * 1024 * 1024;

    /// <summary>
    /// AI 预处理资料 → 生成背诵点草稿批次（待人工校验，B.8 入库）
    /// </summary>
    public async Task<string?> ExecuteAsync(
        string bankId, string? fileName, string content, CancellationToken ct = default)
    {
        // BR-24：格式预检（PDF/Word/txt）
        if (!string.IsNullOrWhiteSpace(fileName)
            && !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return null;

        // BR-24：大小预检
        var bytes = System.Text.Encoding.UTF8.GetByteCount(content ?? string.Empty);
        if (bytes > MaxContentBytes)
            return null;

        // BR-25：幂等——同一 bankId+内容哈希不重复生成批次
        var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{bankId}|{content}")))[..16];
        var existing = DraftBackingPointStore.Get($"{bankId}-{contentHash}");
        if (existing != null)
            return existing.BatchId;

        // 模拟 AI 预处理：按空行/句号切段 → 每段一个背诵点草稿（题目/答案/知识点/关键词候选）
        var segments = (content ?? string.Empty)
            .Split(['\n', '。', '！', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length >= 4)
            .ToList();

        var batchId = $"draft-{bankId}-{contentHash}";
        var items = segments.Select((seg, i) => new DraftBackingPoint(
            QuestionId: $"D-{bankId}-{i + 1:D3}",
            Stem: seg,
            Answer: seg,
            Subject: "chinese",
            KnowledgePoint: "待校验知识点",
            Keywords: [seg[..Math.Min(seg.Length, 8)]],
            Reviewed: false)).ToList();

        DraftBackingPointStore.Put(new DraftBatch(batchId, bankId, items, DateTime.UtcNow));
        await Task.CompletedTask;
        return batchId;
    }
}
