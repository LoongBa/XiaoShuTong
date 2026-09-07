namespace XiaoShuTong.Services.Stats;

/// <summary>
/// 可视化激励域错误码（数字域码 4001/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class StatsErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>无统计数据 (4001，语义名由 U01 推导，[Proposed]）</summary>
    public const string NoStatsData = "NO_STATS_DATA";
}