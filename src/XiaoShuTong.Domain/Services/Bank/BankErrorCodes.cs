namespace XiaoShuTong.Services.Bank;

/// <summary>
/// 题库域错误码（数字域码 15xx/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class BankErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>无权限（私域非 Owner）(1004)</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>题库不存在 (1501)</summary>
    public const string BankNotFound = "BANK_NOT_FOUND";

    /// <summary>题目不存在/不属该题库 (1502)</summary>
    public const string QuestionNotInBank = "QUESTION_NOT_IN_BANK";

    /// <summary>内部错误（内容文件写入失败等）(1006)</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
