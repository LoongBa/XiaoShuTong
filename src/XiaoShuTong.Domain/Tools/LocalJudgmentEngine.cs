namespace XiaoShuTong.Tools;

/// <summary>
/// 本地判题引擎（跨模块桩：判题服务 D01 §9 在切片 03 实施，本切片以关键词命中率代替）
/// </summary>
/// <remarks>
/// BR-10：关键词命中 ≥85% → Correct；60~85% → Partial；&lt;60% → Wrong（生产由 LLM 兜底）。
/// </remarks>
public static class LocalJudgmentEngine
{
    /// <summary>
    /// 按关键词命中率判定结果（Correct/Partial/Wrong）
    /// </summary>
    public static (JudgmentResult Result, double Confidence) Judge(string userAnswer, string[]? answerKeywords)
    {
        if (string.IsNullOrWhiteSpace(userAnswer))
            return (JudgmentResult.Wrong, 0d);

        var keywords = answerKeywords ?? [];
        if (keywords.Length == 0)
            return (JudgmentResult.Wrong, 0d);

        var hits = keywords.Count(k => userAnswer.Contains(k, StringComparison.OrdinalIgnoreCase));
        var ratio = (double)hits / keywords.Length;

        var result = ratio >= 0.85
            ? JudgmentResult.Correct
            : ratio >= 0.60
                ? JudgmentResult.Partial
                : JudgmentResult.Wrong;

        return (result, Math.Round(ratio, 2));
    }
}
