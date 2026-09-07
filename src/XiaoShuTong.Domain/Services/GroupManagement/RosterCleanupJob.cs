using System.Text.Json;
using TKW.Framework.Domain;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;

namespace XiaoShuTong.Services.GroupManagement;

/// <summary>
/// UC-6.6：名单 Agent 整理（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度规则：名单导入成功后异步触发（切片验证：测试直接调用）。
/// 流程：读取批次原始手机号 → 去重 → 11 位 1[3-9] 格式校验 → 非法剔除 → 更新统计字段 → Ready/Failed。
/// BR-16 全部非法 → Failed | BR-17 幂等（Status != Processing 跳过）
/// </remarks>
internal class RosterCleanupJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private RosterImportsDataService? _importsDs;
    private RosterImportsDataService ImportsDs => _importsDs ??= User.Use<RosterImportsDataService>();

    /// <summary>
    /// 处理单个名单批次（幂等：非 Processing 状态直接跳过）
    /// </summary>
    public async Task ExecuteAsync(long importId, CancellationToken ct = default)
    {
        var batch = await ImportsDs.EntityGetAsync(x => x.Id == importId, ct);

        // BR-17：幂等——批次不存在或已处理（非 Processing）不重复处理
        if (batch == null || batch.Status != RosterImportStatus.Processing)
            return;

        // 读取批次原始手机号列表
        var rawPhones = ParsePhones(batch.RawPhonesJson);
        var totalCount = rawPhones.Count;

        // 去重（保持首次出现顺序）
        var distinctPhones = rawPhones.Distinct(StringComparer.Ordinal).ToList();
        var duplicateCount = totalCount - distinctPhones.Count;

        // 格式校验（11 位数字，1[3-9] 开头）→ 非法剔除
        var validPhones = distinctPhones.Where(IsValidPhone).ToList();
        var invalidCount = distinctPhones.Count - validPhones.Count;

        // 更新批次统计：清洗后列表覆盖 RawPhonesJson，Status=Ready（全部非法 → Failed）
        batch.CleanedCount = validPhones.Count;
        batch.DuplicateCount = duplicateCount;
        batch.InvalidCount = invalidCount;
        batch.RawPhonesJson = JsonSerializer.Serialize(validPhones);
        batch.Status = validPhones.Count > 0 ? RosterImportStatus.Ready : RosterImportStatus.Failed;
        batch.CompletedAt = DateTime.UtcNow;
        await ImportsDs.EntityUpdateAsync(batch, ct);
    }

    /// <summary>
    /// 解析批次原始手机号列表（JSON 数组）
    /// </summary>
    private static List<string> ParsePhones(string? rawPhonesJson)
    {
        if (string.IsNullOrWhiteSpace(rawPhonesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(rawPhonesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 手机号格式校验：11 位数字，1[3-9] 开头
    /// </summary>
    internal static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length != 11 || !phone.All(char.IsDigit))
            return false;
        return phone[0] == '1' && phone[1] is >= '3' and <= '9';
    }
}
