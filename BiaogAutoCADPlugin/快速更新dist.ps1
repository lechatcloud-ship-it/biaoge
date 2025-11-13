# 快速更新 dist/BiaogPlugin.bundle

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "快速更新 dist/BiaogPlugin.bundle" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $PSCommandPath
$src = Join-Path $scriptPath "src\BiaogPlugin\bin\Release"
$dest2021 = Join-Path $scriptPath "dist\BiaogPlugin.bundle\Contents\2021"
$dest2018 = Join-Path $scriptPath "dist\BiaogPlugin.bundle\Contents\2018"

Write-Host "正在复制最新编译文件..." -ForegroundColor Yellow
Write-Host ""

# 确保目标目录存在
New-Item -ItemType Directory -Force -Path $dest2021 | Out-Null
New-Item -ItemType Directory -Force -Path $dest2018 | Out-Null

Write-Host "[1/2] 复制到 2021 目录..." -ForegroundColor Yellow
try {
    Copy-Item -Path "$src\*" -Destination $dest2021 -Recurse -Force
    Write-Host "✓ 完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 失败: $($_.Exception.Message)" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "[2/2] 复制到 2018 目录..." -ForegroundColor Yellow
try {
    Copy-Item -Path "$dest2021\*" -Destination $dest2018 -Recurse -Force
    Write-Host "✓ 完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 失败: $($_.Exception.Message)" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✓ 更新完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "最新DLL已复制到：" -ForegroundColor Cyan
Write-Host "  - dist\BiaogPlugin.bundle\Contents\2021\" -ForegroundColor White
Write-Host "  - dist\BiaogPlugin.bundle\Contents\2018\" -ForegroundColor White
Write-Host ""

# 显示DLL文件信息
$dll2021 = Join-Path $dest2021 "BiaogPlugin.dll"
if (Test-Path $dll2021) {
    $info = Get-Item $dll2021
    Write-Host "DLL文件信息：" -ForegroundColor Cyan
    Write-Host "  修改时间: $($info.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
    Write-Host "  文件大小: $([math]::Round($info.Length / 1KB, 2)) KB" -ForegroundColor White
}

Write-Host ""
pause
