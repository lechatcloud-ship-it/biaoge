# Windows 11 应用程序图标最佳实践

## 🎨 当前问题

**当前图标**: `icon.ico`
- **尺寸**: 仅包含 1 个尺寸 (256x256)
- **色深**: 16 色（不够）
- **问题**:
  - ❌ 小图标/中图标显示模糊
  - ❌ 大图标/超大图标拉伸失真
  - ❌ 不符合 Windows 11 Fluent Design

---

## ✅ Windows 11 图标标准

### 必需的图标尺寸

Windows 11 要求 `.ico` 文件包含以下所有尺寸：

| 尺寸 | 用途 | 显示位置 |
|------|------|---------|
| **16x16** | 小图标 | 任务栏、系统托盘、树状列表 |
| **24x24** | 小图标 (150% DPI) | 高DPI显示器 |
| **32x32** | 中图标 | 文件资源管理器列表视图 |
| **48x48** | 大图标 | 文件资源管理器中等图标视图 |
| **64x64** | 大图标 (150% DPI) | 高DPI显示器 |
| **96x96** | 大图标 (200% DPI) | 高DPI显示器 |
| **128x128** | 超大图标 | 文件资源管理器大图标视图 |
| **256x256** | 超大图标 | 文件资源管理器超大图标视图 |
| **512x512** | 可选 | 未来超高DPI显示器 |

### 色深要求

每个尺寸都应该是 **32位色深（RGBA）**：
- ✅ 24位RGB颜色
- ✅ 8位Alpha透明通道
- ✅ 支持平滑抗锯齿
- ✅ 支持阴影效果

### Windows 11 Fluent Design 设计语言

1. **圆角**: 使用柔和的圆角（radius: 8-12px）
2. **扁平化**: 避免过度3D效果
3. **渐变**: 使用微妙的渐变增加深度
4. **阴影**: 柔和的投影（不要过于强烈）
5. **明快色彩**: 使用鲜艳但不刺眼的颜色
6. **高对比度**: 在深色和浅色背景下都清晰可见

---

## 🛠️ 创建多尺寸图标的方法

### 方法1：使用在线工具（最简单）

