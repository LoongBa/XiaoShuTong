using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Core.Hosting;
using System.Threading.Tasks;

namespace XiaoShuTong;

/// <summary>
/// 领域层初始化器。
/// 实体/种子数据相关逻辑在 <see cref="XiaoShuTongDomainInitializer.Data"/> 分部类中维护。
/// <para>V4.9.61 ADR30：基类 <see cref="DomainHostInitializerBase{TUserInfo}"/>
/// 在 <see cref="DomainHostInitializerBase{TUserInfo}.OnServiceProviderBuilt"/> 之后自动调用
/// <c>SyncTables</c>（表结构同步，固定流程），派生类无需覆写 <c>OnServiceProviderBuilt</c> 手动调用。
/// 表先于视图（<c>SyncViewsAsync</c>），消除 42P01 风险。</para>
/// <para>V4.9.85 (ADR47/48/50)：启用扩展须在本类显式声明白名单（发现不自动启用）：</para>
/// <code>
/// [TKWFEnabledExtension(typeof(global::TKWF.Ext.Permissions.PermissionExtensionInitializer&lt;&gt;))]  // 开放泛型
/// </code>
/// <para>未声明的扩展即使被引用也不启用（IsEnabled 默认 false）。</para>
/// </summary>
public partial class XiaoShuTongDomainInitializer : DomainHostInitializerBase<XiaoShuTongUserInfo>
{
    protected override IProjectMetaContext OnRegisterInfrastructureServices(
        IServiceCollection services,
        IConfiguration? configuration,
        IDomainHostOptions options)
    {
        return XiaoShuTong.Generated.ProjectMetaContext.GetOrCreateInstance();
    }

    protected override DomainUserHelperBase<XiaoShuTongUserInfo> OnRegisterDomainServices(
        IServiceCollection services,
        IConfiguration? configuration)
    {
        return new XiaoShuTongUserHelper();
    }

    protected override async Task OnEnsureSystemReadyAsync(
        IServiceProvider sp,
        IReadOnlyList<string> syncedTables,
        IReadOnlyList<string> syncedViews)
    {
        // 委托给 Data 分部类执行实体同步和种子数据
        await Data.OnEnsureDataReadyAsync(sp);
    }
}