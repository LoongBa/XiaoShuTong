namespace XiaoShuTong.Tools;

/// <summary>
/// 内容权威 JSON 文件存储（跨模块桩：生产接入 OSS/本地文件系统，切片验证用内存字典）
/// </summary>
/// <remarks>
/// 对齐 DS01 ⑤ 内容权威：Banks/Questions 表为查询索引，内容权威仍在 JsonPath 文件。
/// 判题/答案读取走本存储，不从 Entity 直读（防爬 DRM）。
/// </remarks>
public static class ContentFileStore
{
    private static readonly Dictionary<string, string> Files = new(StringComparer.Ordinal);

    /// <summary>写入内容文件（jsonPath → JSON 内容）</summary>
    public static void Save(string jsonPath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        Files[jsonPath] = content;
    }

    /// <summary>读取内容文件（不存在返回 null）</summary>
    public static string? Read(string jsonPath)
        => Files.TryGetValue(jsonPath, out var content) ? content : null;

    /// <summary>文件是否存在</summary>
    public static bool Exists(string jsonPath) => Files.ContainsKey(jsonPath);

    /// <summary>清空（测试隔离）</summary>
    public static void Reset() => Files.Clear();
}
