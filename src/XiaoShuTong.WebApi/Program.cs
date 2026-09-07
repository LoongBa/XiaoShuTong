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
            cfg.UseFreeSqlEntityDAC(); // 从配置绑定读取 ConnectionString，isDevelopment 自动获取
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