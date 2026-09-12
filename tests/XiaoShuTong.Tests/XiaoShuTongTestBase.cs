// ═══════════════════════════════════════════════════════
// Tier 1.5 测试中间基类：每 Fact 前重置 SQLite :memory: 测试库
// ═══════════════════════════════════════════════════════
//
// 为什么需要本基类：
//   Tier 1.5（SQLite :memory: 真实内存库）下单 host（集合 Fixture）共享同一个内存库，
//   跨 Fact 状态残留（写基表后读聚合错乱/主键冲突/数据污染——G6 实证）。
//   ADR66 契约：隔离责任在测试侧——每个测试从干净空库开始。
//
// 挂载点说明：
//   DomainXunitTestBase.InitializeAsync（基类实现）→ 创建会话 scope + User → 调 OnInitializeAsync()。
//   OnInitializeAsync 是基类提供的虚方法（非遮蔽基类），此处 override 调 Reset——
//   User 会话已建立（G6b 修复后 Reset 原地清空不失效引用），随后每个 [Fact] 从空库开始。
// ═══════════════════════════════════════════════════════

using TKW.Framework.Domain.FreeSql;
using TKW.Framework.Domain.Testing.xUnit;

namespace XiaoShuTong.Tests;

/// <summary>
/// XiaoShuTong 领域测试统一基类。
/// 每 Fact 前经 <see cref="OnInitializeAsync"/> 调用 <see cref="FreeSqlTestDbResetExtensions.ResetTier15SqliteMemoryDbAsync"/>
/// （非 SQLite 场景 no-op；SQLite 场景原地清空：DELETE 数据 + DROP/重建视图，实例/引用保持有效）。
/// 项目全部测试类继承本基类而非直接继承 DomainXunitTestBase，保证 Tier 1.5 状态隔离契约一致。
/// </summary>
public abstract class XiaoShuTongTestBase(
    XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    /// <summary>每 Fact 前重置 SQLite :memory: 测试库（ADR66 契约；MockDb/PostgreSQL 场景 no-op）。</summary>
    protected override async Task OnInitializeAsync()
    {
        if (Fixture.Host != null)
            await Fixture.Host.ResetTier15SqliteMemoryDbAsync();
    }
}
