using System.Text.Json;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.6 名单 Agent 整理（RosterCleanupJob）Contract 测试
/// 覆盖 BR：BR-16 全部非法 → Failed | BR-17 处理幂等（重复触发仅处理一次）
/// 主流程：去重 + 11 位 1[3-9] 格式校验 + 非法剔除 → 统计字段正确 + Ready
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class RosterCleanupJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private async Task<RosterImports> SeedBatchAsync(string[] rawPhones)
    {
        var ds = User.Use<RosterImportsDataService>();
        return await ds.EntityCreateAsync(new RosterImports
        {
            GroupId = 1,
            OwnerId = 66001,
            ImportMethod = "Paste",
            SourceCount = rawPhones.Length,
            Status = RosterImportStatus.Processing,
            RawPhonesJson = JsonSerializer.Serialize(rawPhones),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：去重 + 校验 + 非法剔除 → Ready + 统计正确</summary>
    [Fact]
    public async Task ExecuteAsync_DeduplicatesAndValidates_UpdatesCountsAndReady()
    {
        // 原始：2 个重复有效 + 1 个 10 位非法 + 1 个非 1[3-9] 开头非法
        var raw = new[] { "13800138000", "13800138000", "1380013800", "23800138000" };
        var batch = await SeedBatchAsync(raw);
        var job = User.Use<RosterCleanupJob>();

        await job.ExecuteAsync(batch.Id, TestContext.Current.CancellationToken);

        var importsDs = User.Use<RosterImportsDataService>();
        var updated = await importsDs.EntityGetAsync(x => x.Id == batch.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(RosterImportStatus.Ready, updated.Status);
        Assert.Equal(1, updated.CleanedCount);     // 仅 13800138000 有效
        Assert.Equal(1, updated.DuplicateCount);   // 13800138000 重复 1 次
        Assert.Equal(2, updated.InvalidCount);     // 10 位 + 非 1[3-9] 开头
        Assert.NotNull(updated.CompletedAt);

        // 清洗后列表覆盖为有效列表（供 6.7/6.8 使用）
        var cleaned = JsonSerializer.Deserialize<List<string>>(updated.RawPhonesJson!);
        Assert.NotNull(cleaned);
        Assert.Equal(new[] { "13800138000" }, cleaned);
    }

    /// <summary>BR-16：全部非法 → Failed + InvalidCount=全部</summary>
    [Fact]
    public async Task ExecuteAsync_AllInvalid_SetsFailed()
    {
        var raw = new[] { "1234567890", "23800138000", "1380013800a" };
        var batch = await SeedBatchAsync(raw);
        var job = User.Use<RosterCleanupJob>();

        await job.ExecuteAsync(batch.Id, TestContext.Current.CancellationToken);

        var importsDs = User.Use<RosterImportsDataService>();
        var updated = await importsDs.EntityGetAsync(x => x.Id == batch.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(RosterImportStatus.Failed, updated.Status);
        Assert.Equal(0, updated.CleanedCount);
        Assert.Equal(raw.Length, updated.InvalidCount);
    }

    /// <summary>BR-17：处理幂等——二次触发不重复处理</summary>
    [Fact]
    public async Task ExecuteAsync_AlreadyProcessed_IsIdempotent()
    {
        var raw = new[] { "13800138000", "13800138000" };
        var batch = await SeedBatchAsync(raw);
        var job = User.Use<RosterCleanupJob>();

        await job.ExecuteAsync(batch.Id, TestContext.Current.CancellationToken);
        await job.ExecuteAsync(batch.Id, TestContext.Current.CancellationToken); // 二次触发

        var importsDs = User.Use<RosterImportsDataService>();
        var updated = await importsDs.EntityGetAsync(x => x.Id == batch.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(RosterImportStatus.Ready, updated.Status);
        Assert.Equal(1, updated.CleanedCount);   // 未被二次处理影响
        Assert.Equal(1, updated.DuplicateCount);
    }

    /// <summary>手机号校验规则：11 位数字，1[3-9] 开头</summary>
    [Theory]
    [InlineData("13800138000", true)]
    [InlineData("19912345678", true)]
    [InlineData("12345678901", false)]  // 12 开头
    [InlineData("23800138000", false)]  // 2 开头
    [InlineData("1380013800", false)]   // 10 位
    [InlineData("1380013800a", false)]  // 含字母
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPhone_ValidatesFormat(string? phone, bool expected)
    {
        Assert.Equal(expected, RosterCleanupJob.IsValidPhone(phone));
    }
}
