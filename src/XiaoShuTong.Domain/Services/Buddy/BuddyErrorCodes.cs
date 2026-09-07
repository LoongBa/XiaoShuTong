namespace XiaoShuTong.Services.Buddy;

/// <summary>
/// 学习搭子域错误码（数字域码 60xx/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class BuddyErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>无权限 (1004)</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>搭子邀请不存在 (6001)</summary>
    public const string BuddyInviteNotFound = "BUDDY_INVITE_NOT_FOUND";

    /// <summary>邀请已过期/已处理 (6002)</summary>
    public const string BuddyInviteExpired = "BUDDY_INVITE_EXPIRED";

    /// <summary>搭子数量已达上限 (6003)</summary>
    public const string BuddyLimitReached = "BUDDY_LIMIT_REACHED";

    /// <summary>非搭子关系 (6004)</summary>
    public const string NotBuddy = "NOT_BUDDY";

    /// <summary>单日邀请超限 (6005)</summary>
    public const string BuddyInviteDailyLimit = "BUDDY_INVITE_DAILY_LIMIT";

    /// <summary>仅限同群组/同年级 (6006)</summary>
    public const string BuddyGroupGradeRequired = "BUDDY_GROUP_GRADE_REQUIRED";
}