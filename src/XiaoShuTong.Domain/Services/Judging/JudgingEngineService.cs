using System.Text.Json;
using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Services.Platform;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Judging;

/// <summary>
/// UC-J.1：判题引擎（规则 + 关键词 + LLM，Callee 被学习域 SubmitAttemptService 消费）
/// </summary>
/// <remarks>
/// BR-32 required 必中要点未命中 → 强制 partial | BR-33 aliases 组内任一命中即该组命中
/// BR-34 LLM 超时/失败 → 降级本地规则 + 降级标记 | BR-35 阈值 ≥0.85→Correct / 0.5~0.85→Partial / &lt;0.5→Wrong | BR-36 统一五键契约
/// O4 连线（V0.7.8）：pairs 逐对命中判定 + 部分给分——Keywords="[]"（无 KeywordGroup，关键词模型不适用），
/// 标准 pairs 读题目 Content（展示侧镜像含 pairs，判题依据），对齐 Python rule_grader.grade_o4（exact 全对满分 / 部分按命中比例 / 全错 Wrong）。
/// 平台-BR-02/03/04：PreferLlm 且低于阈值 → 先走统一 AI 网关判题；网关失败/降级 → 本地规则 + 降级标记。
/// </remarks>
internal class JudgingEngineService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private LlmGateway? _llmGateway;
    private LlmGateway LlmGateway => _llmGateway ??= User.Use<LlmGateway>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 判题：按关键词组加权命中率判定 + LLM 优先（PreferLlm 且低于阈值）+ 五键契约输出
    /// </summary>
    public async Task<JudgingVerdictDto> JudgeAsync(JudgingRequestDto request, CancellationToken ct = default)
    {
        // O4 连线：pairs 逐对判定（关键词模型不适用——O4 Keywords="[]" → totalWeight=0 恒判 Wrong）
        if (string.Equals(request.QType, "O4", StringComparison.OrdinalIgnoreCase))
            return await JudgeO4Async(request, ct);

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

    /// <summary>
    /// O4 连线：pairs 逐对命中判定 + 部分给分（对齐 Python rule_grader.grade_o4）
    /// 标准 pairs 读题目 Content（Questions.Content 展示侧镜像含 pairs，判题依据所在）。
    /// 学生提交 `{"pairs":{"左":"右"}}`（canonical dict）或 `[{left,right}]` list 或 `左=右;左2=右2` 字符串。
    /// 语义：全对 → Correct + Confidence 1.0；0 对 → Wrong（Confidence 0）；部分 → Partial + Confidence = 命中对数/总对数
    /// </summary>
    private async Task<JudgingVerdictDto> JudgeO4Async(JudgingRequestDto request, CancellationToken ct)
    {
        // 标准 pairs 从题目 Content 镜像解析（判题依据；O4 无 KeywordGroup → Keywords="[]" 不读）
        var stdPairs = await LoadStandardPairsAsync(request.QuestionId, ct);
        if (stdPairs is not { Count: > 0 })
            return O4Verdict("Wrong", 0, [], []);   // 题目无 pairs / 题目不存在 → 对齐空 Keywords 语义（总权重 0 → Wrong）

        var userPairs = ParseUserPairs(request.UserAnswer);
        if (userPairs.Count == 0)
            return O4Verdict("Wrong", 0, [], stdPairs.Keys.ToArray());   // 空提交 → Wrong（对齐其他题型空提交行为）

        var hitLefts = stdPairs.Where(p => userPairs.TryGetValue(p.Key, out var r) && r == p.Value)
            .Select(p => p.Key).ToArray();
        var hitCount = hitLefts.Length;
        var total = stdPairs.Count;

        if (hitCount == total)
            return O4Verdict("Correct", 1d, hitLefts, []);   // 全对满分
        if (hitCount == 0)
            return O4Verdict("Wrong", 0d, [], stdPairs.Keys.ToArray());   // 全错

        // 部分给分：命中对数 / 总对数（对齐 grade_o4 allow_partial）
        var ratio = Math.Round((double)hitCount / total, 2);
        var missing = stdPairs.Keys.Where(k => !hitLefts.Contains(k)).ToArray();
        return O4Verdict("Partial", ratio, hitLefts, missing);
    }

    /// <summary>从题目 Content 镜像解析标准 pairs（left→right 规范化映射；无 pairs 返回 null）</summary>
    private async Task<Dictionary<string, string>?> LoadStandardPairsAsync(string questionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(questionId))
            return null;
        try
        {
            var question = await QuestionsDs.EntityGetAsync(x => x.QuestionId == questionId, ct);
            if (question == null || string.IsNullOrWhiteSpace(question.Content))
                return null;
            return ParsePairsFromContent(question.Content);
        }
        catch (Exception ex) when (ex is JsonException or System.InvalidOperationException)
        {
            return null;   // 非法 Content JSON → 无标准 pairs → Wrong（保守，不臆造）
        }
    }

    /// <summary>解析题目 Content 镜像 → 标准 pairs（`pairs:[{left,right}]` → left→right 规范化映射）</summary>
    private static Dictionary<string, string>? ParsePairsFromContent(string contentJson)
    {
        using var doc = JsonDocument.Parse(contentJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("pairs", out var pairs)
            || pairs.ValueKind != JsonValueKind.Array)
            return null;

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in pairs.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var left = NormPair(item.TryGetProperty("left", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null);
            var right = NormPair(item.TryGetProperty("right", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null);
            if (left.Length == 0 || right.Length == 0)
                continue;
            map[left] = right;
        }
        return map.Count > 0 ? map : null;
    }

    /// <summary>解析学生提交 → left→right 映射（支持 dict / list / 字符串三形态）</summary>
    private static Dictionary<string, string> ParseUserPairs(string userAnswer)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = userAnswer?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return map;

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                // canonical dict：{"pairs":{"左":"右"}} 或直接 {"左":"右"}
                var target = root.TryGetProperty("pairs", out var p) && p.ValueKind == JsonValueKind.Object ? p : root;
                foreach (var prop in target.EnumerateObject())
                {
                    var left = NormPair(prop.Name);
                    var right = NormPair(prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null);
                    if (left.Length > 0 && right.Length > 0)
                        map[left] = right;
                }
                return map;
            }
            if (root.ValueKind == JsonValueKind.Array)
            {
                // list：[{left,right},{left,right}]
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    var left = NormPair(item.TryGetProperty("left", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null);
                    var right = NormPair(item.TryGetProperty("right", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null);
                    if (left.Length > 0 && right.Length > 0)
                        map[left] = right;
                }
                return map;
            }
        }
        catch (JsonException)
        {
            // 落到字符串形态（"左=右;左2=右2"）
        }

        // 字符串形态："左=右;左2=右2"（对齐 Python grade_o4 兜底）
        foreach (var seg in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = seg.IndexOf('=');
            if (eq <= 0 || eq >= seg.Length - 1)
                continue;
            var left = NormPair(seg[..eq]);
            var right = NormPair(seg[(eq + 1)..]);
            if (left.Length > 0 && right.Length > 0)
                map[left] = right;
        }
        return map;
    }

    /// <summary>规范化：去首尾空白 + 全半角统一（对齐 Python _norm）</summary>
    private static string NormPair(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;
        return s.Trim()
            .Replace('（', '(').Replace('）', ')')
            .Replace('，', ',').Replace('；', ';');
    }

    /// <summary>O4 判题五键契约输出（降级标记：O4 规则纯判，不涉及 LLM 降级）</summary>
    private static JudgingVerdictDto O4Verdict(string result, double confidence, string[] matched, string[] missing)
        => new()
        {
            Result = result,
            Confidence = confidence,
            MatchedKeywords = matched,
            MissingKeywords = missing,
            Hint = string.Empty,
            IsDegraded = false,
        };

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
