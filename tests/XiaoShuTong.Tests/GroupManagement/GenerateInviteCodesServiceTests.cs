using System.Text.Json;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.8 生成一次性邀请码（GenerateInviteCodesService）Contract 测试
/// 覆盖 BR：BR-18 批次未就绪 → 5204 | BR-20 每有效手机号一码 | BR-21 码 8 位全局唯一 | BR-22 默认 30 天有效 | BR-23 已生成不可重复
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GenerateInviteCodesServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
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
        long groupId, long ownerId, RosterImportStatus status, string[] cleanedPhones)
    {
        var ds = User.Use<RosterImportsDataService>();
        return await ds.EntityCreateAsync(new RosterImports
        {
            GroupId = groupId,
            OwnerId = ownerId,
            ImportMethod = "Paste",
            SourceCount = cleanedPhones.Length,
            CleanedCount = status == RosterImportStatus.Ready ? cleanedPhones.Length : 0,
            Status = status,
            RawPhonesJson = JsonSerializer.Serialize(cleanedPhones),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-20/21/22：Ready 批次 → 每有效手机号生成一个 8 位码，30 天有效</summary>
    [Fact]
    public async Task ExecuteAsync_ReadyBatch_GeneratesOneCodePerPhoneWith30DayExpiry()
    {
        var ownerId = SetUser(68001);
        var group = await SeedGroupAsync(ownerId, $"群组{68001}");
        var phones = new[] { "13800138001", "13800138002", "13800138003" };
        var batch = await SeedBatchAsync(group.Id, ownerId, RosterImportStatus.Ready, phones);
        var svc = User.Use<GenerateInviteCodesService>();

        var before = DateTime.UtcNow;
        var result = await svc.ExecuteAsync(new GenerateInviteCodesReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
            Confirm = true,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(phones.Length, result.GeneratedCount);

        // 每手机号一个码，后四位绑定（BR-20）
        var codesDs = User.Use<OneTimeInviteCodesDataService>();
        var codes = await codesDs.EntitySelectAsync(
            x => x.RosterImportId == batch.Id, ct: TestContext.Current.CancellationToken);
        Assert.Equal(phones.Length, codes.Count);
        Assert.Equal(phones.Length, codes.Select(c => c.PhoneLast4).Distinct().Count());
        Assert.Contains(codes, c => c.PhoneLast4 == "8001");
        Assert.Contains(codes, c => c.PhoneLast4 == "8002");
        Assert.Contains(codes, c => c.PhoneLast4 == "8003");

        // 码 8 位 + 全局唯一（BR-21）
        Assert.All(codes, c => Assert.Equal(8, c.Code.Length));
        Assert.Equal(codes.Count, codes.Select(c => c.Code).Distinct().Count());

        // 默认 30 天有效（BR-22）
        Assert.All(codes, c => Assert.NotNull(c.ExpiresAt));
        var after = DateTime.UtcNow;
        Assert.All(codes, c =>
        {
            Assert.InRange(c.ExpiresAt!.Value, before.AddDays(29.9), after.AddDays(30.1));
        });

        // 批次已置 Exported（BR-23）
        var importsDs = User.Use<RosterImportsDataService>();
        var updated = await importsDs.EntityGetAsync(x => x.Id == batch.Id, TestContext.Current.CancellationToken);
        Assert.Equal(RosterImportStatus.Exported, updated.Status);
    }

    /// <summary>BR-23：已生成（Exported）批次重复生成 → ROSTER_NOT_READY</summary>
    [Fact]
    public async Task ExecuteAsync_AlreadyExported_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(68002);
        var group = await SeedGroupAsync(ownerId, $"群组{68002}");
        var batch = await SeedBatchAsync(group.Id, ownerId, RosterImportStatus.Exported, new[] { "13800138011" });
        var svc = User.Use<GenerateInviteCodesService>();

        var result = await svc.ExecuteAsync(new GenerateInviteCodesReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
            Confirm = true,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-18：Processing 批次 → ROSTER_NOT_READY</summary>
    [Fact]
    public async Task ExecuteAsync_ProcessingBatch_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(68003);
        var group = await SeedGroupAsync(ownerId, $"群组{68003}");
        var batch = await SeedBatchAsync(group.Id, ownerId, RosterImportStatus.Processing, new[] { "13800138021" });
        var svc = User.Use<GenerateInviteCodesService>();

        var result = await svc.ExecuteAsync(new GenerateInviteCodesReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
            Confirm = true,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-07/08：非群主访问 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsGroupNotFound()
    {
        var ownerId = SetUser(68004);
        var group = await SeedGroupAsync(999901, "他人群组");
        await SeedBatchAsync(group.Id, 999901, RosterImportStatus.Ready, new[] { "13800138031" });
        var svc = User.Use<GenerateInviteCodesService>();

        var result = await svc.ExecuteAsync(new GenerateInviteCodesReqDto
        {
            GroupId = group.Id,
            ImportId = 1,
            Confirm = true,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
        Assert.Equal(68004, ownerId);
    }
}
