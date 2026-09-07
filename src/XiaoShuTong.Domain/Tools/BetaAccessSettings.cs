namespace XiaoShuTong.Tools;

/// <summary>
/// 内测开放开关（平台级，BR-05）
/// </summary>
/// <remarks>
/// 切片验证版：静态开关。生产环境应由平台配置中心/数据库驱动（模块 1 依赖）。
/// 测试通过 try/finally 临时切换。
/// </remarks>
public static class BetaAccessSettings
{
    /// <summary>内测是否开放（默认开放）</summary>
    public static bool IsBetaOpen { get; set; } = true;
}
