# 表哥 C# 版本构建指南

## 系统要求

- .NET SDK 8.0 或更高版本
- Windows 10/11, macOS 10.15+, 或 Linux (Ubuntu 20.04+)
- 4GB RAM 最小（推荐 8GB）
- 500MB 磁盘空间

## 安装 .NET SDK

### Windows

```powershell
# 使用 Winget
winget install Microsoft.DotNet.SDK.8

# 或从官网下载
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### macOS

```bash
# 使用 Homebrew
brew install --cask dotnet-sdk

# 或从官网下载
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### Linux (Ubuntu/Debian)

```bash
# 添加 Microsoft 包仓库
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 .NET SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

## 验证安装

```bash
dotnet --version
# 应该输出: 8.0.x
```

## 构建项目

### 1. 克隆仓库

```bash
git clone <repository-url>
cd biaoge/BiaogeCSharp
```

### 2. 恢复依赖

```bash
dotnet restore
```

### 3. 构建项目

```bash
# 调试版本
dotnet build

# 发布版本
dotnet build --configuration Release
```

## 运行应用

### 开发模式

```bash
cd src/BiaogeCSharp
dotnet run
```

### 发布和打包

```bash
# 发布单文件可执行程序 (Windows)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 发布单文件可执行程序 (macOS)
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true

# 发布单文件可执行程序 (Linux)
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

输出位置：`src/BiaogeCSharp/bin/Release/net8.0/<runtime>/publish/`

## 项目结构

```
BiaogeCSharp/
├── src/
│   └── BiaogeCSharp/
│       ├── Controls/          # 自定义UI控件
│       │   ├── NavigationView # 导航视图
│       │   ├── CardWidget     # 卡片控件
│       │   ├── InfoBar        # 通知栏
│       │   └── DwgCanvas      # DWG渲染画布
│       ├── Views/             # XAML视图
│       │   ├── MainWindow     # 主窗口
│       │   ├── HomePage       # 主页
│       │   ├── TranslationPage # 翻译页
│       │   ├── CalculationPage # 算量页
│       │   ├── ExportPage     # 导出页
│       │   └── SettingsDialog # 设置对话框
│       ├── ViewModels/        # MVVM视图模型
│       │   ├── MainWindowViewModel
│       │   ├── TranslationViewModel
│       │   ├── CalculationViewModel
│       │   └── ExportViewModel
│       ├── Models/            # 数据模型
│       │   ├── DwgDocument
│       │   └── ComponentRecognitionResult
│       ├── Services/          # 业务服务
│       │   ├── AsposeDwgParser      # DWG解析
│       │   ├── BailianApiClient     # 百炼API
│       │   ├── TranslationEngine    # 翻译引擎
│       │   ├── CacheService         # 缓存服务
│       │   └── ConfigManager        # 配置管理
│       ├── App.axaml          # 应用程序配置
│       ├── Program.cs         # 程序入口
│       └── BiaogeCSharp.csproj # 项目文件
├── docs/
│   └── UI_MIGRATION_GUIDE.md  # UI迁移指南
└── BUILD_INSTRUCTIONS.md      # 本文件
```

## 开发工具推荐

### IDE

- **Visual Studio 2022** (Windows) - 推荐，完整Avalonia支持
- **Visual Studio Code** - 跨平台，需要安装扩展
- **JetBrains Rider** - 跨平台，商业IDE

### Visual Studio Code 扩展

```bash
# Avalonia支持
code --install-extension AvaloniaTeam.vscode-avalonia

# C#支持
code --install-extension ms-dotnettools.csharp

# XAML支持
code --install-extension ms-dotnettools.csdevkit
```

## 常见问题

### 1. "找不到 dotnet 命令"

确保 .NET SDK 已正确安装并添加到 PATH 环境变量。

### 2. "无法还原 NuGet 包"

```bash
# 清理并重新还原
dotnet clean
dotnet restore --force
```

### 3. "Aspose.CAD 许可证错误"

Aspose.CAD 是商业库。评估模式会有水印限制。购买许可证后：

```csharp
// 在 AsposeDwgParser.cs 中添加
Aspose.CAD.License license = new Aspose.CAD.License();
license.SetLicense("path/to/Aspose.CAD.lic");
```

### 4. "运行时缺少 SkiaSharp 本机库"

```bash
# 重新安装 Avalonia.Skia
dotnet remove package Avalonia.Skia
dotnet add package Avalonia.Skia --version 11.0.10
```

## 性能优化构建

```bash
# 启用 ReadyToRun 编译
dotnet publish -c Release -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true \
  -p:PublishTrimmed=false

# 启用裁剪（减小文件大小，但可能影响反射）
dotnet publish -c Release -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=link
```

## 调试技巧

### 启用详细日志

修改 `Program.cs`:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()  // 改为 Debug
    .WriteTo.Console()
    .WriteTo.File("logs/biaoge-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
    .CreateLogger();
```

### 启用 Avalonia DevTools

在 `Program.cs` 的 `BuildAvaloniaApp()` 中添加:

```csharp
#if DEBUG
    .UseDevTools()
#endif
```

然后在运行时按 F12 打开 DevTools。

## 与 Python 版本的性能对比

根据 `MIGRATION_ANALYSIS.md` 的预测：

| 指标 | Python版本 | C#版本 | 提升 |
|------|----------|--------|------|
| DWG加载 | 2.5s | 0.6s | 4.2x |
| 渲染(50K实体) | 45ms | 6ms | 7.5x |
| 内存占用 | 600MB | 150MB | 4x |
| 翻译API调用 | 120ms | 35ms | 3.4x |

## 下一步

1. 配置阿里云百炼 API 密钥
2. 测试 DWG 文件打开功能
3. 验证翻译功能
4. 性能基准测试

## 支持

如有问题，请参考：
- `docs/UI_MIGRATION_GUIDE.md` - UI组件详细说明
- `MIGRATION_ANALYSIS.md` - 迁移技术分析
- `CSHARP_MIGRATION_PLAN.md` - 完整迁移计划

## 许可证

商业软件 - 版权所有 © 2025
