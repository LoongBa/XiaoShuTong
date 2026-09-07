namespace XiaoShuTong.Services.Rank;

/// <summary>
/// 排行榜域错误码（数字域码 70xx/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class RankErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>范围资源不存在 (1003)</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>该群组战绩榜已关闭 (7001)</summary>
    public const string RankPerformanceDisabled = "RANK_PERFORMANCE_DISABLED";
}