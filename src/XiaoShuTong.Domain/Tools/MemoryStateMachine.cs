namespace XiaoShuTong.Tools;

/// <summary>
/// 四阶记忆状态机（BR-11~21）+ 复习间隔计算（BR-17/18/19）
/// </summary>
/// <remarks>
/// 对齐 D01 §10.2 迁移矩阵 + D02 §4.1/4.2：
/// BR-11 独立答对：✕→△、△→○、○→○（CC+1，满 2 升 ★）；BR-12 ○ 连续 2 次独立答对 → ★
/// BR-13 求助后答对：→ △（CC 清零）；BR-14 独立答错：○→△、★→○、△→✕（CC 清零）
/// BR-15 求助后答错：→ ✕（CC 清零）；BR-20 Assess：答对不升级、答错降级；BR-21 Play：isolated（调用方跳过）
/// </remarks>
public static class MemoryStateMachine
{
    /// <summary>
    /// 状态迁移（Full 提示与 Play 场景由调用方跳过，不入此方法）
    /// </summary>
    public static (MemoryState PostState, int ConsecutiveCorrect) Migrate(
        MemoryState preState,
        int preConsecutiveCorrect,
        JudgmentResult result,
        HintLevel hintLevel,
        LearningScenario scenario)
    {
        var cc = preConsecutiveCorrect;

        // BR-20 Assess（feedback_only）：答对不升级（状态停留），答错走正常降级
        if (scenario == LearningScenario.Assess && result == JudgmentResult.Correct)
            return (preState, cc);

        // BR-13 求助后答对（Partial+Correct）→ Fuzzy
        if (result == JudgmentResult.Correct && hintLevel == HintLevel.Partial)
            return (MemoryState.Fuzzy, 0);

        // 独立答对（None+Correct）
        if (result == JudgmentResult.Correct && hintLevel == HintLevel.None)
        {
            return preState switch
            {
                // BR-11 ✕→△
                MemoryState.NotMastered => (MemoryState.Fuzzy, 1),
                // BR-11 △→○
                MemoryState.Fuzzy => (MemoryState.Mastered, 1),
                // BR-12 ○ CC+1，满 2 → ★
                MemoryState.Mastered when cc + 1 >= 2 => (MemoryState.Proficient, cc + 1),
                // BR-11 ○→○（CC 未满 2）
                MemoryState.Mastered => (MemoryState.Mastered, cc + 1),
                // BR-19 ★ 停留，间隔增大（CC 累计）
                MemoryState.Proficient => (MemoryState.Proficient, cc + 1),
                _ => (preState, cc),
            };
        }

        // BR-15 求助后答错（Partial+Wrong）→ NotMastered，CC 清零
        if (result != JudgmentResult.Correct && hintLevel == HintLevel.Partial)
            return (MemoryState.NotMastered, 0);

        // BR-14 独立答错（None+Wrong）：○→△、★→○、△→✕、✕→✕
        if (result != JudgmentResult.Correct)
        {
            return preState switch
            {
                MemoryState.Mastered => (MemoryState.Fuzzy, 0),
                MemoryState.Proficient => (MemoryState.Mastered, 0),
                MemoryState.Fuzzy => (MemoryState.NotMastered, 0),
                _ => (MemoryState.NotMastered, 0),
            };
        }

        return (preState, cc);
    }

    /// <summary>
    /// 复习间隔：NextReviewAt = 状态基础间隔 × 正确率系数（BR-17/18/19）
    /// 基础间隔：✕30min / △12h / ○3d / ★7d；系数 = clamp(HistoryAccuracy / 0.8, 0.5, 1.5)
    /// ★ 熟练停留：间隔随连续答对递增，封顶 30 天
    /// </summary>
    public static DateTime CalcNextReviewAt(
        MemoryState state,
        double historyAccuracy,
        int consecutiveCorrect,
        DateTime now)
    {
        var baseMinutes = state switch
        {
            MemoryState.NotMastered => 30,
            MemoryState.Fuzzy => 12 * 60,
            MemoryState.Mastered => 3 * 24 * 60,
            MemoryState.Proficient => 7 * 24 * 60,
            _ => 30,
        };

        var factor = Math.Clamp(historyAccuracy / 0.8, 0.5, 1.5);
        var minutes = baseMinutes * factor;

        // BR-19：★ 间隔递增，封顶 30 天
        if (state == MemoryState.Proficient)
        {
            var growth = Math.Max(1, consecutiveCorrect);
            minutes = Math.Min(minutes * growth, 30 * 24 * 60);
        }

        return now.AddMinutes(minutes);
    }
}
