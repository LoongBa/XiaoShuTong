---
title: 小书童 - AdminWasm V0.1.0 开发方案
version: AdminWasm-V0.1.0
summary: AdminWasm 落地 AntDesign Blazor Pro 脚手架 + 项目改名 + 独立版本线
last_updated: 2026-09-10
status: 已定稿
base_arch_version: D05 V1.1
---

# 小书童 AdminWasm V0.1.0 开发方案（AntDesign Blazor Pro 脚手架落地）

## 一、本版目标

- **核心交付**：将 `src/XiaoShuTong.Wasm`（裸 WASM 模板）迁移为 **AntDesign Blazor Pro 脚手架**（`AntDesign.ProLayout` NuGet 包模式），保留 TKWF `ApiClient` 集成；项目与目录改名 `XiaoShuTong.AdminWasm`；建立 AdminWasm 独立 MinVer 版本线。
- **完成定义（DoD）**：`dotnet build` 0 错误；MinVer 从 `AdminWasm-V0.1.0` tag 正确派生 `0.1.0` 且领域线（V0.3.2）不受影响；全仓库无 `XiaoShuTong.Wasm` 残留；`XiaoShuTong.slnx` 引用同步。

## 二、范围

### 包含

1. AntDesign Blazor Pro 脚手架迁移（布局/菜单/登录/静态资源骨架）
2. TKWF ApiClient 集成保留（`ConfigWasmClient` + `UseApiClient(GraphQL)` + `UseWasmAuth`）
3. 项目与目录改名：`XiaoShuTong.Wasm` → `XiaoShuTong.AdminWasm`
4. 独立版本线：csproj `<MinVerTagPrefix>AdminWasm-V</MinVerTagPrefix>`
5. 版本发布：变更记录 + tag `AdminWasm-V0.1.0`

### 不包含

- Pro Demo 页面（Dashboard 分析/表单/列表/搜索/账号中心等）——保留骨架，业务页后续迭代
- 图表（AntDesign.Charts）——当前无统计需求，CMP 已有版本待用
- WebH5 版本线配置（新规则已记入 AGENTS.md，待 WebH5 立项时执行）
- 认证扩展（短信/扫码/SecurePassword 多场景）——仅保留密码登录

## 三、设计依据

| 文档 | 版本 | 路径 |
|------|------|------|
| 前端页面结构与测试策略设计 | V1.1 | `docs/D05-前端页面结构与测试策略设计.md` |
| 参考实现（TKWF + AntDesign Pro 组合） | v0.9 系 | `F:\LoongBa_Git\DMP-Lite\DMP-Lite.Merchant.AdminUI\`（只读参照，简单参考精简约 30 文件） |
| 独立版本线约定 | - | `docs/AGENTS.md` 核心规则（本次新增） |

> 技术选型未新增 ADR：AntDesign Blazor Pro 为既有市场成熟组合（AntDesign 1.6.2 / ProLayout 1.6.2 / Extensions.Localization 1.6.2，均已在 `Directory.Packages.props` CPM 声明），TKWF ApiClient 角色集成方式照搬 DMP-Lite.AdminUI 已验证模式。

## 四、任务拆解

| 任务 | 描述 | 关联ADR | 完成状态 |
|------|------|--------|---------|
| T1 | csproj 引入 AntDesign 包（ProLayout / Extensions.Localization / WebAssembly.Authentication，版本走 CPM） | 无 | ✅ |
| T2 | Program.cs 追加 AntDesign 注册（AddAntDesign / ProSettings / 国际化），保留 TKWF 三段链 | 无 | ✅ |
| T3 | Pro 布局骨架：Layouts/BasicLayout + UserLayout、Routes.razor、App.razor（ErrorBoundary + AntContainer + 会话过期 Modal） | 无 | ✅ |
| T4 | 登录：Pages/User/Login（密码单 Tab）+ Components（RedirectToLogin / RightContent 精简版）+ Services/SessionExpiredState | 无 | ✅ |
| T5 | wwwroot 全套新建（index.html 引 Pro CSS/JS、appsettings.json、菜单、样式、品牌 SVG assets） | 无 | ✅ |
| T6 | 项目与目录改名 XiaoShuTong.Wasm → XiaoShuTong.AdminWasm（git mv + 17 文件内容替换 + slnx 同步） | 无 | ✅ |
| T7 | 独立版本线：csproj MinVerTagPrefix + 变更记录 + tag `AdminWasm-V0.1.0` | 无 | ✅ |
| T8 | 编译验证 + MinVer 派生验证 + 残留检查 | 无 | ✅ |

## 五、技术方案

### 模块结构

```
src/XiaoShuTong.AdminWasm/
├── XiaoShuTong.AdminWasm.csproj   # +AntDesign 包 + MinVerTagPrefix=AdminWasm-V
├── Program.cs                     # TKWF ApiClient 链 + AntDesign 注册
├── App.razor                      # ErrorBoundary + Routes + AntContainer + 会话过期 Modal
├── Routes.razor                   # Router + AuthorizeRouteView（BasicLayout 默认）
├── Layouts/                       # BasicLayout(.razor/.cs) + UserLayout(.razor/.css)
├── Components/                    # RedirectToLogin + GlobalHeader/RightContent（精简）
├── Pages/                         # Home（欢迎页）+ User/Login（密码登录）
├── Services/                      # SessionExpiredState
├── Models/                        # LoginParamsType
└── wwwroot/                       # index.html + appsettings.json + css + data/menu.json + assets
```

### 核心流程

```
启动 → ConfigWasmClient<XiaoShuTongUserInfo> → RegisterServices(AddAntDesign+ProSettings+国际化+Session)
    → UseApiClient(GraphQL) → UseWasmAuth → BuildClient → RunAsync
