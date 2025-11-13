# Generate-BiaogeLogo.ps1 - 生成"标哥"品牌图标
# 红色背景 + 白色"标"字 + Windows 11 Fluent Design风格

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputIco = "icon.ico"
)

Add-Type -AssemblyName System.Drawing

Write-Host "`n╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         生成标哥AutoCAD插件图标              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host "`n🎨 设计规格" -ForegroundColor Yellow
Write-Host "   主题: 标哥品牌" -ForegroundColor Gray
Write-Host "   背景: 红色渐变 (#E74C3C → #C0392B)" -ForegroundColor Gray
Write-Host "   文字: 白色 '标' 字" -ForegroundColor Gray
Write-Host "   风格: Windows 11 Fluent Design" -ForegroundColor Gray
Write-Host "   尺寸: 8个标准尺寸 (16-256)" -ForegroundColor Gray

# Windows 11 标准尺寸
$sizes = @(16, 24, 32, 48, 64, 96, 128, 256)

# 配色方案
$colorStart = [System.Drawing.Color]::FromArgb(255, 231, 76, 60)   # #E74C3C 鲜红色
$colorEnd = [System.Drawing.Color]::FromArgb(255, 192, 57, 43)     # #C0392B 深红色
$colorText = [System.Drawing.Color]::White                          # 白色文字
$colorShadow = [System.Drawing.Color]::FromArgb(80, 0, 0, 0)       # 半透明阴影

Write-Host "`n📐 生成各尺寸图标..." -ForegroundColor Yellow
Write-Host ""

# 创建内存流
$iconStream = New-Object System.IO.MemoryStream
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)

# 写入ICO文件头
$iconWriter.Write([UInt16]0)                # Reserved
$iconWriter.Write([UInt16]1)                # Type: ICO
$iconWriter.Write([UInt16]$sizes.Length)    # Image count

# 准备图像数据
$imageDataList = New-Object System.Collections.ArrayList
$directoryEntries = New-Object System.Collections.ArrayList

Write-Host "   尺寸        大小       状态" -ForegroundColor Gray
Write-Host "   ────────────────────────────────" -ForegroundColor Gray

foreach ($size in $sizes) {
    try {
        # 创建位图
        $bitmap = New-Object System.Drawing.Bitmap($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        # 设置高质量渲染
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        # 计算圆角半径（Windows 11 Fluent Design）
        $cornerRadius = [Math]::Max(2, [int]($size * 0.12))  # 12%圆角

        # 创建圆角矩形路径
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)

        # 添加圆角矩形（左上、右上、右下、左下）
        $diameter = $cornerRadius * 2
        $arc = New-Object System.Drawing.Rectangle($rect.X, $rect.Y, $diameter, $diameter)
        $path.AddArc($arc, 180, 90)  # 左上角

        $arc.X = $rect.Right - $diameter
        $path.AddArc($arc, 270, 90)  # 右上角

        $arc.Y = $rect.Bottom - $diameter
        $path.AddArc($arc, 0, 90)    # 右下角

        $arc.X = $rect.Left
        $path.AddArc($arc, 90, 90)   # 左下角

        $path.CloseFigure()

        # 创建渐变画刷（从左上到右下）
        $gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            $colorStart,
            $colorEnd,
            45.0  # 对角线渐变
        )

        # 填充圆角矩形背景
        $graphics.FillPath($gradientBrush, $path)

        # 绘制文字 "标"
        $fontSize = [int]($size * 0.55)  # 字体占55%

        # 小尺寸使用简化字体
        if ($size -le 24) {
            $fontFamily = "Microsoft YaHei UI"  # 微软雅黑UI（更清晰）
            $fontStyle = [System.Drawing.FontStyle]::Bold
        } else {
            $fontFamily = "Microsoft YaHei"     # 微软雅黑
            $fontStyle = [System.Drawing.FontStyle]::Bold
        }

        $font = New-Object System.Drawing.Font($fontFamily, $fontSize, $fontStyle, [System.Drawing.GraphicsUnit]::Pixel)

        # 测量文字尺寸
        $text = "标"
        $textSize = $graphics.MeasureString($text, $font)

        # 计算文字居中位置
        $textX = ($size - $textSize.Width) / 2
        $textY = ($size - $textSize.Height) / 2

        # 小尺寸微调（视觉居中）
        if ($size -le 32) {
            $textY -= 1
        }

        # 绘制文字阴影（增加深度感）
        if ($size -ge 32) {
            $shadowOffset = [Math]::Max(1, [int]($size * 0.02))
            $shadowBrush = New-Object System.Drawing.SolidBrush($colorShadow)
            $graphics.DrawString($text, $font, $shadowBrush, $textX + $shadowOffset, $textY + $shadowOffset)
            $shadowBrush.Dispose()
        }

        # 绘制文字（白色）
        $textBrush = New-Object System.Drawing.SolidBrush($colorText)
        $graphics.DrawString($text, $font, $textBrush, $textX, $textY)

        # 清理资源
        $textBrush.Dispose()
        $font.Dispose()
        $gradientBrush.Dispose()
        $path.Dispose()
        $graphics.Dispose()

        # 保存为PNG格式
        $pngStream = New-Object System.IO.MemoryStream
        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData = $pngStream.ToArray()
        $pngStream.Dispose()
        $bitmap.Dispose()

        # 保存图像数据
        $null = $imageDataList.Add($pngData)

        # 创建目录条目
        $entry = @{
            Width = if ($size -eq 256) { 0 } else { $size }
            Height = if ($size -eq 256) { 0 } else { $size }
            ColorCount = 0
            Reserved = 0
            ColorPlanes = 1
            BitsPerPixel = 32
            ImageSize = $pngData.Length
            ImageOffset = 0
        }
        $null = $directoryEntries.Add($entry)

        # 输出状态
        $sizeStr = "${size}x${size}".PadRight(10)
        $sizeKB = [math]::Round($pngData.Length / 1KB, 2)
        $sizeKBStr = "$sizeKB KB".PadRight(10)
        Write-Host "   $sizeStr $sizeKBStr ✅" -ForegroundColor Green
    }
    catch {
        Write-Host "   ${size}x${size}     ❌ 失败: $_" -ForegroundColor Red
    }
}

