namespace XiaoShuTong.Services.Platform;

/// <summary>
/// 平台运营域错误码（SNAKE_CASE 语义名，见 M04 错误码表）
/// </summary>
public static class PlatformErrorCodes
{
    /// <summary>未登录/令牌失效</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>参数错误</summary>
    public const string ParamInvalid = "PARAM_INVALID";

    /// <summary>无权限（非平台运营）</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>模型配置不存在</summary>
    public const string ModelNotFound = "MODEL_NOT_FOUND";
}
