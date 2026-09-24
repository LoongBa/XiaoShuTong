using XiaoShuTong;
using TKW.Framework.Domain.ApiService.Hosting;
using TKW.Framework.Domain.FreeSql;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Web.Hosting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// TKWF.Domain V2 四阶段管道初始化
var app = builder.ConfigWebAppDomain<XiaoShuTongUserInfo, XiaoShuTongDomainInitializer, DomainWebOptions>(
        "DomainOptions", cfg =>
        {
            cfg.UseWebExceptionMiddleware = true;
            // V0.6.3 联调：显式 DataType 重载（XML 明示无参重载不设 DataType，默认 PG）。
            // 从 DomainOptions.FreeSqlDataTypeName 配置键解析——null/解析失败回退 PostgreSQL（向后兼容），生产配置不受影响。
            cfg.UseFreeSqlEntityDAC(
                Enum.TryParse<FreeSql.DataType>(cfg.FreeSqlDataTypeName, ignoreCase: true, out var dt)
                    ? dt
                    : FreeSql.DataType.PostgreSQL,
                cfg.ConnectionString);
        })
    .RegisterServices((services, cfg) =>
    {
        services.AddOpenApi();
        services.AddCors(o =>
        {
            o.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            o.AddPolicy("ProductionPolicy", p => p.WithOrigins("https://your-frontend-domain.com")
                .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
        });
    })
    .UseWebSession(o =>
    {
        o.SessionKeyName = "XiaoShuTongApiSession";
        o.ExpiredTimeSpan = TimeSpan.FromMinutes(15);
    })
    .UseApiService(opt =>
    {
        opt.UseGraphQL("/graphql", gql =>
        {
            gql.ConfigureSchema(sb => sb
                .BindRuntimeType<System.Collections.IDictionary, HotChocolate.Types.AnyType>()
                .BindRuntimeType<System.Collections.DictionaryEntry, HotChocolate.Types.AnyType>());
        });
        opt.UseRest("/api");
    })
    .BeforeRouting((app, cfg) =>
    {
        if (cfg.IsDevelopment) app.UseDeveloperExceptionPage();
        app.UseCors(cfg.IsDevelopment ? "DevelopmentPolicy" : "ProductionPolicy");
    })
    .AfterRouting((_, _) => { })
    .Build(app =>
    {
        app.UseCors(app.Environment.IsDevelopment() ? "DevelopmentPolicy" : "ProductionPolicy");

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }
    });

await app.RunWithGraphQLCommandsAsync(args);