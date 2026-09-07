using System.Text.Json;
using TKW.Framework.Domain;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Judging;

/// <summary>
/// UC-J.1：判题引擎（规则 + 关键词 + LLM，Callee 被学习域 SubmitAttemptService 消费）
/// </summary>
/// <remarks>
/// BR-32 required 必中要点未命中 → 强制 partial | BR-33 aliases 组内任一命中即该组命中
/// BR-34 LLM 超时/失败 → 降级本地规则 + 降级标记 | BR-35 阈值 ≥0.85→Correct / 0.5~0.85→Partial / &lt;0.5→Wrong | BR-36 统一五键契约
/// 生产：LLM 供应商可插拔（Prompt 矩阵五层）；切片验证：LLM 路径以本地规则降级实现。
/// </summary>
internal class JudgingEngineService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    /// <summary>
    /// 判题：按关键词组加权命中率判定 + 五键契约输出
    /// </summary>
    public Task<JudgingVerdictDto> JudgeAsync(JudgingRequestDto request, CancellationToken ct = default)
    {
        // 解析 KeywordGroup[]（调用方从题库域读取的判题依据）
        var groups = ParseKeywordGroups(request.Keywords);

        // BR-33：组内任一别名命中即该组命中
        var matched = groups
            .Where(g => g.Aliases.Any(a => ContainsAny(request.UserAnswer, a)))
            .ToList();
        var matchedSet = matched.Select(g => string.Join("/", g.Aliases)).ToArray();

        // 加权命中率
        var totalWeight = groups.Sum(g => g.Weight);
        var hitWeight = matched.Sum(g => g.Weight);
        var ratio = totalWeight <= 0 ? 0d : hitWeight / totalWeight;

        // BR-32：required 必中要点未命中 → 强制 partial
        var requiredMissed = groups.Any(g => g.Required && !matched.Contains(g));
        var effectiveRatio = requiredMissed ? Math.Min(ratio, 0.84) : ratio;

        // BR-35：阈值判定
        var result = effectiveRatio >= 0.85 ? "Correct"
            : effectiveRatio >= 0.5 ? "Partial"
            : "Wrong";

        // BR-34：LLM 路径（低于本地阈值或调用方标记 LLM 失败）→ 降级本地规则 + 标记
        var isDegraded = request.LlmFailed
            || (request.PreferLlm && effectiveRatio < 0.6);
        if (isDegraded && effectiveRatio >= 0.5)
            result = "Partial"; // 降级保守：不轻易判错

        var missing = groups.Except(matched)
            .Select(g => string.Join("/", g.Aliases))
            .ToArray();

        // BR-36：五键契约
        return Task.FromResult(new JudgingVerdictDto
        {
            Result = result,
            Confidence = Math.Round(effectiveRatio, 2),
            MatchedKeywords = matchedSet,
            MissingKeywords = missing,
            Hint = string.Empty,
            IsDegraded = isDegraded,
        });
    }

    private static List<KeywordGroup> ParseKeywordGroups(string? keywordsJson)
    {
        if (string.IsNullOrWhiteSpace(keywordsJson))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<KeywordGroup>>(keywordsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool ContainsAny(string userAnswer, string alias)
        => !string.IsNullOrWhiteSpace(alias) && userAnswer.Contains(alias, StringComparison.OrdinalIgnoreCase);
}

/// <summary>判题请求 DTO</summary>
public sealed record JudgingRequestDto
{
    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>题型（决定判题链）</summary>
    public string QType { get; init; } = string.Empty;

    /// <summary>KeywordGroup[]（判题依据，题库域读取）</summary>
    public string Keywords { get; init; } = "[]";

    /// <summary>用户答案</summary>
    public string UserAnswer { get; init; } = string.Empty;

    /// <summary>求助档位（None/Partial/Full）</summary>
    public string HintLevel { get; init; } = "None";

    /// <summary>LLM 判题失败（降级本地规则）</summary>
    public bool LlmFailed { get; init; }

    /// <summary>优先走 LLM（低于阈值时）</summary>
    public bool PreferLlm { get; init; }
}

/// <summary>判题响应 DTO（五键契约 + 降级标记）</summary>
public sealed record JudgingVerdictDto
{
    /// <summary>判题结果（Correct/Partial/Wrong）</summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>置信度（0-1）</summary>
    public double Confidence { get; init; }

    /// <summary>命中关键词（组）</summary>
    public string[] MatchedKeywords { get; init; } = [];

    /// <summary>缺失关键词（组）</summary>
    public string[] MissingKeywords { get; init; } = [];

    /// <summary>引导线索（≤20 字，严禁答案）</summary>
    public string Hint { get; init; } = string.Empty;

    /// <summary>是否降级（LLM 失败 → 本地规则）</summary>
    public bool IsDegraded { get; init; }
}
