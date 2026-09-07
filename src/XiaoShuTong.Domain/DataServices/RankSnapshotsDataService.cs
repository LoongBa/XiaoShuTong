using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Entities.Rank.DTOs;

namespace XiaoShuTong.DataServices.Rank;

/// <summary>数据服务：&#x6392;&#x540D;&#x5FEB;&#x7167;&#xFF08;&#x6392;&#x884C;&#x699C;&#x8D8B;&#x52BF;&#x6570;&#x636E;&#x6E90;&#xFF0C;&#x4E00;&#x4EBA;&#x4E00;&#x8303;&#x56F4;&#x4E00;&#x5B66;&#x79D1;&#x4E00;&#x6307;&#x6807;&#x4E00;&#x65E5;&#x4E00;&#x884C;&#xFF09;</summary>
// 提示：标准 CRUD 逻辑和构造函数已由 RankSnapshotsDataService.g.cs 承载。
// 这里的分部类仅用于编写特定的业务查询方法。
//
// 【V4.9.40 架构说明】
// - DataService 为 sealed 分部类，.g.cs 承载所有 CRUD 和搜索能力
// - 自定义业务逻辑 → 在 `Services/` 下创建 Service 类，标注 `[GenerateController]`
// - 数据过滤干预 → 实现 IGlobalQueryFilter（初始化器基类 DomainHostInitializerBase 已实现，override Apply<T> 即可）
// - 不要手动创建 Controller 类（V3.7 起由 SG 自动生成）
 partial class RankSnapshotsDataService(IDomainUser user, IEntityDAC<RankSnapshots> dac)
        : DomainDataServiceBase<RankSnapshots, RankSnapshotsDto>(user, dac, hasSoftDelete:false) 
{
     // 场景：业务驱动的删除（自动按 HasSoftDelete 分派：有软删→软删，无软删→物理删）
     public async Task<bool> AdminDeleteAsync(long id, CancellationToken ct)
     {
         // 公共 DeleteAsync 自动分派——不调用 [Obsolete] 的 InternalHardDeleteAsync（ADR15）
         return await DeleteAsync(id, ct);
     }
}