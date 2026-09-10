using TKW.Framework.Domain;
using XiaoShuTong.Services.Platform;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.7：资料上传与 AI 预处理（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度：群主上传资料后异步触发（切片验证：测试直接调用）。
/// BR-24 格式（PDF/Word/txt）+ 大小预检 | BR-25 幂等（同批次不重复处理）| BR-26 AI 草稿必须人工校验后才能入库
/// 平台-BR-02/03/04：AI 生成背诵点走统一网关；网关失败/降级 → 按标点切段桩逻辑（保持幂等）。
/// AI 生成背诵点草稿以内存 DraftBackingPointStore 存储（跨模块桩）。
/// </remarks>
internal class BankContentPreprocessJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const long MaxContentBytes = 10 * 1024 * 1024;

    private LlmGateway? _llmGateway;
    private LlmGateway LlmGateway => _llmGateway ??= User.Use<LlmGateway>();

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

        // 平台-BR-02/03：先走统一 AI 网关生成背诵点；失败/降级 → 按标点切段桩逻辑
        var segments = await BuildSegmentsAsync(content ?? string.Empty, ct);

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

    /// <summary>
    /// 生成背诵点段落列表：优先网关返回（按行拆分）；网关降级/无内容 → 按空行/句号切段桩逻辑
    /// </summary>
    private async Task<List<string>> BuildSegmentsAsync(string content, CancellationToken ct)
    {
        var llmResult = await LlmGateway.CompleteAsync(BackingPointSystemPrompt, content, ct);
        if (llmResult.Success && !string.IsNullOrWhiteSpace(llmResult.Content))
        {
            var llmSegments = llmResult.Content
                .Split(['\n', '。', '；', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length >= 4)
                .ToList();
            if (llmSegments.Count > 0)
                return llmSegments;
        }

        // 降级（平台-BR-03/04）：模拟 AI 预处理——按空行/句号切段
        return content
            .Split(['\n', '。', '！', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length >= 4)
            .ToList();
    }

    private const string BackingPointSystemPrompt =
        """
        你是小书童的资料预处理引擎。请将以下学习资料拆分为若干个背诵点，每个背诵点是一个相对独立的知识片段。
        要求：
        1. 每个背诵点单独一行输出，不要编号，不要多余解释；
        2. 背诵点保持原文语义完整，长度不少于 4 个字符；
        3. 若资料为空或无法拆分，只输出一行"无内容"。
        """;
}
