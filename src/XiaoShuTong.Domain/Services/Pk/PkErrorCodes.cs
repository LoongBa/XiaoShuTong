namespace XiaoShuTong.Services.Pk;

/// <summary>
/// 搭子PK竞技域错误码（数字域码 20xx/15xx/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class PkErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>题库不存在 (1501)</summary>
    public const string BankNotFound = "BANK_NOT_FOUND";

    /// <summary>比赛不存在 (2001)</summary>
    public const string MatchNotFound = "MATCH_NOT_FOUND";

    /// <summary>比赛已结束/不可加入 (2002)</summary>
    public const string MatchClosed = "MATCH_CLOSED";

    /// <summary>参赛人数已满 (2003)</summary>
    public const string MatchFull = "MATCH_FULL";

    /// <summary>非参赛者 (2004)</summary>
    public const string NotParticipant = "NOT_PARTICIPANT";

    /// <summary>仅搭子（好友）之间可以 PK (2005)</summary>
    public const string NotBuddyPk = "NOT_BUDDY_PK";
}