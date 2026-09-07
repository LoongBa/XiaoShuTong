namespace XiaoShuTong.Services.Parent;

/// <summary>
/// 家长报告订阅域错误码（数字域码 80xx/40xx/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class ParentErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>资源不存在 (1003)</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>无权限 (1004)</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>无统计数据 (4001)</summary>
    public const string NoStatsData = "NO_STATS_DATA";

    /// <summary>未订阅/无权限查看完整报告 (8001)</summary>
    public const string SubscriptionRequired = "SUBSCRIPTION_REQUIRED";

    /// <summary>家长-孩子未建立授权关系 (8002)</summary>
    public const string ParentStudentNotAuthorized = "PARENT_STUDENT_NOT_AUTHORIZED";

    /// <summary>试用已过期 (8003)</summary>
    public const string TrialExpired = "TRIAL_EXPIRED";
}