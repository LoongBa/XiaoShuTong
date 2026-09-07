namespace XiaoShuTong.Tools;

/// <summary>
/// 连续天数计算器（共享：GetStreakService / RankSnapshotFreezeJob 等）
/// </summary>
public static class StreakCalculator
{
    /// <summary>
    /// 当前连击：当日或昨日有记录即延续，从该日往回数连续天数
    /// </summary>
    public static int CalcCurrentStreak(IReadOnlyCollection<DateOnly> dates, DateOnly today)
    {
        if (dates.Count == 0)
            return 0;

        var sorted = dates.Distinct().OrderBy(d => d).ToList();
        var anchor = sorted.Contains(today) ? today
            : sorted.Contains(today.AddDays(-1)) ? today.AddDays(-1)
            : (DateOnly?)null;
        if (anchor is not { } a)
            return 0;

        var index = sorted.IndexOf(a);
        var current = 1;
        for (var i = index - 1; i >= 0 && sorted[i].DayNumber == sorted[i + 1].DayNumber - 1; i--)
            current++;
        return current;
    }

    /// <summary>
    /// 历史最长连击（全部日期上的最大连续段）
    /// </summary>
    public static int CalcLongestStreak(IReadOnlyCollection<DateOnly> dates)
    {
        var sorted = dates.Distinct().OrderBy(d => d).ToList();
        if (sorted.Count == 0)
            return 0;

        var longest = 1;
        var run = 1;
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].DayNumber == sorted[i - 1].DayNumber + 1)
                run++;
            else
            {
                longest = Math.Max(longest, run);
                run = 1;
            }
        }
        return Math.Max(longest, run);
    }
}