// ═══════════════════════════════════════════════════════
// 框架测试契约：测试类模板
// ═══════════════════════════════════════════════════════
//
// 生命周期（xUnit IAsyncLifetime）：
//   Fixture 构造函数 → Fixture.InitializeAsync
//   → 测试类构造函数 → 测试类.InitializeAsync（创建会话 + User）
//   → 每个测试方法 → 测试类.DisposeAsync（销毁会话）
//
// 关键约束：DataService 不能构造函数注入（throw-factory）
// → 全部用 this.User.Use<T>() 在方法体内获取（V4.9+ 统一 API，具体类自动走 NoAop）。
// ═══════════════════════════════════════════════════════

using XiaoShuTong;
using XiaoShuTong.DataServices;
using XiaoShuTong.Entities;
// using XiaoShuTong.Services;  // 骨架阶段不存在，写真实 Service 时取消注释
using TKW.Framework.Domain;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests;

/// <summary>
/// [Collection] 必须匹配 Fixture 的 CollectionDefinition 名称。
/// 同一个 Collection 内的测试共享 Fixture 实例（不共享 DB 事务，独立会话）。
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]    // 用于 CI 过滤：dotnet test --filter "Category=Contract"
public class XiaoShuTongServiceTests : XiaoShuTongTestBase
{
    public XiaoShuTongServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    /// <summary>
    /// 测试 Service（带 [GenerateController] 的业务服务）
    /// 写真实测试时：取消 Skip + 替换类型名
    /// </summary>
    [Fact(Skip = "替换为真实 Service 后启用")]
    public async Task MyService_ShouldDoSomething()
    {
        // 取消 using XiaoShuTong.Services 注释
        // var svc = this.User.Use<EnrollMemberService>();
        // var result = await svc.ExecuteAsync(/* 参数 */);
        // Assert.True(result.Success);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 测试 DataService（数据存取层）
    /// 写真实测试时：取消 Skip + 替换类型名
    /// </summary>
    [Fact(Skip = "替换为真实 DataService 后启用")]
    public async Task MyDataService_Query()
    {
        // var ds = this.User.Use<MemberDataService>();
        // var existing = await ds.EntityGetAsync(m => m.Phone == "13800138001");
        // Assert.NotNull(existing);
        await Task.CompletedTask;
    }
}