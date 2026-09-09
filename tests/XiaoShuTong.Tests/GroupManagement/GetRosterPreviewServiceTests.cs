using System.Text.Json;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.7 查看名单整理预览（GetRosterPreviewService）Contract 测试
/// 覆盖 BR：BR-07 群组不存在 | BR-08 非 Owner → 5001 | BR-18 批次不存在/不属本群 → 5204（Processing 返回轮询态）
/// BR-19 手机号脱敏展示（前缀 3 位 + **** + 后 4 位，禁止返回完整手机号）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetRosterPreviewServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 非当前用户的第三方群主 Id（避免与本文件测试用户 Id 冲突）
    private const long OtherOwnerId = 999941;

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
        long groupId, long ownerId, RosterImportStatus status, string[] phones,
        int cleanedCount = 0, int duplicateCount = 0, int invalidCount = 0)
    {
        var ds = User.Use<RosterImportsDataService>();
        return await ds.EntityCreateAsync(new RosterImports
        {
            GroupId = groupId,
            OwnerId = ownerId,
            ImportMethod = "Paste",
            SourceCount = phones.Length,
            CleanedCount = cleanedCount,
            DuplicateCount = duplicateCount,
            InvalidCount = invalidCount,
            Status = status,
            RawPhonesJson = JsonSerializer.Serialize(phones),
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-19：Ready 批次 → 返回统计 + 脱敏预览（无完整手机号）</summary>
    [Fact]
    public async Task ExecuteAsync_ReadyBatch_ReturnsMaskedPreview()
    {
        var ownerId = SetUser(47301);
        var group = await SeedGroupAsync(ownerId, $"群组{47301}");
        var batch = await SeedBatchAsync(
            group.Id, ownerId, RosterImportStatus.Ready,
            new[] { "13800138001", "13800138002" }, cleanedCount: 2);
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(batch.Id, result.ImportId);
        Assert.Equal("Ready", result.Status);
        Assert.Equal(2, result.CleanedCount);

        // BR-19：脱敏后（前缀 3 + **** + 后 4），不含完整手机号
        Assert.Equal(["138****8001", "138****8002"], result.Preview);
        Assert.DoesNotContain(result.Preview, p => p == "13800138001");
        Assert.DoesNotContain(result.Preview, p => p.Contains("38001"));
    }

    /// <summary>BR-18 轮询态：Processing 批次 → 返回当前状态 + 空预览（前端轮询，不 5204）</summary>
    [Fact]
    public async Task ExecuteAsync_ProcessingBatch_ReturnsPollingState()
    {
        var ownerId = SetUser(47302);
        var group = await SeedGroupAsync(ownerId, $"群组{47302}");
        var batch = await SeedBatchAsync(group.Id, ownerId, RosterImportStatus.Processing, new[] { "13800138011" });
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Processing", result.Status);
        Assert.Empty(result.Preview);
    }

    /// <summary>BR-19 扩展：Failed 批次 → 返回统计 + 原始名单脱敏预览</summary>
    [Fact]
    public async Task ExecuteAsync_FailedBatch_ReturnsStatsAndMaskedPreview()
    {
        var ownerId = SetUser(47303);
        var group = await SeedGroupAsync(ownerId, $"群组{47303}");
        var batch = await SeedBatchAsync(
            group.Id, ownerId, RosterImportStatus.Failed,
            new[] { "1234567890", "23800138000" }, invalidCount: 2);
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Failed", result.Status);
        Assert.Equal(2, result.InvalidCount);
        Assert.Equal(2, result.SourceCount);
        // BR-19：非 Processing 批次一律脱敏展示（含 Failed）
        Assert.Equal(["123****7890", "238****8000"], result.Preview);
    }

    /// <summary>BR-18：批次不存在 → 5204</summary>
    [Fact]
    public async Task ExecuteAsync_NonexistentBatch_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(47304);
        var group = await SeedGroupAsync(ownerId, $"群组{47304}");
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
        {
            GroupId = group.Id,
            ImportId = -1,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-18：批次不属于本群组 → 5204</summary>
    [Fact]
    public async Task ExecuteAsync_BatchOfOtherGroup_ReturnsRosterNotReady()
    {
        var ownerId = SetUser(47305);
        var group = await SeedGroupAsync(ownerId, $"群组{47305}");
        var otherGroup = await SeedGroupAsync(OtherOwnerId, "他人群组");
        var otherBatch = await SeedBatchAsync(
            otherGroup.Id, OtherOwnerId, RosterImportStatus.Ready, new[] { "13800138021" }, cleanedCount: 1);
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
        {
            GroupId = group.Id,
            ImportId = otherBatch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.RosterNotReady, result.ErrorCode);
    }

    /// <summary>BR-07/BR-08：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_NonexistentGroup_ReturnsGroupNotFound()
    {
        SetUser(47306);
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
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
        SetUser(47307);
        var group = await SeedGroupAsync(OtherOwnerId, "他人群组");
        var batch = await SeedBatchAsync(
            group.Id, OtherOwnerId, RosterImportStatus.Ready, new[] { "13800138031" }, cleanedCount: 1);
        var svc = User.Use<GetRosterPreviewService>();

        var result = await svc.ExecuteAsync(new GetRosterPreviewReqDto
        {
            GroupId = group.Id,
            ImportId = batch.Id,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-19 单元：脱敏规则（前缀 3 位 + **** + 后 4 位）</summary>
    [Theory]
    [InlineData("13800138000", "138****8000")]
    [InlineData("19912345678", "199****5678")]
    [InlineData("1380013800", "138****3800")]  // 10 位（非法但仍按规则脱敏）
    public void MaskPhone_MasksPrefixAndSuffix(string phone, string expected)
        => Assert.Equal(expected, GetRosterPreviewService.MaskPhone(phone));

    /// <summary>BR-19 越界保护：空/超短手机号原样返回</summary>
    [Theory]
    [InlineData("")]
    [InlineData("123456")] // 长度 < 7 不足以前 3 + 后 4
    public void MaskPhone_TooShort_ReturnsOriginal(string phone)
        => Assert.Equal(phone, GetRosterPreviewService.MaskPhone(phone));
}