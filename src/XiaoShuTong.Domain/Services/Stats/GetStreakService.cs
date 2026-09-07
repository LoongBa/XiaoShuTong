using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Stats;

/// <summary>
/// UC-6.2：查看连续天数
/// </summary>
/// <remarks>
/// BR-05 无记录当前 0 | BR-06 连击 = StatDate 连续（当日/昨日有记录即延续）| BR-07 断更清零、最长保留
/// 日期连续性派生（业务逻辑），Service 层实现（VEntity 设计规格：不建视图）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetStreakService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private DailyStatsDataService? _dailyDs;
    private DailyStatsDataService DailyDs => _dailyDs ??= User.Use<DailyStatsDataService>();

    /// <summary>
    /// 当前/最长连续背诵天数
    /// </summary>
    public async Task<GetStreakResDto> ExecuteAsync(CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // 当前学生全部 StatDate（升序）
        var stats = await DailyDs.EntitySelectAsync(
            x => x.UserId == userId,
            orderBy: q => q.OrderBy(x => x.StatDate),
            ct: ct);
        var dates = stats.Select(s => s.StatDate).Distinct().OrderBy(d => d).ToList();

        // BR-05：无记录 → 当前 0
        if (dates.Count == 0)
            return new GetStreakResDto { Success = true, CurrentStreak = 0, LongestStreak = 0 };

        // BR-06/BR-07：连续性派生（共享 StreakCalculator）
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)); // UTC+8 业务日
        var current = StreakCalculator.CalcCurrentStreak(dates, today);
        var longest = StreakCalculator.CalcLongestStreak(dates);

        return new GetStreakResDto { Success = true, CurrentStreak = current, LongestStreak = longest };
    }
}

/// <summary>连续天数响应 DTO</summary>
public sealed record GetStreakResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>当前连续天数（断更 0）</summary>
    public int CurrentStreak { get; init; }

    /// <summary>历史最长连续</summary>
    public int LongestStreak { get; init; }
}