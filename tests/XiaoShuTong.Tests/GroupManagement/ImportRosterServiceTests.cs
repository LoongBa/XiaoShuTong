using System.Text.Json;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Services.GroupManagement;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.GroupManagement;

/// <summary>
/// UC-6.5 导入成员名单（ImportRosterService）Contract 测试
/// 覆盖 BR：BR-07 群组不存在 | BR-08 非 Owner → 5001 | BR-14 名单为空不可提交 → 1002
/// BR-15 创建批次（Processing + 统计原始行数）供异步 Agent 整理（UC-6.6）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class ImportRosterServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 非当前用户的第三方群主 Id（避免与本文件测试用户 Id 冲突）
    private const long OtherOwnerId = 999931;

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

    /// <summary>主流程 + BR-15：合法名单 → 创建 Processing 批次 + 统计原始行数</summary>
    [Fact]
    public async Task ExecuteAsync_ValidPastedText_CreatesProcessingBatch()
    {
        var ownerId = SetUser(47201);
        var group = await SeedGroupAsync(ownerId, $"群组{47201}");
        var svc = User.Use<ImportRosterService>();
        var text = "13800138001\n13800138002\n13800138003";

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = group.Id,
            Method = "Paste",
            RawText = text,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.ImportId > 0);
        Assert.Equal("Processing", result.Status);
        Assert.Equal(3, result.SourceCount);

        // 批次已落库（Status=Processing、OwnerId、RawPhonesJson 保留原始名单）
        var importsDs = User.Use<RosterImportsDataService>();
        var batch = await importsDs.EntityGetAsync(x => x.Id == result.ImportId, TestContext.Current.CancellationToken);
        Assert.NotNull(batch);
        Assert.Equal(RosterImportStatus.Processing, batch.Status);
        Assert.Equal(ownerId, batch.OwnerId);
        Assert.Equal(group.Id, batch.GroupId);
        Assert.Equal("Paste", batch.ImportMethod);
        var phones = JsonSerializer.Deserialize<List<string>>(batch.RawPhonesJson!);
        Assert.NotNull(phones);
        Assert.Equal(new[] { "13800138001", "13800138002", "13800138003" }, phones);
    }

    /// <summary>主流程：多行文本含空行/空白行 → 仅统计非空行（BR-15 行数口径）</summary>
    [Fact]
    public async Task ExecuteAsync_TextWithBlankLines_CountsNonEmptyLinesOnly()
    {
        var ownerId = SetUser(47202);
        var group = await SeedGroupAsync(ownerId, $"群组{47202}");
        var svc = User.Use<ImportRosterService>();

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = group.Id,
            Method = "Paste",
            RawText = "13800138011\n\n   \r\n13800138012",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.SourceCount);

        var importsDs = User.Use<RosterImportsDataService>();
        var batch = await importsDs.EntityGetAsync(x => x.Id == result.ImportId, TestContext.Current.CancellationToken);
        Assert.NotNull(batch);
        var phones = JsonSerializer.Deserialize<List<string>>(batch.RawPhonesJson!);
        Assert.NotNull(phones);
        Assert.Equal(new[] { "13800138011", "13800138012" }, phones);
    }

    /// <summary>主流程：File 方式 → Method 透传落库</summary>
    [Fact]
    public async Task ExecuteAsync_FileMethod_PersistsImportMethod()
    {
        var ownerId = SetUser(47203);
        var group = await SeedGroupAsync(ownerId, $"群组{47203}");
        var svc = User.Use<ImportRosterService>();

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = group.Id,
            Method = "File",
            RawText = "13800138021",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Processing", result.Status);

        var importsDs = User.Use<RosterImportsDataService>();
        var batch = await importsDs.EntityGetAsync(x => x.Id == result.ImportId, TestContext.Current.CancellationToken);
        Assert.NotNull(batch);
        Assert.Equal("File", batch.ImportMethod);
    }

    /// <summary>BR-14：名单为空（null）→ PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_NullText_ReturnsParamInvalid()
    {
        var ownerId = SetUser(47204);
        var group = await SeedGroupAsync(ownerId, $"群组{47204}");
        var svc = User.Use<ImportRosterService>();

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = group.Id,
            Method = "Paste",
            RawText = null,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-14：名单为空（纯空白）→ PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_WhitespaceOnlyText_ReturnsParamInvalid()
    {
        var ownerId = SetUser(47205);
        var group = await SeedGroupAsync(ownerId, $"群组{47205}");
        var svc = User.Use<ImportRosterService>();

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = group.Id,
            Method = "Paste",
            RawText = "  \n\t\n ",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-07/BR-08：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_NonexistentGroup_ReturnsGroupNotFound()
    {
        SetUser(47206);
        var svc = User.Use<ImportRosterService>();

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = -1,
            Method = "Paste",
            RawText = "13800138031",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-08：非 Owner 访问他人群组 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwner_ReturnsGroupNotFound()
    {
        SetUser(47207);
        var group = await SeedGroupAsync(OtherOwnerId, "他人群组");
        var svc = User.Use<ImportRosterService>();

        var result = await svc.ExecuteAsync(new ImportRosterReqDto
        {
            GroupId = group.Id,
            Method = "Paste",
            RawText = "13800138041",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(GroupErrorCodes.GroupNotFound, result.ErrorCode);
    }
}