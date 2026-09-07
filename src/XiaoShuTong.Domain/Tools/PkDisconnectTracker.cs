namespace XiaoShuTong.Tools;

/// <summary>
/// PK 断线追踪器（跨模块桩：生产为事件驱动 + Redis 计时，切片验证用内存字典）
/// </summary>
/// <remarks>
/// UC-9.5：检测到断线启动 30s 弃权倒计时；30s 内重连取消（BR-21）；超时判弃权（BR-23）。
/// </remarks>
public static class PkDisconnectTracker
{
    private static readonly Dictionary<string, DateTime> Disconnects = new(StringComparer.Ordinal);

    private static string Key(long matchId, long userId) => $"{matchId}:{userId}";

    /// <summary>记录断线时间</summary>
    public static void Track(long matchId, long userId, DateTime at)
        => Disconnects[Key(matchId, userId)] = at;

    /// <summary>取断线时间（未断线返回 null）</summary>
    public static DateTime? GetDisconnectedAt(long matchId, long userId)
        => Disconnects.TryGetValue(Key(matchId, userId), out var at) ? at : null;

    /// <summary>取消计时（重连，BR-21）</summary>
    public static void Cancel(long matchId, long userId)
        => Disconnects.Remove(Key(matchId, userId));

    /// <summary>清空（测试隔离）</summary>
    public static void Reset() => Disconnects.Clear();
}