**推荐工具**: [IcoFX](https://icofx.ro/) 或 [RealWorld Icon Editor](http://www.rw-designer.com/)

**步骤**:
1. 准备一张 **512x512** 或 **1024x1024** 的PNG源图（32位RGBA）
2. 导入到工具中
3. 自动生成所有尺寸（16, 24, 32, 48, 64, 96, 128, 256）
4. 手动微调小尺寸图标（16x16, 24x24）- 简化细节
5. 导出为 `.ico` 文件

### 方法2：使用 ImageMagick（命令行）

**安装 ImageMagick**:
```bash
# Windows (使用 Scoop)
scoop install imagemagick

# 或下载安装包
https://imagemagick.org/script/download.php
```

**生成多尺寸图标**:
```bash
# 从PNG源图生成包含所有尺寸的.ico
magick convert source.png -define icon:auto-resize=256,128,96,64,48,32,24,16 icon.ico
```

### 方法3：使用 PowerShell + .NET（自动化）

创建 `Generate-Icon.ps1`:

```powershell
# Generate-Icon.ps1 - 生成多尺寸Windows 11图标

param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePng,  # 源PNG文件（建议512x512或更大）

    [Parameter(Mandatory=$true)]
    [string]$OutputIco   # 输出.ico文件
)

Add-Type -AssemblyName System.Drawing

# 所需的图标尺寸
$sizes = @(16, 24, 32, 48, 64, 96, 128, 256)

# 加载源图像
$sourceImage = [System.Drawing.Image]::FromFile($SourcePng)

# 创建图标流
$iconStream = New-Object System.IO.MemoryStream
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)

# 写入ICO文件头
$iconWriter.Write([UInt16]0)      # Reserved
$iconWriter.Write([UInt16]1)      # Type: 1 = ICO
$iconWriter.Write([UInt16]$sizes.Length)  # Number of images

$imageDataList = @()
$currentOffset = 6 + ($sizes.Length * 16)  # Header + Directory entries

foreach ($size in $sizes) {
    # 创建缩放后的图像
    $resizedImage = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($resizedImage)

    # 高质量缩放
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
    $graphics.Dispose()

    # 保存为PNG格式（保留透明度）
    $pngStream = New-Object System.IO.MemoryStream
    $resizedImage.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData = $pngStream.ToArray()
    $pngStream.Dispose()
    $resizedImage.Dispose()

    # 写入目录条目
    $iconWriter.Write([Byte]($size % 256))     # Width
    $iconWriter.Write([Byte]($size % 256))     # Height
    $iconWriter.Write([Byte]0)                 # Color palette
    $iconWriter.Write([Byte]0)                 # Reserved
    $iconWriter.Write([UInt16]1)               # Color planes
    $iconWriter.Write([UInt16]32)              # Bits per pixel
    $iconWriter.Write([UInt32]$pngData.Length) # Image data size
    $iconWriter.Write([UInt32]$currentOffset)  # Image data offset

    $imageDataList += $pngData
    $currentOffset += $pngData.Length
}

# 写入所有图像数据
foreach ($imageData in $imageDataList) {
    $iconWriter.Write($imageData)
}

# 保存到文件
$iconWriter.Flush()
$iconBytes = $iconStream.ToArray()
[System.IO.File]::WriteAllBytes($OutputIco, $iconBytes)

$iconWriter.Dispose()
$iconStream.Dispose()
$sourceImage.Dispose()

Write-Host "✅ 图标已生成: $OutputIco"
Write-Host "包含尺寸: $($sizes -join ', ')"
```

**使用方法**:
```powershell
# 从512x512的PNG生成多尺寸图标
.\Generate-Icon.ps1 -SourcePng "source-512.png" -OutputIco "icon-new.ico"
```

### 方法4：使用在线服务

**推荐网站**:
- https://www.icoconverter.com/
- https://convertio.co/png-ico/
- https://favicon.io/favicon-converter/

**步骤**:
1. 上传 512x512 PNG 源图
2. 选择"生成所有尺寸"
3. 下载 `.ico` 文件

---

## 🎨 设计建议

### 为小尺寸优化（16x16, 24x24）

小尺寸图标需要**手动简化**：

```
原始设计（256x256）          简化设计（16x16）
┌─────────────────┐          ┌───────┐
│   ╔═══════╗     │          │ ┌───┐ │
│   ║ 标哥  ║     │    →     │ │ B │ │  (只保留首字母)
│   ║ AutoCAD║    │          │ └───┘ │
│   ╚═══════╝     │          └───────┘
└─────────────────┘
```

**原则**:
- ❌ 不要缩放复杂细节
- ✅ 简化为核心元素
- ✅ 使用粗线条（至少2px）
- ✅ 移除文字（除非非常大）

### 配色方案

**推荐：建筑工程主题**

```css
/* 主色调：建筑蓝 */
--primary: #0078D4;      /* Microsoft Blue */
--secondary: #005A9E;    /* Darker Blue */
--accent: #FFB900;       /* Warning Orange */

/* 渐变 */
background: linear-gradient(135deg, #0078D4 0%, #005A9E 100%);
```

**确保对比度**:
- 浅色背景下清晰可见
- 深色背景下清晰可见
- 使用白色或深色边框（可选）

---

## 📋 检查清单

使用以下PowerShell脚本检查图标：

```powershell
# Check-Icon.ps1 - 检查图标尺寸和色深

param([string]$IconPath)

Add-Type -AssemblyName System.Drawing

$icon = New-Object System.Drawing.Icon($IconPath)

Write-Host "`n=== 图标信息 ===" -ForegroundColor Cyan
Write-Host "文件: $IconPath"
Write-Host "大小: $([math]::Round((Get-Item $IconPath).Length / 1KB, 2)) KB"

Write-Host "`n包含的尺寸:" -ForegroundColor Yellow

# 读取ICO文件
$bytes = [System.IO.File]::ReadAllBytes($IconPath)
$stream = New-Object System.IO.MemoryStream(,$bytes)
$reader = New-Object System.IO.BinaryReader($stream)

# 跳过文件头
$reader.ReadUInt16() | Out-Null  # Reserved
$reader.ReadUInt16() | Out-Null  # Type
$count = $reader.ReadUInt16()    # Count

Write-Host "总数: $count 个图像`n" -ForegroundColor Green

$requiredSizes = @(16, 24, 32, 48, 64, 96, 128, 256)
$foundSizes = @()

for ($i = 0; $i -lt $count; $i++) {
    $width = $reader.ReadByte()
    $height = $reader.ReadByte()
    $colorCount = $reader.ReadByte()
    $reserved = $reader.ReadByte()
    $planes = $reader.ReadUInt16()
    $bitCount = $reader.ReadUInt16()
    $imageSize = $reader.ReadUInt32()
    $imageOffset = $reader.ReadUInt32()

    $actualWidth = if ($width -eq 0) { 256 } else { $width }
    $actualHeight = if ($height -eq 0) { 256 } else { $height }

    $foundSizes += $actualWidth

    $status = if ($bitCount -eq 32) { "✅" } else { "⚠️" }
    Write-Host "$status ${actualWidth}x${actualHeight} - ${bitCount}位色深"
}

Write-Host "`n=== 合规性检查 ===" -ForegroundColor Cyan

$missing = $requiredSizes | Where-Object { $_ -notin $foundSizes }

if ($missing.Count -eq 0) {
    Write-Host "✅ 所有必需尺寸都存在" -ForegroundColor Green
} else {
    Write-Host "❌ 缺少以下尺寸: $($missing -join ', ')" -ForegroundColor Red
}

# 检查32位色深
$non32bit = $count - ($foundSizes | Where-Object { $_ -in $requiredSizes }).Count
if ($non32bit -eq 0) {
    Write-Host "✅ 所有尺寸都是32位色深" -ForegroundColor Green
} else {
    Write-Host "⚠️  部分尺寸不是32位色深" -ForegroundColor Yellow
}

$reader.Dispose()
$stream.Dispose()
$icon.Dispose()

Write-Host ""
```

**运行检查**:
```powershell
.\Check-Icon.ps1 -IconPath "icon.ico"
```

**期望输出**:
```
=== 图标信息 ===
文件: icon.ico
大小: 156.8 KB

包含的尺寸:
总数: 8 个图像

✅ 16x16 - 32位色深
✅ 24x24 - 32位色深
✅ 32x32 - 32位色深
✅ 48x48 - 32位色深
✅ 64x64 - 32位色深
✅ 96x96 - 32位色深
✅ 128x128 - 32位色深
✅ 256x256 - 32位色深

=== 合规性检查 ===
✅ 所有必需尺寸都存在
✅ 所有尺寸都是32位色深
```

---

## 🚀 快速行动计划

### 步骤1：准备源图

创建或获取一个 **512x512 像素，32位RGBA** 的PNG图像：

**选项A：使用Figma/Canva设计**
1. 创建512x512画布
2. 设计图标（圆角正方形，蓝色渐变）
3. 添加 "B" 字母或建筑符号
4. 导出为PNG（32位，透明背景）

**选项B：使用AI生成**
```
提示词：
"A modern Windows 11 style app icon for an AutoCAD plugin,
blue gradient background with rounded corners,
featuring a stylized letter 'B' or building blueprint symbol,
clean and minimal design, 512x512, transparent background"
```

**选项C：修改现有图标**
- 如果已有设计，使用Photoshop/GIMP调整到512x512

### 步骤2：生成多尺寸.ico

**最简单方法**：
```bash
# 使用在线工具
1. 访问 https://www.icoconverter.com/
2. 上传 source-512.png
3. 选择所有尺寸 (16, 24, 32, 48, 64, 96, 128, 256)
4. 下载 icon-new.ico
```

**本地方法**（如果安装了ImageMagick）：
```bash
magick convert source-512.png -define icon:auto-resize=256,128,96,64,48,32,24,16 icon-new.ico
```

### 步骤3：替换图标

```bash
# 备份旧图标
cd BiaogAutoCADPlugin/Installer-GUI
mv icon.ico icon-old.ico

# 复制新图标
cp icon-new.ico icon.ico
```

### 步骤4：重新编译

```bash
cd BiaogAutoCADPlugin/Installer-GUI
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true
```

### 步骤5：验证

```powershell
# 检查新图标
.\Check-Icon.ps1 -IconPath "icon.ico"

# 在文件资源管理器中查看
# - 切换到不同视图（列表、大图标、超大图标）
# - 确认在所有尺寸下都清晰
```

---

## 📚 参考资源

### 官方文档
- [Windows App Icon Guidelines](https://learn.microsoft.com/en-us/windows/apps/design/style/iconography/app-icon-design)
- [Fluent 2 Design System](https://fluent2.microsoft.design/)

### 设计工具
- **IcoFX**: https://icofx.ro/ (付费，功能强大)
- **RealWorld Icon Editor**: http://www.rw-designer.com/ (免费)
- **GIMP**: https://www.gimp.org/ (免费，需插件)

### 在线转换器
- https://www.icoconverter.com/
- https://convertio.co/png-ico/
- https://favicon.io/favicon-converter/

### 命令行工具
- **ImageMagick**: https://imagemagick.org/
- **icotool** (Linux): `apt install icoutils`

---

## ✅ 最终检查清单

制作完成后，确认：

- [ ] ✅ 包含所有8个必需尺寸（16, 24, 32, 48, 64, 96, 128, 256）
- [ ] ✅ 所有尺寸都是32位色深（RGBA）
- [ ] ✅ 16x16和24x24手动简化过（不是直接缩放）
- [ ] ✅ 使用Windows 11设计语言（圆角、扁平、渐变）
- [ ] ✅ 在浅色背景下清晰可见
- [ ] ✅ 在深色背景下清晰可见
- [ ] ✅ 文件大小合理（100-300 KB）
- [ ] ✅ 在文件资源管理器中所有视图模式下都清晰

---

**当前状态**: ❌ 不合规（只有1个尺寸，16色）
**目标状态**: ✅ 完全符合Windows 11标准（8个尺寸，32位色深）
