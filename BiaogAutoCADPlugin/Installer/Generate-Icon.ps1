# Generate-Icon.ps1 - 从PNG源图生成Windows 11标准多尺寸图标
# 用法: .\Generate-Icon.ps1 -SourcePng "source.png" -OutputIco "icon.ico"

param(
    [Parameter(Mandatory=$true, HelpMessage="PNG源图文件路径（建议512x512或更大）")]
    [string]$SourcePng,

    [Parameter(Mandatory=$false)]
    [string]$OutputIco = "icon-new.ico"
)

# 检查源文件
if (-not (Test-Path $SourcePng)) {
    Write-Host "❌ 错误: 找不到源图文件 $SourcePng" -ForegroundColor Red
    exit 1
}

# 加载所需程序集
Add-Type -AssemblyName System.Drawing

Write-Host "`n╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Windows 11 图标生成工具（多尺寸）        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host "`n📥 加载源图..." -ForegroundColor Yellow

# 加载源图像
try {
    $sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path $SourcePng).Path)
    Write-Host "   ✅ 源图: ${SourcePng}" -ForegroundColor Green
    Write-Host "   尺寸: $($sourceImage.Width)x$($sourceImage.Height)" -ForegroundColor Gray
    Write-Host "   格式: $($sourceImage.RawFormat.Guid)" -ForegroundColor Gray
}
catch {
    Write-Host "   ❌ 无法加载图像: $_" -ForegroundColor Red
    exit 1
}

# 检查源图尺寸
if ($sourceImage.Width -lt 256 -or $sourceImage.Height -lt 256) {
    Write-Host "`n⚠️  警告: 源图尺寸小于256x256，可能导致质量下降" -ForegroundColor Yellow
    Write-Host "   推荐: 使用512x512或更大的PNG源图" -ForegroundColor Gray
}

# Windows 11 标准尺寸
$sizes = @(16, 24, 32, 48, 64, 96, 128, 256)

Write-Host "`n🎨 生成图标尺寸..." -ForegroundColor Yellow
Write-Host ""

# 创建内存流
$iconStream = New-Object System.IO.MemoryStream
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)

# 写入ICO文件头
$iconWriter.Write([UInt16]0)                # Reserved (must be 0)
$iconWriter.Write([UInt16]1)                # Type: 1 = ICO
$iconWriter.Write([UInt16]$sizes.Length)    # Number of images

# 准备目录条目和图像数据
$imageDataList = New-Object System.Collections.ArrayList
$directoryEntries = New-Object System.Collections.ArrayList

Write-Host "   尺寸        大小       状态" -ForegroundColor Gray
Write-Host "   ────────────────────────────────" -ForegroundColor Gray

foreach ($size in $sizes) {
    try {
        # 创建缩放后的图像
        $resizedImage = New-Object System.Drawing.Bitmap($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($resizedImage)

        # 使用高质量插值算法
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # 绘制缩放后的图像
        $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
        $graphics.Dispose()

        # 保存为PNG格式（保留透明度）
        $pngStream = New-Object System.IO.MemoryStream
        $resizedImage.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData = $pngStream.ToArray()
        $pngStream.Dispose()
        $resizedImage.Dispose()

        # 保存图像数据
        $null = $imageDataList.Add($pngData)

        # 创建目录条目
        $entry = @{
            Width = if ($size -eq 256) { 0 } else { $size }  # 256用0表示
            Height = if ($size -eq 256) { 0 } else { $size }
            ColorCount = 0              # 0 = 不使用调色板
            Reserved = 0
            ColorPlanes = 1
            BitsPerPixel = 32           # 32位色深（RGBA）
            ImageSize = $pngData.Length
            ImageOffset = 0             # 稍后计算
        }
        $null = $directoryEntries.Add($entry)

        # 格式化输出
        $sizeStr = "${size}x${size}".PadRight(10)
        $sizeKB = [math]::Round($pngData.Length / 1KB, 2)
        $sizeKBStr = "$sizeKB KB".PadRight(10)
        Write-Host "   $sizeStr $sizeKBStr ✅" -ForegroundColor Green
    }
    catch {
        Write-Host "   ${size}x${size}     ❌ 失败: $_" -ForegroundColor Red
    }
}

# 计算每个图像的偏移量
$currentOffset = 6 + ($sizes.Length * 16)  # Header (6 bytes) + Directory entries (16 bytes each)

for ($i = 0; $i -lt $directoryEntries.Count; $i++) {
    $directoryEntries[$i].ImageOffset = $currentOffset
    $currentOffset += $directoryEntries[$i].ImageSize
}

# 写入所有目录条目
Write-Host "`n📝 写入ICO文件头..." -ForegroundColor Yellow
foreach ($entry in $directoryEntries) {
    $iconWriter.Write([Byte]$entry.Width)
    $iconWriter.Write([Byte]$entry.Height)
    $iconWriter.Write([Byte]$entry.ColorCount)
    $iconWriter.Write([Byte]$entry.Reserved)
    $iconWriter.Write([UInt16]$entry.ColorPlanes)
    $iconWriter.Write([UInt16]$entry.BitsPerPixel)
    $iconWriter.Write([UInt32]$entry.ImageSize)
    $iconWriter.Write([UInt32]$entry.ImageOffset)
}

# 写入所有图像数据
Write-Host "📦 写入图像数据..." -ForegroundColor Yellow
foreach ($imageData in $imageDataList) {
    $iconWriter.Write($imageData)
}

# 保存到文件
Write-Host "💾 保存图标文件..." -ForegroundColor Yellow
$iconWriter.Flush()
$iconBytes = $iconStream.ToArray()

try {
    [System.IO.File]::WriteAllBytes((Resolve-Path -Path . | Join-Path -ChildPath $OutputIco), $iconBytes)

    $fileSize = [math]::Round($iconBytes.Length / 1KB, 2)

    Write-Host "`n╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║              ✅ 图标生成成功！                ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan

    Write-Host "`n📄 输出文件" -ForegroundColor Yellow
    Write-Host "   文件: $OutputIco" -ForegroundColor Gray
    Write-Host "   大小: $fileSize KB" -ForegroundColor Gray
    Write-Host "   包含: $($sizes.Count) 个尺寸 ($($sizes -join ', '))" -ForegroundColor Gray

    Write-Host "`n🔍 下一步" -ForegroundColor Yellow
    Write-Host "   1. 运行检查脚本验证:" -ForegroundColor Gray
    Write-Host "      .\Check-Icon.ps1 -IconPath `"$OutputIco`"" -ForegroundColor Cyan
    Write-Host "`n   2. 如果合格，替换当前图标:" -ForegroundColor Gray
    Write-Host "      mv icon.ico icon-backup.ico" -ForegroundColor Cyan
    Write-Host "      mv $OutputIco icon.ico" -ForegroundColor Cyan
    Write-Host "`n   3. 重新编译安装程序" -ForegroundColor Gray
    Write-Host "      dotnet publish -c Release -r win-x64 ..." -ForegroundColor Cyan

    Write-Host ""
}
catch {
    Write-Host "`n❌ 保存失败: $_" -ForegroundColor Red
    exit 1
}
finally {
    # 清理资源
    $iconWriter.Dispose()
    $iconStream.Dispose()
    $sourceImage.Dispose()
}
