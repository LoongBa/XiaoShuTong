using XiaoShuTong;
using XiaoShuTong.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TKW.Framework.Domain.ApiClient;
using TKW.Framework.Domain.Blazor.Abstractions.Extensions;
using TKW.Framework.Domain.Blazor.Wasm.Extensions;
using TKW.Framework.Session;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var webApiBaseUrl = builder.Configuration.GetSection("WebApi:BaseUrl").Value
                    ?? builder.HostEnvironment.BaseAddress;

// ─── 1. 根组件 ───
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ───────────────────────────────────────────────────────────────
// 领域客户端 RPC 传输 + 本地业务服务 → 构建 → 启动
var app = await builder.ConfigWasmClient<XiaoShuTongUserInfo>(webApiBaseUrl, options =>
{
    options.IsDevelopment = builder.HostEnvironment.IsEnvironment("Development");
})
.RegisterServices(sp =>
{
    // ─── 2. 注册 HttpClient、会话服务 ───
    sp.AddSessionAwareHttpClient("WebApi", webApiBaseUrl);
    sp.AddScoped(sp => new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });
    sp.AddSessionServices();
})
.UseApiClient(ApiClientType.GraphQL)    // 启用 ApiClient（GraphQL）传输
.UseWasmAuth()                          // 启用 Wasm 认证
.BuildClient<XiaoShuTongUserInfo>();         // 构建客户端
await app.RunAsync();                   // 运行 Wasm 应用