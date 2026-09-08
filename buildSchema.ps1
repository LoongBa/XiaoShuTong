<#
.SYNOPSIS
  Schema 导出 + TKWF ts-client 代码生成管线。
  横跨 WebApi（生产者）和前端项目（消费者），放在解决方案根目录。

  管线：dotnet schema export → schema.graphql → 前端 npm run gen-ts-client → ts-client.g.ts + GraphQL_Api.md
  （对齐框架新版 buildSchema.ps1：gen-ts-client 为唯一 codegen，graphql-codegen 已废弃）

.PARAMETER Quick
  跳过 WebApi 导出，假设 schema.graphql 已存在，直接跑 gen-ts-client。

.PARAMETER Validate
  生成后检查前端 src/gql/ 下有无未提交变更（CI 用）。

.EXAMPLE
  .\buildSchema.ps1           # 完整流程（导出 schema + gen-ts-client）
  .\buildSchema.ps1 -Quick    # 仅 gen-ts-client（schema 已存在）
  .\buildSchema.ps1 -Validate # CI 验证
#>

param(
  [switch]$Quick,
  [switch]$Validate
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot

$WebApiProject = "$ScriptDir\src\XiaoShuTong.WebApi"
$SchemaFile    = "$WebApiProject\schema.graphql"

# 前端目录（gen-ts-client 消费端）——WebH5
$FrontendDir = "$ScriptDir\src\XiaoShuTong.WebH5"
if (-not (Test-Path "$FrontendDir\package.json")) {
  Write-Err "前端目录未找到: $FrontendDir（无 package.json）"
}
$GeneratedDir = "src/gql"

# ---------- helpers ----------

function Write-Info($msg)  { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Write-Err($msg)  { Write-Host "[ERR]  $msg" -ForegroundColor Red; exit 1 }

# 确保前端 package.json 含 gen-ts-client script（自愈）
function Ensure-NpmScript {
  $pkgFile = "$FrontendDir\package.json"
  if (-not (Test-Path $pkgFile)) {
    Write-Err "package.json 不存在: $pkgFile"
  }
  $pkg = Get-Content $pkgFile -Encoding UTF8 -Raw | ConvertFrom-Json
  if ($pkg.scripts.'gen-ts-client') {
    Write-Info "npm script 'gen-ts-client' 已存在，跳过"
    return
  }
  Write-Info "添加 npm script 'gen-ts-client'..."
  if (-not $pkg.scripts) { $pkg | Add-Member -NotePropertyName "scripts" -NotePropertyValue ([ordered]@{}) -Force }
  $pkg.scripts | Add-Member -NotePropertyName "gen-ts-client" -NotePropertyValue "tsx scripts/gen-ts-client.ts" -Force
  $pkg | ConvertTo-Json -Depth 10 | Set-Content $pkgFile -NoNewline
  Write-Ok "npm script 'gen-ts-client' 已添加"
}

# 运行 gen-ts-client（在 WebH5 目录）
function Invoke-GenTsClient {
  Write-Info "Running gen-ts-client..."
  Push-Location $FrontendDir
  try {
    npm run gen-ts-client
    if ($LASTEXITCODE -ne 0) { Write-Err "gen-ts-client failed (exit $LASTEXITCODE)." }
  } finally { Pop-Location }

  $files = Get-ChildItem -Path "$FrontendDir\$GeneratedDir" -Recurse -File -ErrorAction SilentlyContinue
  Write-Ok "gen-ts-client complete. $($files.Count) files generated in $GeneratedDir."
}

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

# ---------- Quick (gen-ts-client only) ----------

if ($Quick) {
  if (-not (Test-Path $SchemaFile)) {
    Write-Err "schema.graphql not found at $SchemaFile. Run without -Quick to export it."
  }
  Ensure-NpmScript
  Invoke-GenTsClient
  exit 0
}

# ---------- Full mode (default) ----------

Write-Info "=== Schema Export: Full Mode ==="

# 1. Build WebApi
Write-Info "Building WebApi..."
dotnet build $WebApiProject --nologo -q
if ($LASTEXITCODE -ne 0) {
  if (Test-Path $SchemaFile) {
    Write-Host "[WARN] WebApi build 失败（多为 obj/SDK 环境问题），schema.graphql 已存在，继续使用现有 schema。" -ForegroundColor Yellow
  } else {
    Write-Err "WebApi build failed 且 schema.graphql 不存在，无法继续。"
  }
} else {
  Write-Ok "WebApi build succeeded."
}

# 2. Export schema via HotChocolate.AspNetCore.CommandLine (no Kestrel, no DB)
Write-Info "Exporting schema.graphql via CommandLine..."
Remove-Item $SchemaFile -ErrorAction Ignore
dotnet run --project $WebApiProject -- schema export --output $SchemaFile
if ($LASTEXITCODE -ne 0) { Write-Err "Schema export failed." }
if (-not (Test-Path $SchemaFile)) { Write-Err "schema.graphql not found after export." }
Write-Ok "schema.graphql exported."

# 3. gen-ts-client（ts-client.g.ts + GraphQL_Api.md）
Ensure-NpmScript
Invoke-GenTsClient

Write-Ok "=== Schema export complete ==="
