<#
.SYNOPSIS
  Schema 导出 + GraphQL TypeScript 代码生成管线。
  横跨 WebApi（生产者）和前端项目（消费者），放在解决方案根目录。

.PARAMETER Quick
  跳过 WebApi 启动，假设 schema.graphql 已存在，直接跑 codegen。

.PARAMETER Watch
  graphql-codegen watch 模式。

.PARAMETER Validate
  生成后检查 src/gql/ 下有无未提交变更（CI 用）。

.EXAMPLE
  .\buildSchema.ps1           # 完整流程
  .\buildSchema.ps1 -Quick    # 仅 codegen
  .\buildSchema.ps1 -Watch    # 监听
  .\buildSchema.ps1 -Validate # CI 验证
#>

param(
  [switch]$Quick,
  [switch]$Watch,
  [switch]$Validate
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot

$WebApiProject    = "$ScriptDir\src\XiaoShuTong.WebApi"
$SchemaFile       = "$WebApiProject\schema.graphql"

# 自动检测前端目录（用于 graphql-codegen TypeScript 生成）
# 优先 AdminWeb（独立前端/React/Vue），无则回退到 WebApi 自身
# 注意：Wasm（Blazor Wasm）是 C# 项目，不需要 TypeScript codegen，不纳入候选
$FrontendDir = $null
$frontendCandidates = @(
    "$ScriptDir\src\XiaoShuTong.AdminWeb"   # 独立前端（React/Vue）
)
foreach ($c in $frontendCandidates) {
    if (Test-Path $c) { $FrontendDir = $c; break }
}
if (-not $FrontendDir) {
    $FrontendDir = $WebApiProject  # 无前端项目，回退到 WebApi 自身
}
$isFrontendWebApi = ($FrontendDir -eq $WebApiProject)
$GeneratedDir     = "src/gql"
$CodegenConfig    = "$WebApiProject\codegen.yml"   # codegen.yml 归 WebApi 项目（schema 生产者）
$CodegenTemplate  = "$env:TKWF_FRAMEWORK_PATH\docs\AC-Kit\scaffolding\WebUI\codegen.yml.template"

# ---------- helpers ----------

function Write-Info($msg)  { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Write-Err($msg)  { Write-Host "[ERR]  $msg" -ForegroundColor Red; exit 1 }

# 确保 codegen.yml 存在（自愈：从 AC-Kit 模板创建，替换 XiaoShuTong 变量）
function Ensure-CodegenConfig {
    if (Test-Path $CodegenConfig) {
        Write-Info "codegen.yml 已存在，跳过创建"
        return
    }
    if (-not (Test-Path $CodegenTemplate)) {
        Write-Err "codegen.yml 模板不存在: $CodegenTemplate"
    }
    Write-Info "从模板创建 codegen.yml..."
    $content = (Get-Content $CodegenTemplate -Encoding UTF8 -Raw) -replace '\{Name\}', 'XiaoShuTong'
    # 如果前端目录是 WebApi 自身，调整 schema 路径为相对路径
    if ($isFrontendWebApi) {
        $content = $content -replace "schema: '.*'", "schema: './schema.graphql'"
    }
    $content | Set-Content $CodegenConfig -NoNewline
    Write-Ok "已创建: $CodegenConfig"
}

# 确保 package.json 包含 codegen script（自愈）
function Ensure-NpmScript {
    $pkgFile = "$FrontendDir\package.json"
    if (-not (Test-Path $pkgFile)) {
        Write-Info "package.json 不存在，正在初始化 npm..."
        Push-Location $FrontendDir
        try {
            npm init -y 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Err "npm init 失败" }
            npm install --save-dev graphql @graphql-codegen/cli @graphql-codegen/typescript @graphql-codegen/typescript-operations 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Err "npm install 失败" }
            if (-not (Test-Path "$FrontendDir\node_modules\graphql")) {
                Write-Err "graphql 包未安装，npm install 可能不完整"
            }
            Write-Ok "npm 初始化完成"
        } finally { Pop-Location }
    }
    $pkg = Get-Content $pkgFile -Encoding UTF8 -Raw | ConvertFrom-Json
    if (-not $pkg.scripts) { $pkg | Add-Member -NotePropertyName "scripts" -NotePropertyValue ([ordered]@{}) -Force }
    if ($pkg.scripts.codegen) {
        Write-Info "npm script 'codegen' 已存在，跳过"
        return
    }
    Write-Info "添加 npm script 'codegen'..."
    $pkg.scripts | Add-Member -NotePropertyName "codegen" -NotePropertyValue "graphql-codegen --config `"$CodegenConfig`"" -Force
    $pkg.scripts | Add-Member -NotePropertyName "codegen:watch" -NotePropertyValue "graphql-codegen --watch --config `"$CodegenConfig`"" -Force
    $pkg | ConvertTo-Json -Depth 10 | Set-Content $pkgFile -NoNewline
    Write-Ok "npm script 'codegen' 已添加"
}

