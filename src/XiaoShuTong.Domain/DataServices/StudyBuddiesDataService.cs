using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Buddy.DTOs;

namespace XiaoShuTong.DataServices.Buddy;

/// <summary>数据服务：&#x5B66;&#x4E60;&#x642D;&#x5B50;&#xFF08;&#x53CC;&#x5411;&#x540C;&#x610F;&#x5236;&#xFF0C;&#x540C;&#x4E00;&#x5BF9;&#x7528;&#x6237;&#x4EC5;&#x4E00;&#x6761;&#x5173;&#x7CFB;&#x8BB0;&#x5F55;&#xFF09;</summary>
// 提示：标准 CRUD 逻辑和构造函数已由 StudyBuddiesDataService.g.cs 承载。
// 这里的分部类仅用于编写特定的业务查询方法。
//
// 【V4.9.40 架构说明】
// - DataService 为 sealed 分部类，.g.cs 承载所有 CRUD 和搜索能力
// - 自定义业务逻辑 → 在 `Services/` 下创建 Service 类，标注 `[GenerateController]`
// - 数据过滤干预 → 实现 IGlobalQueryFilter（初始化器基类 DomainHostInitializerBase 已实现，override Apply<T> 即可）
// - 不要手动创建 Controller 类（V3.7 起由 SG 自动生成）
 partial class StudyBuddiesDataService(IDomainUser user, IEntityDAC<StudyBuddies> dac)
        : DomainDataServiceBase<StudyBuddies, StudyBuddiesDto>(user, dac, hasSoftDelete:false) 
{
     // 场景：业务驱动的删除（自动按 HasSoftDelete 分派：有软删→软删，无软删→物理删）
     public async Task<bool> AdminDeleteAsync(long id, CancellationToken ct)
     {
         // 公共 DeleteAsync 自动分派——不调用 [Obsolete] 的 InternalHardDeleteAsync（ADR15）
         return await DeleteAsync(id, ct);
     }
}