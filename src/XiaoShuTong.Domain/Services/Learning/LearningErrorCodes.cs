namespace XiaoShuTong.Services.Learning;

/// <summary>
/// 学习 Session 域错误码（数字域码 + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class LearningErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>提交过于频繁 (1005)</summary>
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    /// <summary>内部错误 (1006)</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>题库不存在 (1501)</summary>
    public const string BankNotFound = "BANK_NOT_FOUND";

    /// <summary>题目不存在/不属该题库 (1502)</summary>
    public const string QuestionNotInBank = "QUESTION_NOT_IN_BANK";

    /// <summary>会话不存在 (3001)</summary>
    public const string SessionNotFound = "SESSION_NOT_FOUND";

    /// <summary>答案格式错误 (3002)</summary>
    public const string AnswerFormatInvalid = "ANSWER_FORMAT_INVALID";

    /// <summary>任务不存在 (5101)</summary>
    public const string TaskNotFound = "TASK_NOT_FOUND";

    /// <summary>任务已截止/关闭 (5102)</summary>
    public const string TaskClosed = "TASK_CLOSED";
}
