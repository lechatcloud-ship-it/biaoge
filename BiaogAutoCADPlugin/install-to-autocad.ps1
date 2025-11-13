# 标哥AutoCAD插件 - 自动安装脚本

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  标哥AutoCAD插件 - 自动安装" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# 源目录（编译输出）
$sourceDir = "C:\Users\weiyou\Desktop\biaoge\BiaogAutoCADPlugin\src\BiaogPlugin\bin\Release"

# 目标目录（AutoCAD插件目录）
$targetBundle = "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents\Windows\2024"

# 创建目录结构
Write-Host "步骤 1/3: 创建bundle目录..." -ForegroundColor Yellow
$bundleRoot = "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle"
if (!(Test-Path $bundleRoot)) {
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
}
if (!(Test-Path $targetBundle)) {
    New-Item -ItemType Directory -Path $targetBundle -Force | Out-Null
}
Write-Host "  ✓ 目录创建成功" -ForegroundColor Green

# 复制PackageContents.xml
Write-Host ""
Write-Host "步骤 2/3: 复制配置文件..." -ForegroundColor Yellow
$packageXml = "C:\Users\weiyou\Desktop\biaoge\BiaogAutoCADPlugin\PackageContents.xml"
if (Test-Path $packageXml) {
    Copy-Item $packageXml $bundleRoot -Force
    Write-Host "  ✓ PackageContents.xml" -ForegroundColor Green
}

# 复制所有DLL文件
Write-Host ""
Write-Host "步骤 3/3: 复制插件文件..." -ForegroundColor Yellow
Get-ChildItem -Path $sourceDir -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName $targetBundle -Force
    Write-Host "  ✓ $($_.Name)" -ForegroundColor Green
}

# 复制PDB文件（用于调试）
Get-ChildItem -Path $sourceDir -Filter "*.pdb" | ForEach-Object {
    Copy-Item $_.FullName $targetBundle -Force
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  ✓ 安装完成！" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "安装位置: $targetBundle" -ForegroundColor White
Write-Host ""
Write-Host "下一步操作：" -ForegroundColor Yellow
Write-Host "  1. 重启AutoCAD" -ForegroundColor White
Write-Host "  2. 插件会自动加载" -ForegroundColor White
Write-Host "  3. 运行 BIAOGE_VERSION 验证版本" -ForegroundColor White
Write-Host "  4. 运行 BIAOGE_KEYS 安装快捷键" -ForegroundColor White
Write-Host ""
Write-Host "新功能测试：" -ForegroundColor Yellow
Write-Host "  • BIAOGE_TOGGLE_TRANSLATE - 切换翻译面板" -ForegroundColor White
Write-Host "  • BIAOGE_TOGGLE_CALCULATE - 切换算量面板" -ForegroundColor White
Write-Host "  • BIAOGE_TOGGLE_AI - 切换AI助手面板" -ForegroundColor White
Write-Host "  • BIAOGE_KEYS - 打开快捷键管理对话框" -ForegroundColor White
Write-Host ""
