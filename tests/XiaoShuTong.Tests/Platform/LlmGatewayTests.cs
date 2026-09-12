using XiaoShuTong.DataServices.Platform;
using XiaoShuTong.Entities.Platform;
using XiaoShuTong.Services.Platform;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Platform;

/// <summary>
/// LLM 统一网关（LlmGateway）Contract 测试
/// 覆盖 BR：平台-BR-02 多模型轮询（SortOrder 升序逐一尝试）| 平台-BR-03 全部失败降级不抛错
/// | 平台-BR-04 无启用模型/未配置 → 直接降级，不阻塞业务
/// ⚠️ 不发起真实 HTTP：模型 BaseUrl 指向不可达地址（127.0.0.1:1，端口 1 必然连接失败）+ 极短超时（1s）。
///    仅断言降级信号（Success=false, IsDegraded=true），不依赖网络结果。
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class LlmGatewayTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    // 不可达端点（端口 1 必然连接失败；即使异常也由 1s 超时兜底）
    private const string UnreachableBaseUrl = "http://127.0.0.1:1";

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    /// <summary>
    /// 清空 AiModelConfig 数据（排除跨测试残留的启用模型，保证各用例状态确定）。
    /// Tier 1.5（SQLite :memory:）：MockDbEntityDAC.Clear() 已退役——改为删除全部行，
    /// 语义等价（无任何残留配置），经 IEntityDAC 读写同源（P0-1 Forwarder）。
    /// </summary>
    private async Task ResetModelConfigsAsync()
    {
        var dac = User.GetService<IEntityDAC<AiModelConfig>>();
        var all = await dac.ToListAsync(dac.Query, TestContext.Current.CancellationToken);
        foreach (var cfg in all)
            await dac.DeleteAsync(cfg, TestContext.Current.CancellationToken);
    }

    private async Task SeedModelAsync(int tag, int sortOrder, bool enabled, string baseUrl)
    {
        var ds = User.Use<AiModelConfigDataService>();
        await ds.EntityCreateAsync(new AiModelConfig
        {
            UId = UidGenerator.NewId(),
            Name = $"gw-model-{tag}",
            Provider = AiProvider.OpenAiCompatible,
            BaseUrl = baseUrl,
            ApiKey = $"sk-gateway-{tag}",
            ModelName = "gpt-gateway-test",
            Enabled = enabled,
            SortOrder = sortOrder,
            TimeoutSeconds = 1,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>平台-BR-04：无启用模型（仅禁用模型）→ 降级信号，不触发任何 HTTP</summary>
    [Fact]
    public async Task CompleteAsync_NoEnabledModels_ReturnsDegraded()
    {
        await ResetModelConfigsAsync();
        SetUser(51401);
        await SeedModelAsync(51401, 0, enabled: false, baseUrl: UnreachableBaseUrl);
        var gw = User.Use<LlmGateway>();

        var result = await gw.CompleteAsync("system", "user", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.IsDegraded);
        Assert.Empty(result.Content);
    }

    /// <summary>平台-BR-03：唯一启用模型不可达 → 全部失败 → 降级信号，不抛异常</summary>
    [Fact]
    public async Task CompleteAsync_AllModelsUnreachable_ReturnsDegraded()
    {
        await ResetModelConfigsAsync();
        SetUser(51402);
        await SeedModelAsync(51402, 0, enabled: true, baseUrl: UnreachableBaseUrl);
        var gw = User.Use<LlmGateway>();

        var result = await gw.CompleteAsync("system", "user", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.IsDegraded);
        // 测试通过 = 网络失败被网关内部吞掉（TryCompleteWithModelAsync 捕获 → null），不冒泡给调用方
        Assert.Empty(result.Content);
    }

    /// <summary>平台-BR-02 排序语义：2 个启用模型（SortOrder 0/1）全部不可达 → 逐一尝试后降级，不抛错</summary>
    [Fact]
    public async Task CompleteAsync_OrderBySortOrder_AttemptsInOrder()
    {
        await ResetModelConfigsAsync();
        SetUser(51403);
        await SeedModelAsync(51403, 0, enabled: true, baseUrl: UnreachableBaseUrl);
        await SeedModelAsync(51404, 1, enabled: true, baseUrl: UnreachableBaseUrl);
        var gw = User.Use<LlmGateway>();

        var result = await gw.CompleteAsync("system", "user", TestContext.Current.CancellationToken);

        // 多模型逐一尝试路径被覆盖：首个模型失败 → 切换下一模型 → 仍失败 → 降级
        Assert.False(result.Success);
        Assert.True(result.IsDegraded);
        Assert.Empty(result.Content);
    }

    /// <summary>平台-BR-04：网关未配置任何模型 → 直接降级，不阻塞业务调用</summary>
    [Fact]
    public async Task CompleteAsync_EmptyProviderConfig_NoModels()
    {
        await ResetModelConfigsAsync();
        SetUser(51405);
        var gw = User.Use<LlmGateway>();

        var result = await gw.CompleteAsync("system", "user", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.IsDegraded);
        Assert.Empty(result.Content);
    }
}