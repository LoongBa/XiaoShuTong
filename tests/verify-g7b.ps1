# ═══════════════════════════════════════════════════════
# G7b 修复验证脚本（框架组部署 v4.10.19+ 后运行）
# 用途：检测框架 G7b 修复是否就位 + 全套件回归
# 用法：pwsh -NoProfile -File verify-g7b.ps1
# ═══════════════════════════════════════════════════════
$ErrorActionPreference = "Stop"
$env:TKWFDeployPath = "F:\TKWF_FRAMEWORK_PATH"
$repo = "F:\LoongBa_Git\XiaoShuTong"

Write-Host "=== G7b 修复验证（DateTime? nullable TypeHandler）===" -ForegroundColor Cyan

# ── 1. 部署 DLL 特征检测 ──
$dll = "$env:TKWFDeployPath\build\refs\TKWF.Domain.FreeSql.dll"
if (-not (Test-Path $dll)) { Write-Host "✗ DLL 不存在: $dll" -ForegroundColor Red; exit 1 }
$ver = (Get-Item $dll).VersionInfo.ProductVersion
$ts = (Get-Item $dll).LastWriteTime
Write-Host "部署 DLL: $ver ($ts)" -ForegroundColor Yellow

$bytes = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
$hasNullableHandler = $text.Contains("DateTimeUtcNullableHandler")
$hasDateTimeHandler = $text.Contains("DateTimeUtcHandler")

Write-Host "DateTimeUtcHandler (G7): $hasDateTimeHandler"
Write-Host "DateTimeUtcNullableHandler (G7b): $hasNullableHandler"

if (-not $hasNullableHandler)
{
    Write-Host "✗ G7b 未修复：部署 DLL 缺 DateTimeUtcNullableHandler（当前 $ver）" -ForegroundColor Red
    Write-Host "  转交材料：$repo\docs\草稿\框架组转交摘要-G7b-DateTime-nullable-TypeHandler.md"
    Write-Host "  等待框架组部署 v4.10.19+ 后重跑本脚本"
    exit 1
}
Write-Host "✓ G7b 修复特征就位" -ForegroundColor Green

# ── 2. 框架源码确认（可选）──
$src = "$env:TKWFDeployPath\..\_TKWF"  # 若框架源码在此
if (Test-Path "$src\_Domain.Infrastructure\FreeSql\SqliteTypeHandlerRegistrar.cs")
{
    $hasSrc = Select-String -Path "$src\_Domain.Infrastructure\FreeSql\SqliteTypeHandlerRegistrar.cs" -Pattern "typeof\(DateTime\?\)" -Quiet
    Write-Host "框架源码 nullable 注册: $hasSrc"
}

# ── 3. 全套件回归 ──
Write-Host "=== 全套件回归 ===" -ForegroundColor Cyan
dotnet test "$repo\tests\XiaoShuTong.Tests\XiaoShuTong.Tests.csproj" -c Debug --nologo 2>&1 | Select-String -Pattern "已通过|失败:|\[FAIL\]" | ForEach-Object { $_.Line.Trim() }

Write-Host "=== 完成 ===" -ForegroundColor Green
Write-Host "预期：345 通过 / 0 失败 / 2 跳过（G7b 7 用例清零）"