# 计算偏移量
$currentOffset = 6 + ($sizes.Length * 16)
for ($i = 0; $i -lt $directoryEntries.Count; $i++) {
    $directoryEntries[$i].ImageOffset = $currentOffset
    $currentOffset += $directoryEntries[$i].ImageSize
}

# 写入目录条目
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

# 写入图像数据
Write-Host "📦 写入图像数据..." -ForegroundColor Yellow
foreach ($imageData in $imageDataList) {
    $iconWriter.Write($imageData)
}

# 保存文件
Write-Host "💾 保存图标文件..." -ForegroundColor Yellow
$iconWriter.Flush()
$iconBytes = $iconStream.ToArray()

try {
    $outputPath = Join-Path (Get-Location) $OutputIco
    [System.IO.File]::WriteAllBytes($outputPath, $iconBytes)

    $fileSize = [math]::Round($iconBytes.Length / 1KB, 2)

    Write-Host "`n╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║            ✅ 图标生成成功！                  ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan

    Write-Host "`n📄 输出文件" -ForegroundColor Yellow
    Write-Host "   文件: $OutputIco" -ForegroundColor Gray
    Write-Host "   大小: $fileSize KB" -ForegroundColor Gray
    Write-Host "   包含: $($sizes.Count) 个尺寸" -ForegroundColor Gray
    Write-Host "   尺寸: $($sizes -join ', ')" -ForegroundColor Gray

    Write-Host "`n🎨 设计特性" -ForegroundColor Yellow
    Write-Host "   ✅ 红色渐变背景 (#E74C3C → #C0392B)" -ForegroundColor Gray
    Write-Host "   ✅ 白色'标'字，加粗显示" -ForegroundColor Gray
    Write-Host "   ✅ 圆角设计（12%圆角）" -ForegroundColor Gray
    Write-Host "   ✅ 文字阴影（增加深度）" -ForegroundColor Gray
    Write-Host "   ✅ 32位色深（RGBA）" -ForegroundColor Gray
    Write-Host "   ✅ 符合Windows 11 Fluent Design" -ForegroundColor Gray

    Write-Host "`n🔍 验证" -ForegroundColor Yellow
    Write-Host "   运行: .\Check-Icon.ps1 -IconPath `"$OutputIco`"" -ForegroundColor Cyan

    Write-Host "`n📋 下一步" -ForegroundColor Yellow
    Write-Host "   1. 在文件资源管理器中预览图标" -ForegroundColor Gray
    Write-Host "   2. 如果满意，替换旧图标:" -ForegroundColor Gray
    Write-Host "      mv icon.ico icon-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss').ico" -ForegroundColor Cyan
    Write-Host "      mv $OutputIco icon.ico" -ForegroundColor Cyan
    Write-Host "   3. 重新编译安装程序" -ForegroundColor Gray

    Write-Host ""
}
catch {
    Write-Host "`n❌ 保存失败: $_" -ForegroundColor Red
    exit 1
}
finally {
    $iconWriter.Dispose()
    $iconStream.Dispose()
}
