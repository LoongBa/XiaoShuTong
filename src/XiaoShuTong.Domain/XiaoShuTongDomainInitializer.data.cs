using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace XiaoShuTong;

// ══════════════════════════════════════════════════════════════════
// 实体同步与种子数据维护文件
//
// ⚠ 规则：增加/删除 Entity 后必须同步此文件。
//    - 新增 Entity → 在此添加种子数据（测试/初始化用）
//    - 删除 Entity → 移除此处对应的种子数据逻辑
//
// 此文件为 partial class，与 XiaoShuTongDomainInitializer.cs 配对。
// 修改此文件无需触及主初始化器。Agent 按此规则执行。
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// 实体同步与种子数据。增加/删除 Entity 时同步更新此处。
/// </summary>
public partial class XiaoShuTongDomainInitializer
{
    /// <summary>
    /// Data 分部类：只包含实体同步 + 种子数据逻辑。
    /// </summary>
    internal static class Data
    {
        /// <summary>
        /// 实体同步 + 种子数据插入入口。
        /// Agent：按业务需求在此添加/移除实体对应的种子数据（开发/测试环境）。
        /// </summary>
        public static async Task OnEnsureDataReadyAsync(IServiceProvider sp)
        {
            // TODO Agent: 在此添加实体种子数据
            // 示例：
            // var repo = sp.GetRequiredService<IFreeSql>();
            // if (await repo.Select<Merchant>().AnyAsync())
            //     return;  // 已有数据则跳过
            // repo.Insert(new Merchant { Name = "默认商户" });

            await Task.CompletedTask;
        }
    }
}