路由 → Routes.razor（Authorize）→ BasicLayout（ProLayout 侧边菜单 data/menu.json）
未登录 → RedirectToLogin → /user/login（DomainClientUser.LoginAsAsync + AuthContext.Password）
会话过期 → DomainUser.OnAuthRequired → App.razor Modal → 重新登录
```

### 依赖版本（CPM 集中声明，csproj 不写版本号）

| 包 | 版本 | 说明 |
|----|------|------|
| AntDesign.ProLayout | 1.6.2 | Pro 布局引擎（BasicLayout/SettingDrawer/菜单），传递引入 AntDesign 1.6.2 |
| AntDesign.Extensions.Localization | 1.6.2 | 交互式国际化 |
| Microsoft.AspNetCore.Components.WebAssembly.Authentication | 10.0.10 | WASM 认证基础设施 |
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.10 | 既有 |

> AntDesign.ProLayout 1.6.2 nuspec 未显式声明 net10.0 TFM（仅 net5.0-net9.0 + netstandard2.1），net10.0 项目通过资产 roll-forward 兼容，本次构建已实测可用。

### 独立版本线机制

- 领域线：全局 `Directory.Build.props` 的 `MinVerTagPrefix=v`（大小写不敏感匹配现有 `V0.x.x` tag）
- AdminWasm 线：csproj 覆盖 `<MinVerTagPrefix>AdminWasm-V</MinVerTagPrefix>`，只识别 `AdminWasm-V*` tag
- `git tag` 是仓库全局的，三线 tag 名互不冲突（`V0.x.x` / `AdminWasm-V0.x.x` / `WebH5-V0.x.x`）

## 六、验收标准

| 验收项 | 验收条件 | 验收方式 | 结果 |
|--------|---------|---------|------|
| 编译 | `dotnet build src/XiaoShuTong.AdminWasm/...csproj` 0 错误 | 自动化 | ✅ |
| MinVer 派生 | AdminWasm 程序集版本 = 0.1.0（b3bd020） | 构建产物 AssemblyInfo 核验 | ✅ |
| 版本线隔离 | WebApi 程序集版本 = 0.3.3-preview 系（不受 AdminWasm tag 影响） | 构建产物 AssemblyInfo 核验 | ✅ |
| 改名完整性 | 全仓库（除 bin/obj/.git）无 `XiaoShuTong.Wasm` 残留 | grep | ✅ |
| 解决方案引用 | `XiaoShuTong.slnx` → `src/XiaoShuTong.AdminWasm/XiaoShuTong.AdminWasm.csproj` | 人工核验 | ✅ |
| 静态资源 | index.html 引用的 `_content/AntDesign*` CSS/JS 路径正确 | 人工核验 | ✅ |

## 七、风险与对策

| 风险 | 影响 | 对策 |
|------|------|------|
| ProLayout 1.6.2 无显式 net10 TFM | 运行时兼容风险 | 本次构建实测通过；如遇运行时异常，对照 pro-components 源码或降级 1.5.x |
| TKWFDeployPath 环境变量缺失 | Dll 模式编译失败 | `Directory.Build.props` 已有 `_TKWF_ValidateDeployPath` 提前报错；新终端需 setx |
| 会话过期与未登录混淆 | 登录体验混乱 | SessionExpiredState 区分 OnAuthRequired vs 首次访问，沿用 DMP 验证模式 |
| menu.json 占位路径指向 "/" | 菜单点击回首页 | 待真实页面落地时同步改 menu.json（已留结构注释） |

## 变更记录

| 日期 | 版本 | 变更内容 |
|------|------|---------|
| 2026-09-10 | AdminWasm-V0.1.0 | 初始版本（AntDesign Blazor Pro 脚手架迁移 + 改名 XiaoShuTong.AdminWasm + 独立版本线 + tag 发布） |