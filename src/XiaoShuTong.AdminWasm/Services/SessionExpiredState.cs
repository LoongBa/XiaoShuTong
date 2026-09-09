namespace XiaoShuTong.AdminWasm.Services;

/// <summary>
/// 跟踪会话过期状态，用于区分"会话过期"和"从未登录"两种场景。
/// OnAuthRequired 触发时设为 true，NotAuthorized 检查后重置为 false。
/// </summary>
public class SessionExpiredState
{
    /// <summary>是否刚因会话过期而触发</summary>
    public bool IsExpired { get; set; }
}
