namespace XiaoShuTong.Services.Judging;

/// <summary>
/// 判题域错误码（数字域码 90xx/30xx + SNAKE_CASE 语义名双列，见 U01 错误码表）
/// </summary>
public static class JudgingErrorCodes
{
    /// <summary>未登录/令牌失效 (1001)</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>作答关联会话不存在 (3001，跨域引用学习域)</summary>
    public const string SessionNotFound = "SESSION_NOT_FOUND";

    /// <summary>判题服务不可用（已降级）(1006)</summary>
    public const string JudgingServiceDown = "JUDGING_SERVICE_DOWN";

    /// <summary>反馈记录不存在 (9001)</summary>
    public const string FeedbackNotFound = "FEEDBACK_NOT_FOUND";
}
