using System.Text.Json;
using TKW.Framework.Domain;
using XiaoShuTong.Services.Platform;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Judging;

/// <summary>
/// UC-J.1：判题引擎（规则 + 关键词 + LLM，Callee 被学习域 SubmitAttemptService 消费）
/// </summary>
/// <remarks>
/// BR-32 required 必中要点未命中 → 强制 partial | BR-33 aliases 组内任一命中即该组命中
/// BR-34 LLM 超时/失败 → 降级本地规则 + 降级标记 | BR-35 阈值 ≥0.85→Correct / 0.5~0.85→Partial / &lt;0.5→Wrong | BR-36 统一五键契约
/// 平台-BR-02/03/04：PreferLlm 且低于阈值 → 先走统一 AI 网关判题；网关失败/降级 → 本地规则 + 降级标记。
/// </remarks>
internal class JudgingEngineService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private LlmGateway? _llmGateway;
    private LlmGateway LlmGateway => _llmGateway ??= User.Use<LlmGateway>();

    /// <summary>
    /// 判题：按关键词组加权命中率判定 + LLM 优先（PreferLlm 且低于阈值）+ 五键契约输出
    /// </summary>
    public async Task<JudgingVerdictDto> JudgeAsync(JudgingRequestDto request, CancellationToken ct = default)
    {
        // 解析 KeywordGroup[]（调用方从题库域读取的判题依据）
        var groups = ParseKeywordGroups(request.Keywords);

        // BR-33：组内任一别名命中即该组命中
        var matched = groups
            .Where(g => g.Aliases.Any(a => ContainsAny(request.UserAnswer, a)))
            .ToList();
        var matchedSet = matched.Select(g => string.Join("/", g.Aliases)).ToArray();
        var missing = groups.Except(matched)
            .Select(g => string.Join("/", g.Aliases))
            .ToArray();

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

        // 平台-BR-02：PreferLlm 且低于本地阈值 → 先调统一 AI 网关判题
        var isDegraded = request.LlmFailed;
        if (request.PreferLlm && effectiveRatio < 0.6)
        {
            var llmResult = await LlmGateway.CompleteAsync(
                JudgingSystemPrompt,
                $"题目：{request.QuestionId}\n题型：{request.QType}\n参考答案要点：{request.Keywords}\n学生答案：{request.UserAnswer}",
                ct);

            if (llmResult.Success)
            {
                var llmVerdict = ParseLlmVerdict(llmResult.Content);
                if (llmVerdict != null)
                {
                    // 网关判题成功 → 以 LLM 结论为准（五键契约，非降级）
                    return new JudgingVerdictDto
                    {
                        Result = llmVerdict,
                        Confidence = Math.Round(effectiveRatio, 2),
                        MatchedKeywords = matchedSet,
                        MissingKeywords = missing,
                        Hint = string.Empty,
                        IsDegraded = false,
                    };
                }
            }

            // 平台-BR-03：网关失败/降级 → 本地规则 + 降级标记（BR-34）
            isDegraded = true;
        }

        // BR-34：LLM 路径失败 → 降级本地规则 + 标记（保守：不轻易判错）
        if (isDegraded && effectiveRatio >= 0.5)
            result = "Partial";

        // BR-36：五键契约
        return new JudgingVerdictDto
        {
            Result = result,
            Confidence = Math.Round(effectiveRatio, 2),
            MatchedKeywords = matchedSet,
            MissingKeywords = missing,
            Hint = string.Empty,
            IsDegraded = isDegraded,
        };
    }

    private const string JudgingSystemPrompt =
        """
        你是小书童的判题引擎。根据参考答案要点与学生答案，将作答判定为 Correct / Partial / Wrong 三者之一：
        - Correct：学生答案完整命中全部必中要点，语义正确
        - Partial：学生答案命中部分要点或语义接近但不完整
        - Wrong：学生答案未命中关键要点或语义错误
        只输出一个单词（Correct / Partial / Wrong），不要输出任何解释或其他内容。
        """;

    /// <summary>解析网关判题结论（Correct/Partial/Wrong），无法解析返回 null</summary>
    private static string? ParseLlmVerdict(string content)
    {
        if (content.Contains("Correct", StringComparison.OrdinalIgnoreCase))
            return "Correct";
        if (content.Contains("Partial", StringComparison.OrdinalIgnoreCase))
            return "Partial";
        if (content.Contains("Wrong", StringComparison.OrdinalIgnoreCase))
            return "Wrong";
        return null;
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
