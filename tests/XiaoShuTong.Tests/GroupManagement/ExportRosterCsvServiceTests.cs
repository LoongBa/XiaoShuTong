using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.9 导出 CSV 分发（ExportRosterCsvService）Contract 测试
/// 覆盖 BR：BR-07 群组不存在 | BR-08 非 Owner → 5001 | BR-18 批次不存在/不属本群/无码 → 5204
/// BR-24 CSV 仅含后四位+码（不含 openid/完整手机号/学习数据）| BR-25 CSV 存 OSS 7 天过期（未过期复用 / 过期重导）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ExportRosterCsvServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 非当前用户的第三方群主 Id（避免与本文件测试用户 Id 冲突）
    private const long OtherOwnerId = 999921;

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string name)
    {
        var ds = User.Use<GroupsDataService>();
        return await ds.EntityCreateAsync(new Groups
        {
            OwnerId = ownerId,
            Name = name,
            Subject = "history",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<RosterImports> SeedBatchAsync(
        long groupId, long ownerId, string[] phones,
        RosterImportStatus status = RosterImportStatus.Exported,
        string? csvUrl = null, DateTime? csvExpiresAt = null)
    {
        var ds = User.Use<RosterImportsDataService>();
        return await ds.EntityCreateAsync(new RosterImports
        {
            GroupId = groupId,
            OwnerId = ownerId,
            ImportMethod = "Paste",
            SourceCount = phones.Length,
            CleanedCount = phones.Length,
            Status = status,
            RawPhonesJson = System.Text.Json.JsonSerializer.Serialize(phones),
            CsvFileUrl = csvUrl,
            CsvExpiresAt = csvExpiresAt,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedCodeAsync(long groupId, long rosterImportId, string phoneLast4, string code)
    {
        var ds = User.Use<OneTimeInviteCodesDataService>();
        await ds.EntityCreateAsync(new OneTimeInviteCodes
        {
            GroupId = groupId,
            Code = code,
            PhoneLast4 = phoneLast4,
            Status = OneTimeCodeStatus.Unused,
            GeneratedBy = OtherOwnerId,
            RosterImportId = rosterImportId,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-24/25：已生成码批次 → 生成 CSV（后四位+码）并返回 7 天有效链接</summary>
    [Fact]
    public async Task ExecuteAsync_BatchWithCodes_ExportsCsvAndPersistsSevenDayLink()
    {
        var ownerId = SetUser(47101);
        var group = await SeedGroupAsync(ownerId, $"群组{47101}");
        var batch = await SeedBatchAsync(group.Id, ownerId, new[] { "13800138001", "13800138002" });
        await SeedCodeAsync(group.Id, batch.Id, "8001", "ABCD1234");
        await SeedCodeAsync(group.Id, batch.Id, "8002", "EFGH5678");
        var svc = User.Use<ExportRosterCsvService>();

        var before = DateTime.UtcNow;
        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        // BR-24：CSV 列仅后四位+码（不含 openid/完整手机号/学习数据列）
        Assert.True(result.Success);
        Assert.Equal(["phone_last4", "invite_code"], result.Columns);
        Assert.DoesNotContain(result.Columns, c => c.Contains("openid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Columns, c => c.Contains("phone", StringComparison.OrdinalIgnoreCase) && !c.Contains("last4", StringComparison.OrdinalIgnoreCase));

        // BR-24：mock 下载链接按批次拼装，不含名单原始手机号；CsvFileUrl 已落库
        Assert.Equal($"mock://roster-csv/{batch.Id}.csv", result.CsvFileUrl);
        var exportsDs = User.Use<RosterImportsDataService>();
        var updated = await exportsDs.EntityGetAsync(x => x.Id == batch.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(result.CsvFileUrl, updated.CsvFileUrl);

        // BR-25：CSV 7 天过期
        var after = DateTime.UtcNow;
        Assert.NotNull(result.ExpiresAt);
        Assert.InRange(result.ExpiresAt!.Value, before.AddDays(6.9), after.AddDays(7.1));
        Assert.NotNull(updated.CsvExpiresAt);
        Assert.InRange(updated.CsvExpiresAt!.Value, before.AddDays(6.9), after.AddDays(7.1));
    }

    /// <summary>BR-25：已有未过期 CSV → 直接复用已有链接（不再重新拼装/刷新过期时间）</summary>
    [Fact]
    public async Task ExecuteAsync_UnexpiredCsv_ReusesExistingUrl()
    {
        var ownerId = SetUser(47102);
        var group = await SeedGroupAsync(ownerId, $"群组{47102}");
        var existingUrl = "mock://roster-csv/existing.csv";
        var existingExpiry = DateTime.UtcNow.AddDays(1);
        var batch = await SeedBatchAsync(
            group.Id, ownerId, new[] { "13800138003" }, csvUrl: existingUrl, csvExpiresAt: existingExpiry);
        await SeedCodeAsync(group.Id, batch.Id, "8003", "IJKL9012");
        var svc = User.Use<ExportRosterCsvService>();

        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(existingUrl, result.CsvFileUrl);
        Assert.Equal(existingExpiry, result.ExpiresAt);
        Assert.Equal(["phone_last4", "invite_code"], result.Columns);
    }

    /// <summary>BR-25 备选流：CSV 已过期 → 重新导出（生成新链接 + 新 7 天有效期）</summary>
    [Fact]
    public async Task ExecuteAsync_ExpiredCsv_RegeneratesNewLink()
    {
        var ownerId = SetUser(47103);
        var group = await SeedGroupAsync(ownerId, $"群组{47103}");
        var batch = await SeedBatchAsync(
            group.Id, ownerId, new[] { "13800138004" }, csvUrl: "mock://roster-csv/stale.csv", csvExpiresAt: DateTime.UtcNow.AddDays(-1));
        await SeedCodeAsync(group.Id, batch.Id, "8004", "MNOP3456");
        var svc = User.Use<ExportRosterCsvService>();

        var before = DateTime.UtcNow;
        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var after = DateTime.UtcNow;
        Assert.Equal($"mock://roster-csv/{batch.Id}.csv", result.CsvFileUrl);
        Assert.NotNull(result.ExpiresAt);
        Assert.InRange(result.ExpiresAt!.Value, before.AddDays(6.9), after.AddDays(7.1));
    }

    /// <summary>BR-23/BR-18：批次无一次性码（未生成）→ 引导先生成 → 5204</summary>
    [Fact]
    public async Task ExecuteAsync_BatchWithoutCodes_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(47104);
        var group = await SeedGroupAsync(ownerId, $"群组{47104}");
        var batch = await SeedBatchAsync(group.Id, ownerId, new[] { "13800138005" }, status: RosterImportStatus.Ready);
        var svc = User.Use<ExportRosterCsvService>();

        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-18：批次不属于本群组 → 5204</summary>
    [Fact]
    public async Task ExecuteAsync_BatchOfOtherGroup_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(47105);
        var group = await SeedGroupAsync(ownerId, $"群组{47105}");
        var otherGroup = await SeedGroupAsync(OtherOwnerId, "他人群组");
        var otherBatch = await SeedBatchAsync(otherGroup.Id, OtherOwnerId, new[] { "13800138006" });
        await SeedCodeAsync(otherGroup.Id, otherBatch.Id, "8006", "QRST7890");
        var svc = User.Use<ExportRosterCsvService>();

        // 用本群 groupId + 他群批次 id（Service 先校验批次 GroupId == 请求 GroupId）
        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = otherBatch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-18：批次不存在 → 5204</summary>
    [Fact]
    public async Task ExecuteAsync_NonexistentBatch_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(47106);
        var group = await SeedGroupAsync(ownerId, $"群组{47106}");
        var svc = User.Use<ExportRosterCsvService>();

        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = -1,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-07/BR-08：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_NonexistentGroup_ReturnsGroupNotFound()
    {
        SetUser(47107);
        var svc = User.Use<ExportRosterCsvService>();

        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = -1,
            ImportId = -1,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-08：非 Owner 访问他人群组 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsGroupNotFound()
    {
        SetUser(47108);
        var group = await SeedGroupAsync(OtherOwnerId, "他人群组");
        var batch = await SeedBatchAsync(group.Id, OtherOwnerId, new[] { "13800138007" });
        await SeedCodeAsync(group.Id, batch.Id, "8007", "UVWX2345");
        var svc = User.Use<ExportRosterCsvService>();

        var result = await svc.ExecuteAsync(new ExportRosterCsvReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }
}