# 后台进程管理已移除 — 改用 HotChocolate.AspNetCore.CommandLine
# dotnet run -- schema export 同步执行，无需进程管理

# ---------- Validate ----------

if ($Validate) {
  Write-Info "Validate: generating and checking for changes..."
  & $PSCommandPath -Quick
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

  Push-Location $FrontendDir
  $diffOutput = git diff --stat -- $GeneratedDir 2>&1
  Pop-Location

  if ($LASTEXITCODE -ne 0) { Write-Err "git diff failed. Are you in a git repository?" }
  if ([string]::IsNullOrWhiteSpace($diffOutput)) {
    Write-Ok "Generated files are up to date."
    exit 0
  } else {
    Write-Host "`n$diffOutput`n" -ForegroundColor Yellow
    Write-Err "Generated files in $GeneratedDir are not up to date. Run '.\buildSchema.ps1 -Quick' and commit."
  }
}

# ---------- Watch ----------

if ($Watch) {
  Write-Info "Watch mode: starting graphql-codegen watcher..."
  Push-Location $FrontendDir
  npm run codegen:watch
  Pop-Location
  exit $LASTEXITCODE
}

# ---------- Quick (codegen only) ----------

if ($Quick) {
  if (-not (Test-Path $SchemaFile)) {
    Write-Err "schema.graphql not found at $SchemaFile. Run without -Quick to export it."
  }
  # 自愈：确保 codegen 配置就绪
  Ensure-CodegenConfig
  Ensure-NpmScript
  Write-Info "Quick mode: codegen..."
  Push-Location $FrontendDir
  try {
    npm run codegen
    if ($LASTEXITCODE -ne 0) { Write-Err "graphql-codegen failed." }
  } finally { Pop-Location }

  $files = Get-ChildItem -Path "$FrontendDir\$GeneratedDir" -Recurse -File -ErrorAction SilentlyContinue
  Write-Ok "Codegen complete. $($files.Count) files generated."
  exit 0
}

# ---------- Full mode (default) ----------

Write-Info "=== Schema Export: Full Mode ==="

# 1. Build WebApi
Write-Info "Building WebApi..."
dotnet build $WebApiProject --nologo -q
if ($LASTEXITCODE -ne 0) { Write-Err "WebApi build failed." }
Write-Ok "WebApi build succeeded."

# 2. Export schema via HotChocolate.AspNetCore.CommandLine (no Kestrel, no DB)
Write-Info "Exporting schema.graphql via CommandLine..."
Remove-Item $SchemaFile -ErrorAction Ignore
dotnet run --project $WebApiProject -- schema export --output $SchemaFile
if ($LASTEXITCODE -ne 0) { Write-Err "Schema export failed." }
if (-not (Test-Path $SchemaFile)) { Write-Err "schema.graphql not found after export." }
Write-Ok "schema.graphql exported."

# 3. 确保 codegen 配置就绪（自愈：codegen.yml + npm script）
Write-Info "Ensuring codegen config..."
Ensure-CodegenConfig
Ensure-NpmScript
Write-Ok "codegen config ready."

# 4. graphql-codegen
Write-Info "Running graphql-codegen..."
Push-Location $FrontendDir
try {
  npm run codegen
  if ($LASTEXITCODE -ne 0) { Write-Err "graphql-codegen failed." }
  Write-Ok "graphql-codegen OK."
} finally { Pop-Location }

Write-Ok "=== Schema export complete ==="
