namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// 家校任务闭环域错误码（数字域码 51xx/15xx/10xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class TaskErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误 (1002)</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>无权限 (1004)</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>群组不存在 (5001)</summary>
    public const string GroupNotFound = "GROUP_NOT_FOUND";

    /// <summary>任务不存在 (5101)</summary>
    public const string TaskNotFound = "TASK_NOT_FOUND";

    /// <summary>任务已截止/关闭 (5102)</summary>
    public const string TaskClosed = "TASK_CLOSED";

    /// <summary>题库不存在 (1501)</summary>
    public const string BankNotFound = "BANK_NOT_FOUND";

    /// <summary>题目不属于题库 (1502)</summary>
    public const string QuestionNotInBank = "QUESTION_NOT_IN_BANK";
}