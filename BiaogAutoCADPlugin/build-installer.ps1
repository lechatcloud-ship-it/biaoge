# ============================================================
# 标哥AutoCAD插件 - 编译安装程序到dist/
# 用于日常开发：编译安装程序并直接输出到dist/根目录
# ============================================================

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "编译安装程序到 dist/" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Split-Path -Parent $PSCommandPath
$installerProject = Join-Path $projectRoot "Installer"
$distPath = Join-Path $projectRoot "dist"

# 检查Installer项目
if (-not (Test-Path $installerProject)) {
    Write-Host "✗ 错误：找不到 Installer 项目" -ForegroundColor Red
    exit 1
}

# 检查dist目录
if (-not (Test-Path $distPath)) {
    Write-Host "✗ 错误：找不到 dist 目录" -ForegroundColor Red
    Write-Host "  请先运行 build-bundle.bat 构建插件" -ForegroundColor Yellow
    exit 1
}

# 检查BiaogPlugin.bundle
$bundlePath = Join-Path $distPath "BiaogPlugin.bundle"
if (-not (Test-Path $bundlePath)) {
    Write-Host "✗ 错误：找不到 BiaogPlugin.bundle" -ForegroundColor Red
    Write-Host "  请先运行 build-bundle.bat 构建插件" -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/3] 检查完成" -ForegroundColor Green
Write-Host "  ✓ Installer-GUI 项目存在" -ForegroundColor Gray
Write-Host "  ✓ dist/ 目录存在" -ForegroundColor Gray
Write-Host "  ✓ BiaogPlugin.bundle 存在" -ForegroundColor Gray
Write-Host ""

# 编译安装程序
Write-Host "[2/3] 编译安装程序" -ForegroundColor Yellow
Write-Host ""

Push-Location $installerProject

Write-Host "清理旧的编译输出..." -ForegroundColor Gray
if (Test-Path "bin") { Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "obj") { Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "正在编译为单文件exe..." -ForegroundColor Gray
$buildOutput = dotnet publish -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishReadyToRun=true `
    /p:DebugType=none `
    /p:DebugSymbols=false 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ 编译失败！" -ForegroundColor Red
    Write-Host $buildOutput
    Pop-Location
    exit 1
}

# 查找编译后的exe
$exePath = "bin\Release\net8.0-windows\win-x64\publish\标哥AutoCAD插件安装程序.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "✗ 错误：找不到编译后的exe文件" -ForegroundColor Red
    Write-Host "  期望位置: $exePath" -ForegroundColor Yellow
    Pop-Location
    exit 1
}

$exeInfo = Get-Item $exePath
$exeSize = [math]::Round($exeInfo.Length / 1MB, 2)

Write-Host "✓ 编译成功" -ForegroundColor Green
Write-Host "  文件大小: $exeSize MB" -ForegroundColor Cyan
Write-Host "  修改时间: $($exeInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan
Write-Host ""

Pop-Location

# 复制到dist/
Write-Host "[3/3] 复制到 dist/" -ForegroundColor Yellow
Write-Host ""

$destExe = Join-Path $distPath "安装程序.exe"

# 删除旧的安装程序（如果存在）
if (Test-Path $destExe) {
    Write-Host "删除旧的安装程序..." -ForegroundColor Gray
    Remove-Item $destExe -Force
}

# 复制新的安装程序
$sourceExe = Join-Path $installerProject $exePath
Copy-Item -Path $sourceExe -Destination $destExe -Force

# 验证
$destInfo = Get-Item $destExe
$destSize = [math]::Round($destInfo.Length / 1MB, 2)

Write-Host "✓ 复制成功" -ForegroundColor Green
Write-Host "  目标位置: $destExe" -ForegroundColor Cyan
Write-Host "  文件大小: $destSize MB" -ForegroundColor Cyan
Write-Host "  修改时间: $($destInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan
Write-Host ""

# 显示dist/目录结构
Write-Host "============================================" -ForegroundColor Green
Write-Host "✓ 构建完成！" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

Write-Host "dist/ 目录结构:" -ForegroundColor Cyan
Write-Host ""

Get-ChildItem -Path $distPath | Sort-Object PSIsContainer -Descending, Name | ForEach-Object {
    if ($_.PSIsContainer) {
        $itemSize = [math]::Round((Get-ChildItem -Path $_.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
        Write-Host "  📁 $($_.Name)\" -NoNewline -ForegroundColor Yellow
        Write-Host " ($itemSize MB)" -ForegroundColor Gray
    } else {
        $itemSize = [math]::Round($_.Length / 1MB, 2)
        if ($_.Name -eq "安装程序.exe") {
            Write-Host "  🚀 $($_.Name)" -NoNewline -ForegroundColor Green
            Write-Host " ($itemSize MB) ✅ 最新" -ForegroundColor Cyan
        } else {
            Write-Host "  📄 $($_.Name)" -NoNewline -ForegroundColor White
            Write-Host " ($itemSize MB)" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "用户使用流程:" -ForegroundColor Yellow
Write-Host "  1. 将整个 dist/ 文件夹分发给客户" -ForegroundColor White
Write-Host "  2. 客户双击运行 '安装程序.exe'" -ForegroundColor White
Write-Host "  3. 点击'开始安装'按钮" -ForegroundColor White
Write-Host "  4. 安装程序会智能检测所有AutoCAD版本" -ForegroundColor White
Write-Host "  5. 自动安装到统一位置，所有版本共享" -ForegroundColor White
Write-Host "  6. 重启AutoCAD即可使用" -ForegroundColor White
Write-Host ""

Write-Host "安装程序智能功能:" -ForegroundColor Yellow
Write-Host "  ✓ 自动检测AutoCAD 2018-2024所有版本" -ForegroundColor Green
Write-Host "  ✓ 通过注册表智能查找安装路径" -ForegroundColor Green
Write-Host "  ✓ 降级方案：扫描C/D/E/F盘常见路径" -ForegroundColor Green
Write-Host "  ✓ 统一安装位置，多版本共享" -ForegroundColor Green
Write-Host "  ✓ 用户只需点击'安装'/'卸载'按钮" -ForegroundColor Green
Write-Host ""
