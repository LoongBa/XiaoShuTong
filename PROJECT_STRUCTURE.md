# 项目结构（XiaoShuTong）

```
XiaoShuTong/
├── Agents_Use_TKWF.md              ← OpenCode 自动加载的开发规则
├── .opencode/opencode.json         ← OpenCode 配置（instructions 加载规则文件）
├── PROJECT_STRUCTURE.md            ← 本文件（活文档，目录结构变化时同步更新）
├── .TKWF/
│   ├── TKWF-Rules.md               ← 增量开发路由（需求分层、Skill 路由、红线）
│   ├── xCodeGen/                   ← 生成配置
│   └── {域}/                       ← 域活态文档（gitignore，构建后生成）
├── XiaoShuTong.Dev.slnx                 ← ProjectRef 模式（联调 TKWF 源码）
├── XiaoShuTong.slnx                     ← Dll 模式（引用 build\refs\ 预编译 DLL）
├── Directory.Build.props           ← TkwfReferenceMode 等
├── XiaoShuTong.Domain/                  ← 领域层
├── XiaoShuTong.WebApi/                  ← .NET WebApi
├── XiaoShuTong.AdminWeb/                ← 前端 SPA
├── build.ps1 / buildSchema.ps1     ← 构建/生成工具
├── scripts/                        ← 脚本
└── docs/                           ← 项目文档
```