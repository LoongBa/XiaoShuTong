namespace XiaoShuTong.Tools;

/// <summary>
/// 业务键 UId 生成器（32 位，对齐框架 IIdGenerator.NewId() 长度约定）
/// </summary>
/// <remarks>
/// 内存 DAC（切片验证）不自动生成 UId，服务创建实体时须显式赋值（DS01 Uid 非空唯一）。
/// </remarks>
public static class UidGenerator
{
    /// <summary>生成 32 位小写 hex 业务键（Guid N 格式）</summary>
    public static string NewId() => Guid.NewGuid().ToString("N");
}
