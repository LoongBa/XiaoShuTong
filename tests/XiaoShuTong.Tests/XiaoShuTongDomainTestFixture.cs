// ═══════════════════════════════════════════════════════
// 框架测试契约：测试 Fixture 模板
// ═══════════════════════════════════════════════════════
//
// 方式 A（推荐）：ConfigTestDomainAsync — 一行搞定 5 个阻塞点
//   无连接串 → 内存 DAC（SetTestingEntityDAC），无需数据库
//   有连接串 → 真实 PostgreSQL（SetFreeSqlEntityDAC）
//
// 方式 B（手动）：逐行写 —— 用于理解原理或在中间插入自定义注册
//
// 5 个阻塞点（缺一不可）：
//   1. ILogger<T>       → AddLogging
//   2. 会话管理         → TestSessionManager
//   3. DataService 注册 → DomainHost.Initialize（内置 throw-factory）
//   4. IEntityDAC<T>    → SetTestingEntityDAC / SetFreeSqlEntityDAC
//   5. 种子数据/自举    → ServiceProviderBuiltCallbackAsync
// ═══════════════════════════════════════════════════════

using XiaoShuTong;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.FreeSql;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests;

/// <summary>声明此为集合夹具</summary>
[CollectionDefinition("XiaoShuTongDomain")]
public class XiaoShuTongDomainCollection : ICollectionFixture<XiaoShuTongDomainTestFixture> { }

/// <summary>领域测试夹具（每个测试类创建一次）</summary>
public sealed class XiaoShuTongDomainTestFixture : DomainXunitTestFixtureBase
{
    public DomainHost<XiaoShuTongUserInfo>? Host { get; private set; }

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();

        // 从 appsettings.Test.json 读取连接串（由脚本创建时替换 {ConnectionString}）
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();
        var options = config.GetSection("DomainOptions").Get<DomainWebOptions>()
            ?? new DomainWebOptions { IsDevelopment = true };

        // ── 方式 A1：内存 DAC（推荐，无需数据库） ──
        // 无连接串参数 → 内部用 SetTestingEntityDAC()，基于 ConcurrentDictionary
        Host = await services.ConfigTestDomainAsync<XiaoShuTongUserInfo, XiaoShuTongDomainInitializer, DomainWebOptions>(
            options);

        // ── 方式 B：手动逐行（等价的 5 步，用于理解原理或插入自定义注册） ──
        // services.AddLogging(b => b.ClearProviders().SetMinimumLevel(LogLevel.Warning));
        // services.AddSingleton<ISessionManager<XiaoShuTongUserInfo>, TestSessionManager<XiaoShuTongUserInfo>>();
        // var host = DomainHost<XiaoShuTongUserInfo>.Initialize<XiaoShuTongDomainInitializer, DomainWebOptions>(services, options);
        // services.SetTestingEntityDAC();       // 内存 DAC
        // // 或 services.SetFreeSqlEntityDAC(connectionString, isDev);  // 真实 DB
        // var sp = services.BuildServiceProvider();
        // var initializer = sp.GetRequiredService<DomainHostInitializerBase<XiaoShuTongUserInfo, DomainWebOptions>>();
        // await initializer.ServiceProviderBuiltCallbackAsync(sp);
        // Host = host;
    }
}