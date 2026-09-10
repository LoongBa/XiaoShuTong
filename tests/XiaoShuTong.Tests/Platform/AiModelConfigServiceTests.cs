using System.Text.Json;
using XiaoShuTong.DataServices.Platform;
using XiaoShuTong.Entities.Platform;
using XiaoShuTong.Services.Platform;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Platform;

/// <summary>
/// UC-M04 AI 模型配置管理（AiModelConfigService）Contract 测试
/// 覆盖 BR：平台-BR-01 ApiKey 敏感（不下发前端、空 ApiKey 不改）| CRUD 语义（创建/更新/启停/删除）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class AiModelConfigServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    // 不可达端点（端口 1 必然连接失败）：配置管理测试仅为 CRUD 语义，避免任何真实 HTTP 出网
    private const string UnreachableBaseUrl = "http://127.0.0.1:1";

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private static CreateAiModelConfigReqDto NewReq(int tag) => new()
    {
        Name = $"ai-model-test-{tag}",
        Provider = nameof(AiProvider.OpenAiCompatible),
        BaseUrl = UnreachableBaseUrl,
        ApiKey = $"sk-test-{tag}-secret",
        ModelName = "gpt-test",
        Enabled = true,
        SortOrder = 0,
        TimeoutSeconds = 1,
    };

    /// <summary>主流程：合法请求创建成功并返回 UId，ApiKey 入库</summary>
    [Fact]
    public async Task CreateModel_Valid_ReturnsUid()
    {
        SetUser(51301);
        var svc = User.Use<AiModelConfigService>();

        var result = await svc.CreateModelAsync(NewReq(51301), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.UId));

        // 数据已落库：Name/BaseUrl/ApiKey 均正确（ApiKey 仅入库，不回显）
        var ds = User.Use<AiModelConfigDataService>();
        var model = await ds.EntityGetAsync(x => x.UId == result.UId, TestContext.Current.CancellationToken);
        Assert.NotNull(model);
        Assert.Equal("ai-model-test-51301", model.Name);
        Assert.Equal(UnreachableBaseUrl, model.BaseUrl);
        Assert.Equal("sk-test-51301-secret", model.ApiKey);
        Assert.Equal(nameof(AiProvider.OpenAiCompatible), model.Provider.ToString());
    }

    /// <summary>平台-BR-01 前置：缺 Name/BaseUrl/ApiKey/ModelName → PARAM_INVALID</summary>
    [Fact]
    public async Task CreateModel_MissingFields_ReturnsParamInvalid()
    {
        SetUser(51302);
        var svc = User.Use<AiModelConfigService>();

        // 缺 Name（空白）
        var r1 = await svc.CreateModelAsync(NewReq(51302) with { Name = " " }, TestContext.Current.CancellationToken);
        // 缺 BaseUrl
        var r2 = await svc.CreateModelAsync(NewReq(51302) with { BaseUrl = "" }, TestContext.Current.CancellationToken);
        // 缺 ApiKey
        var r3 = await svc.CreateModelAsync(NewReq(51302) with { ApiKey = " " }, TestContext.Current.CancellationToken);
        // 缺 ModelName
        var r4 = await svc.CreateModelAsync(NewReq(51302) with { ModelName = "" }, TestContext.Current.CancellationToken);
        // Provider 非法枚举
        var r5 = await svc.CreateModelAsync(NewReq(51302) with { Provider = "not-a-provider" }, TestContext.Current.CancellationToken);

        Assert.False(r1.Success);
        Assert.Equal(PlatformErrorCodes.ParamInvalid, r1.ErrorCode);
        Assert.False(r2.Success);
        Assert.Equal(PlatformErrorCodes.ParamInvalid, r2.ErrorCode);
        Assert.False(r3.Success);
        Assert.Equal(PlatformErrorCodes.ParamInvalid, r3.ErrorCode);
        Assert.False(r4.Success);
        Assert.Equal(PlatformErrorCodes.ParamInvalid, r4.ErrorCode);
        Assert.False(r5.Success);
        Assert.Equal(PlatformErrorCodes.ParamInvalid, r5.ErrorCode);
    }

    /// <summary>平台-BR-01：创建后列表序列化不含 apiKey、亦不含敏感值</summary>
    [Fact]
    public async Task ListModels_DoesNotExposeApiKey()
    {
        SetUser(51303);
        var svc = User.Use<AiModelConfigService>();
        var created = await svc.CreateModelAsync(NewReq(51303), TestContext.Current.CancellationToken);
        Assert.True(created.Success);

        var list = await svc.ListModelsAsync(
            new ListAiModelConfigReqDto { Name = "ai-model-test-51303" },
            TestContext.Current.CancellationToken);

        Assert.True(list.Success);
        Assert.NotEmpty(list.Items);

        var json = JsonSerializer.Serialize(list.Items);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        // 敏感值本身也不得出现
        Assert.DoesNotContain("sk-test-51303-secret", json, StringComparison.OrdinalIgnoreCase);
        // 普通字段正常回显
        Assert.Contains("ai-model-test-51303", json);
    }

    /// <summary>平台-BR-01：更新传 ApiKey=null/空 → 数据库中 ApiKey 不变（其余字段可改）</summary>
    [Fact]
    public async Task UpdateModel_EmptyApiKey_KeepsOriginal()
    {
        SetUser(51304);
        var svc = User.Use<AiModelConfigService>();
        var created = await svc.CreateModelAsync(NewReq(51304), TestContext.Current.CancellationToken);
        Assert.True(created.Success);

        // ApiKey=null + 改名 → 改名生效、ApiKey 保持原值
        var upd1 = await svc.UpdateModelAsync(
            new UpdateAiModelConfigReqDto { UId = created.UId, ApiKey = null, Name = "ai-model-test-51304-updated" },
            TestContext.Current.CancellationToken);
        // ApiKey=空串 → 仍不改
        var upd2 = await svc.UpdateModelAsync(
            new UpdateAiModelConfigReqDto { UId = created.UId, ApiKey = "" },
            TestContext.Current.CancellationToken);

        Assert.True(upd1.Success);
        Assert.True(upd2.Success);

        var ds = User.Use<AiModelConfigDataService>();
        var model = await ds.EntityGetAsync(x => x.UId == created.UId, TestContext.Current.CancellationToken);
        Assert.NotNull(model);
        Assert.Equal("sk-test-51304-secret", model.ApiKey);
        Assert.Equal("ai-model-test-51304-updated", model.Name);
    }

    /// <summary>更新：UId 不存在 → MODEL_NOT_FOUND</summary>
    [Fact]
    public async Task UpdateModel_UnknownUid_ReturnsModelNotFound()
    {
        SetUser(51305);
        var svc = User.Use<AiModelConfigService>();

        var result = await svc.UpdateModelAsync(
            new UpdateAiModelConfigReqDto { UId = "no-such-model-51305" },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PlatformErrorCodes.ModelNotFound, result.ErrorCode);
    }

    /// <summary>启停：UId 不存在 → MODEL_NOT_FOUND</summary>
    [Fact]
    public async Task SetEnabled_UnknownUid_ReturnsModelNotFound()
    {
        SetUser(51306);
        var svc = User.Use<AiModelConfigService>();

        var result = await svc.SetEnabledAsync(
            new SetEnabledReqDto { UId = "no-such-model-51306", Enabled = true },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PlatformErrorCodes.ModelNotFound, result.ErrorCode);
    }

    /// <summary>删除：UId 不存在 → MODEL_NOT_FOUND</summary>
    [Fact]
    public async Task DeleteModel_UnknownUid_ReturnsModelNotFound()
    {
        SetUser(51307);
        var svc = User.Use<AiModelConfigService>();

        var result = await svc.DeleteModelAsync(
            new DeleteAiModelConfigReqDto { UId = "no-such-model-51307" },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PlatformErrorCodes.ModelNotFound, result.ErrorCode);
    }

    /// <summary>主流程：删除后列表不再包含该 UId</summary>
    [Fact]
    public async Task DeleteModel_Valid_Removes()
    {
        SetUser(51308);
        var svc = User.Use<AiModelConfigService>();
        var created = await svc.CreateModelAsync(NewReq(51308), TestContext.Current.CancellationToken);
        Assert.True(created.Success);

        var del = await svc.DeleteModelAsync(
            new DeleteAiModelConfigReqDto { UId = created.UId },
            TestContext.Current.CancellationToken);

        Assert.True(del.Success);
        Assert.True(del.Removed);

        var list = await svc.ListModelsAsync(
            new ListAiModelConfigReqDto { Name = "ai-model-test-51308" },
            TestContext.Current.CancellationToken);
        Assert.Empty(list.Items);
    }
}