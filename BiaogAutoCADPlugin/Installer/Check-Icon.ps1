# Check-Icon.ps1 - 检查图标尺寸和色深
# 用法: .\Check-Icon.ps1 -IconPath "icon.ico"

param(
    [Parameter(Mandatory=$false)]
    [string]$IconPath = "icon.ico"
)

if (-not (Test-Path $IconPath)) {
    Write-Host "❌ 错误: 找不到文件 $IconPath" -ForegroundColor Red
    exit 1
}

Add-Type -AssemblyName System.Drawing

Write-Host "`n╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        Windows 11 图标合规性检查工具         ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host "`n📁 文件信息" -ForegroundColor Yellow
Write-Host "   路径: $IconPath"
Write-Host "   大小: $([math]::Round((Get-Item $IconPath).Length / 1KB, 2)) KB"

# 读取ICO文件
$bytes = [System.IO.File]::ReadAllBytes($IconPath)
$stream = New-Object System.IO.MemoryStream(,$bytes)
$reader = New-Object System.IO.BinaryReader($stream)

# 读取文件头
$reserved = $reader.ReadUInt16()
$type = $reader.ReadUInt16()
$count = $reader.ReadUInt16()

Write-Host "`n📊 包含的图像" -ForegroundColor Yellow
Write-Host "   总数: $count 个`n"

$requiredSizes = @(16, 24, 32, 48, 64, 96, 128, 256)
$foundSizes = @()
$allAre32Bit = $true

Write-Host "   尺寸      色深     状态" -ForegroundColor Gray
Write-Host "   ────────────────────────────" -ForegroundColor Gray

for ($i = 0; $i -lt $count; $i++) {
    $width = $reader.ReadByte()
    $height = $reader.ReadByte()
    $colorCount = $reader.ReadByte()
    $reserved = $reader.ReadByte()
    $planes = $reader.ReadUInt16()
    $bitCount = $reader.ReadUInt16()
    $imageSize = $reader.ReadUInt32()
    $imageOffset = $reader.ReadUInt32()

    # 处理256尺寸（在ICO中用0表示）
    $actualWidth = if ($width -eq 0) { 256 } else { $width }
    $actualHeight = if ($height -eq 0) { 256 } else { $height }

    $foundSizes += $actualWidth

    # 检查是否为32位
    $is32Bit = $bitCount -eq 32
    if (-not $is32Bit) { $allAre32Bit = $false }

    # 格式化输出
    $sizeStr = "${actualWidth}x${actualHeight}".PadRight(8)
    $depthStr = "${bitCount}位".PadRight(8)

    if ($is32Bit -and ($actualWidth -in $requiredSizes)) {
        Write-Host "   $sizeStr $depthStr ✅" -ForegroundColor Green
    } elseif ($is32Bit) {
        Write-Host "   $sizeStr $depthStr ⚠️  (额外尺寸)" -ForegroundColor Yellow
    } else {
        Write-Host "   $sizeStr $depthStr ❌ (色深不足)" -ForegroundColor Red
    }
}

$reader.Dispose()
$stream.Dispose()

# 合规性检查
Write-Host "`n🔍 合规性检查" -ForegroundColor Yellow
Write-Host ""

$missingRequired = $requiredSizes | Where-Object { $_ -notin $foundSizes }
$passedAll = $true

# 检查1: 必需尺寸
Write-Host "   [1] 必需尺寸检查" -ForegroundColor Cyan
if ($missingRequired.Count -eq 0) {
    Write-Host "       ✅ 所有必需尺寸都存在" -ForegroundColor Green
    Write-Host "       包含: 16, 24, 32, 48, 64, 96, 128, 256" -ForegroundColor Gray
} else {
    Write-Host "       ❌ 缺少以下尺寸: $($missingRequired -join ', ')" -ForegroundColor Red
    Write-Host "       必需: 16, 24, 32, 48, 64, 96, 128, 256" -ForegroundColor Gray
    $passedAll = $false
}

Write-Host ""

# 检查2: 色深
Write-Host "   [2] 色深检查" -ForegroundColor Cyan
if ($allAre32Bit) {
    Write-Host "       ✅ 所有尺寸都是32位色深（RGBA）" -ForegroundColor Green
} else {
    Write-Host "       ❌ 部分尺寸不是32位色深" -ForegroundColor Red
    Write-Host "       要求: 32位（24位RGB + 8位Alpha）" -ForegroundColor Gray
    $passedAll = $false
}

Write-Host ""

# 检查3: 文件大小
Write-Host "   [3] 文件大小检查" -ForegroundColor Cyan
$fileSize = (Get-Item $IconPath).Length / 1KB
if ($fileSize -gt 50 -and $fileSize -lt 500) {
    Write-Host "       ✅ 文件大小合理 ($([math]::Round($fileSize, 2)) KB)" -ForegroundColor Green
} elseif ($fileSize -le 50) {
    Write-Host "       ⚠️  文件较小 ($([math]::Round($fileSize, 2)) KB)，可能质量不够" -ForegroundColor Yellow
} else {
    Write-Host "       ⚠️  文件较大 ($([math]::Round($fileSize, 2)) KB)，建议优化" -ForegroundColor Yellow
}

# 最终结果
Write-Host "`n╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
if ($passedAll) {
    Write-Host "║          ✅ 完全符合 Windows 11 标准          ║" -ForegroundColor Green
} else {
    Write-Host "║          ❌ 不符合 Windows 11 标准            ║" -ForegroundColor Red
}
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan

if (-not $passedAll) {
    Write-Host "`n📖 建议:" -ForegroundColor Yellow
    Write-Host "   1. 阅读 Windows11图标规范.md 了解详细要求" -ForegroundColor Gray
    Write-Host "   2. 使用 Generate-Icon.ps1 从PNG源图生成合规图标" -ForegroundColor Gray
    Write-Host "   3. 或使用在线工具: https://www.icoconverter.com/" -ForegroundColor Gray
}

Write-Host ""

# 返回状态码
if ($passedAll) { exit 0 } else { exit 1 }
