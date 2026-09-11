// ═══════════════════════════════════════════════════════
// 框架测试契约：测试 Fixture 模板
// ═══════════════════════════════════════════════════════
//
// 方式 A（推荐）：ConfigTestDomainAsync — 一行搞定 5 个阻塞点
//   无 configure → 默认内存 MockDbEntityDAC（V4.10.7+），无需数据库
//   cfg.UseFreeSqlEntityDAC(DataType.Sqlite, ":memory:") → SQLite 真实内存库（Tier 1.5）
//
// 方式 B（手动）：逐行写 —— 用于理解原理或在中间插入自定义注册
//
// 5 个阻塞点（缺一不可）：
//   1. ILogger<T>       → AddLogging
//   2. 会话管理         → TestSessionManager
//   3. DataService 注册 → DomainHost.Initialize（内置 throw-factory）
//   4. IEntityDAC<T>    → SetEntityDAC(MockDbEntityDAC<>) / SetEntityDAC(FreeSqlEntityDAC<>)
//   5. 种子数据/自举    → ServiceProviderBuiltCallbackAsync
// ═══════════════════════════════════════════════════════

using XiaoShuTong;
using FreeSql;
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

        // ── Tier 1：内存 DAC（默认 MockDbEntityDAC，V4.10.7+） ──
        // ⚠️ Tier 1.5（SQLite :memory:）暂不可用：框架 ConfigTestDomainAsync + UseFreeSqlEntityDAC(Sqlite,:memory:)
        //    SyncViewsAsync 执行 ViewSql 时 ObjectDisposedException（FreeSql SQLite 连接池 :memory: 不保活，
        //    SyncStructure 后连接释放）——框架自身测试全用裸 FreeSqlBuilder（绕过 DomainHost），此组合未实证。
        //    待框架修复后启用 Tier 1.5（见 PkPlayerStatsView ViewSqlSQLite 已就绪）。
        Host = await services.ConfigTestDomainAsync<XiaoShuTongUserInfo, XiaoShuTongDomainInitializer, DomainWebOptions>(
            options);

        // ── 方式 B：手动逐行（等价的 5 步，用于理解原理或插入自定义注册） ──
        // services.AddLogging(b => b.ClearProviders().SetMinimumLevel(LogLevel.Warning));
        // services.AddSingleton<ISessionManager<XiaoShuTongUserInfo>, TestSessionManager<XiaoShuTongUserInfo>>();
        // var host = DomainHost<XiaoShuTongUserInfo>.Initialize<XiaoShuTongDomainInitializer, DomainWebOptions>(services, options);
        // services.SetEntityDAC(typeof(MockDbEntityDAC<>));    // 内存 DAC
        // // 或 services.SetEntityDAC(typeof(FreeSqlEntityDAC<>)) + cfg.ConnectionStringTemplate;  // 真实 DB
        // var sp = services.BuildServiceProvider();
        // var initializer = sp.GetRequiredService<DomainHostInitializerBase<XiaoShuTongUserInfo, DomainWebOptions>>();
        // await initializer.ServiceProviderBuiltCallbackAsync(sp);
        // Host = host;
    }
}