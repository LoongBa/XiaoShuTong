namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// 群组管理域错误码（数字域码 + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class GroupErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>群组不存在 (1003 / UC-6.3 起 5001)</summary>
    public const string GroupNotFound = "GROUP_NOT_FOUND";

    /// <summary>重复加入/已在群组 (5002)</summary>
    public const string AlreadyInGroup = "ALREADY_IN_GROUP";

    /// <summary>非群组成员/无权限 (5003)</summary>
    public const string NotGroupMember = "NOT_GROUP_MEMBER";

    /// <summary>内测邀请码无效/已使用/已过期 (5201)</summary>
    public const string BetaCodeInvalid = "BETA_CODE_INVALID";

    /// <summary>一次性邀请码无效/已使用/已过期 (5202)</summary>
    public const string OneTimeCodeInvalid = "ONE_TIME_CODE_INVALID";

    /// <summary>手机号后四位不匹配 (5203)</summary>
    public const string PhoneLast4Mismatch = "PHONE_LAST4_MISMATCH";

    /// <summary>名单批次不存在/未就绪 (5204)</summary>
    public const string RosterNotReady = "ROSTER_NOT_READY";

    /// <summary>内测未开放 (5205)</summary>
    public const string BetaNotOpen = "BETA_NOT_OPEN";

    /// <summary>内部错误 (1006)</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
