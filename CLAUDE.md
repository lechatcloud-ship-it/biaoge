# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目简介

**标哥AutoCAD插件** - 专业的建筑工程CAD图纸AI智能翻译和工程量计算工具

这是一个基于 **AutoCAD .NET API** 的AutoCAD插件（非独立软件），集成了AI智能翻译、构件识别算量和Excel导出功能。采用阿里云百炼大模型提供AI能力。

**重要说明：**
- 这是一个 **AutoCAD插件**，不是独立的桌面应用程序
- 需要在AutoCAD环境中运行（通过NETLOAD或自动加载）
- 使用AutoCAD官方引擎，实现100%准确的DWG文件读取
- 仅支持 **Windows平台**（AutoCAD for Mac不支持.NET API）

## 核心技术栈

### 开发语言和框架
- **开发语言**: C# 11 (.NET 8.0)
- **AutoCAD API**: AutoCAD .NET API 2024/2025
  - `acdbmgd.dll` - Database API（数据库对象）
  - `acmgd.dll` - Application Services API（应用服务）
  - `AcCoreMgd.dll` - Core API（核心功能）
- **UI框架**: WPF (Windows Presentation Foundation)
  - PaletteSet（AutoCAD可停靠面板）
  - XAML布局 + Dark主题

### 依赖库
- **阿里云百炼**: DashScope API（AI翻译）
- **日志**: Serilog 3.1+（结构化日志）
- **数据库**: Microsoft.Data.Sqlite 8.0+（翻译缓存）
- **Excel导出**: EPPlus 7.0.10（工程量清单）
- **HTTP客户端**: System.Net.Http

### 平台要求
- **操作系统**: Windows 10/11（64-bit）
- **AutoCAD版本**: AutoCAD 2024/2025
- **.NET版本**: .NET Framework 4.8 或 .NET 8.0
- **Visual Studio**: 2022+（推荐）

**为什么只支持Windows？**
- AutoCAD for Mac **不支持 .NET API**
- Mac版只支持AutoLISP，不支持C#/.NET开发
- 如果需要跨平台，必须使用C++ ObjectARX + Qt/wxWidgets

## 开发环境设置

### 前置要求

1. **安装Visual Studio 2022**
   - 工作负载：".NET桌面开发"
   - 工作负载：".NET跨平台开发"

2. **安装AutoCAD 2024或2025**
   - 确保安装了桌面版（非Web版）
   - 注意：Express版本不支持插件开发

3. **配置AutoCAD .NET API引用**
   - AutoCAD DLL位置：`C:\Program Files\Autodesk\AutoCAD 2024\`
   - 在项目中引用（已在.csproj中配置）：
     ```xml
     <Reference Include="acdbmgd">
       <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll</HintPath>
       <Private>False</Private>
     </Reference>
     ```

### 获取代码

```bash
git clone https://github.com/lechatcloud-ship-it/biaoge.git
cd biaoge/BiaogAutoCADPlugin
```

### 配置API密钥

首次运行需要配置阿里云百炼API密钥：

1. **获取API密钥**：访问 https://dashscope.aliyuncs.com/
2. **在AutoCAD中配置**：
   - 加载插件后运行 `BIAOGE_SETTINGS` 命令
   - 在"百炼API配置"选项卡中输入API密钥
   - 配置保存在：`%USERPROFILE%\.biaoge\config.json`

### 构建项目

```bash
# 使用自动化脚本（推荐）
cd BiaogAutoCADPlugin
.\build.bat

# 或使用dotnet CLI
dotnet restore
dotnet build --configuration Release
```

### 调试插件

1. **配置调试启动项**（已在.csproj中配置）：
   ```xml
   <PropertyGroup Condition="'$(Configuration)'=='Debug'">
     <StartAction>Program</StartAction>
     <StartProgram>C:\Program Files\Autodesk\AutoCAD 2024\acad.exe</StartProgram>
   </PropertyGroup>
   ```

2. **按F5启动调试**：
   - Visual Studio会自动启动AutoCAD
   - 在AutoCAD命令行输入 `NETLOAD`
   - 选择 `bin\Debug\net8.0\BiaogPlugin.dll`
   - 输入 `BIAOGE_HELP` 查看所有命令

3. **设置断点**：
   - 在C#代码中设置断点
   - 在AutoCAD中执行命令，触发断点

## 项目架构

### 架构分层

```
┌─────────────────────────────────────────────────────────┐
│          AutoCAD命令层 (Commands.cs)                    │
│  BIAOGE_TRANSLATE | BIAOGE_CALCULATE | BIAOGE_SETTINGS  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│          UI层 (UI/)                                      │
│  PaletteSet | TranslationPalette | CalculationPalette   │
│  SettingsDialog | WPF Controls                          │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│          业务逻辑层 (Services/)                          │
│  TranslationController | TranslationEngine               │
│  ComponentRecognizer | QuantityCalculator                │
│  ExcelExporter | PerformanceMonitor                     │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│          服务层 (Services/)                              │
│  BailianApiClient | CacheService | ConfigManager        │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│          数据访问层 (Services/)                          │
│  DwgTextExtractor | DwgTextUpdater                      │
│  AutoCAD Database API (acdbmgd.dll)                     │
└─────────────────────────────────────────────────────────┘
```

### 关键模块说明

#### 插件生命周期 (`PluginApplication.cs`)
- 实现 `IExtensionApplication` 接口
- `Initialize()`: 插件加载时初始化服务
- `Terminate()`: 插件卸载时清理资源
- 使用 `ServiceLocator` 管理服务依赖

#### 命令系统 (`Commands.cs`)
- 使用 `[CommandMethod]` 特性注册AutoCAD命令
- 命令前缀：`BIAOGE_`
- 17个命令（翻译、算量、设置、诊断、工具）

#### UI面板 (`UI/`)
- **TranslationPalette**: 翻译界面（语言选择、进度显示）
- **CalculationPalette**: 算量界面（构件识别、统计、导出）
- **SettingsDialog**: 设置对话框（API配置、缓存管理）
- 使用 `PaletteSet` 创建可停靠面板

#### 翻译引擎 (`Services/TranslationEngine.cs`)
- 批量翻译（50条/批，减少API调用）
- 智能缓存（90%+命中率）
- 进度回调（实时更新UI）

#### DWG处理 (`Services/Dwg*.cs`)
- **DwgTextExtractor**: 提取DWG文本实体（DBText, MText, Attribute）
- **DwgTextUpdater**: 更新DWG文本内容
- **TextFilter**: 过滤可翻译文本（排除纯数字、符号）

#### 构件识别 (`Services/ComponentRecognizer.cs`)
- 多策略识别：正则表达式 + 数量提取 + 规范验证 + AI验证
- 建筑规范约束：GB 50854-2013等
- 置信度评分：0.5-1.0

#### 工程量计算 (`Services/QuantityCalculator.cs`)
- 按类型分组统计
- 材料汇总（混凝土、钢筋、砌体、门窗）
- 成本估算

#### Excel导出 (`Services/ExcelExporter.cs`)
- 使用EPPlus 7.0.10（NonCommercial许可）
- 三个工作表：汇总表、明细表、材料表
- 专业格式和样式

## 核心数据流

### 翻译流程
```
用户点击"翻译" → TranslationPalette
         ↓
    TranslationController
         ↓
1. DwgTextExtractor.ExtractAllText()
   → 遍历BlockTable → 提取DBText/MText/Attribute
         ↓
2. TextFilter.FilterTranslatableText()
   → 过滤纯数字、空文本、符号
         ↓
3. CacheService.GetTranslationAsync()
   → SQLite查询（LRU缓存）
         ↓
4. TranslationEngine.TranslateBatchWithCacheAsync()
   → BailianApiClient.TranslateTextAsync()
   → 批量50条/次，带重试逻辑
         ↓
5. DwgTextUpdater.UpdateTexts()
   → 使用Transaction更新DBText.TextString
   → LockDocument确保线程安全
         ↓
    更新UI统计 → 完成
```

### 算量流程
```
用户点击"识别" → CalculationPalette
         ↓
1. DwgTextExtractor.ExtractAllText()
         ↓
2. ComponentRecognizer.RecognizeFromTextEntitiesAsync()
   → 策略1: 正则匹配（Compiled Regex）
   → 策略2: 数量提取（QuantityRegex）
   → 策略3: 规范验证（GB标准）
   → 策略4: AI验证（可选）
         ↓
3. 过滤置信度 >= 阈值
         ↓
4. QuantityCalculator.CalculateSummary()
   → GroupByType（按类型分组）
   → CalculateMaterialSummary（材料汇总）
         ↓
5. ExcelExporter.ExportSummary()
   → EPPlus生成三个工作表
   → 保存到桌面
         ↓
    打开文件夹 → 完成
```

## AutoCAD .NET API最佳实践

基于Autodesk官方文档（help.autodesk.com/view/OARX/2025/ENU/）：

### 1. Transaction模式（必须）

```csharp
// ✅ 正确：始终使用Transaction
using (var tr = db.TransactionManager.StartTransaction())
{
    var obj = tr.GetObject(objectId, OpenMode.ForWrite);
    // 修改对象...
    tr.Commit(); // 提交更改
}

// ❌ 错误：不使用Transaction
var obj = objectId.GetObject(OpenMode.ForWrite); // 不安全！
```

### 2. Document Locking（必须）

```csharp
// ✅ 正确：锁定文档
var doc = Application.DocumentManager.MdiActiveDocument;
using (var docLock = doc.LockDocument())
{
    // 修改DWG数据...
}

// ❌ 错误：不锁定文档（多线程不安全）
```

### 3. 命令标志

```csharp
// Modal命令（阻塞UI）
[CommandMethod("BIAOGE_TRANSLATE", CommandFlags.Modal)]

// Session命令（非阻塞，适合长时间操作）
[CommandMethod("BIAOGE_BACKGROUND", CommandFlags.Session)]
```

### 4. 异步操作

```csharp
// ✅ 正确：async/await模式
[CommandMethod("BIAOGE_TRANSLATE", CommandFlags.Modal)]
public async void TranslateDrawing()
{
    await Task.Run(() => {
        // 长时间操作...
    });
}
```

### 5. 资源释放

```csharp
// ✅ 正确：使用using确保释放
using (var reader = blockRecord.GetObjects())
{
    foreach (ObjectId objId in reader)
    {
        // 处理对象...
    }
} // 自动释放
```

### 6. 错误处理

```csharp
try
{
    using (var tr = db.TransactionManager.StartTransaction())
    {
        // 操作...
        tr.Commit();
    }
}
catch (Autodesk.AutoCAD.Runtime.Exception ex)
{
    // 处理AutoCAD特定异常
    Log.Error(ex, "AutoCAD操作失败");
    ed.WriteMessage($"\n[错误] {ex.Message}");
}
```

## 代码规范

### 注释语言
- 代码注释统一使用**中文**
- XML文档注释使用中文
- 变量命名使用英文

### 命名约定
```csharp
// 类名：PascalCase
public class TranslationEngine { }

// 方法名：PascalCase
public async Task TranslateAsync() { }

// 私有字段：_camelCase
private readonly BailianApiClient _bailianClient;

// 局部变量：camelCase
var textEntities = extractor.ExtractAllText();

// 常量：PascalCase
private const int BatchSize = 50;
```

### 日志规范
```csharp
// 使用结构化日志
Log.Information("开始翻译: {Count} 条文本", texts.Count);
Log.Error(ex, "API调用失败: {ErrorCode}", errorCode);
Log.Debug("缓存命中: {CacheKey}", cacheKey);
```

### 异步编程
```csharp
// ✅ 使用async/await
public async Task<List<string>> TranslateAsync(List<string> texts)
{
    return await Task.Run(() => {
        // 同步操作...
    });
}

// ✅ 异步方法以Async结尾
public async Task<string> GetTranslationAsync(string text)
```

## 配置管理

### 配置文件位置
```
%USERPROFILE%\.biaoge\
    ├── config.json           # 用户配置
    ├── cache.db              # 翻译缓存（SQLite）
    └── logs\                 # 日志文件
```

### 配置结构
```json
{
  "Bailian": {
    "ApiKey": "sk-...",
    "BaseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
    "TextTranslationModel": "qwen-mt-plus",
    "ImageTranslationModel": "qwen-vl-max",
    "MultimodalDialogModel": "qwen-vl-max"
  },
  "Translation": {
    "BatchSize": 50,
    "EnableCache": true,
    "CacheExpirationDays": 30
  }
}
```

## 常见开发任务

### 添加新命令

1. 在 `Commands.cs` 中添加：
```csharp
[CommandMethod("BIAOGE_NEWFEATURE", CommandFlags.Modal)]
public void NewFeature()
{
    var doc = Application.DocumentManager.MdiActiveDocument;
    var ed = doc.Editor;

    ed.WriteMessage("\n执行新功能...");
    // 实现逻辑...
}
```

2. 更新 `BIAOGE_HELP` 命令的帮助信息

### 添加新的识别规则

修改 `Services/ComponentRecognizer.cs`:
```csharp
private static readonly Dictionary<string, List<Regex>> ComponentPatterns = new()
{
    ["新构件类型"] = new List<Regex>
    {
        new Regex(@"匹配模式1", RegexOptions.Compiled),
        new Regex(@"匹配模式2", RegexOptions.Compiled)
    }
};
```

### 添加新的UI面板

1. 创建 `UI/NewPalette.xaml` 和 `NewPalette.xaml.cs`
2. 在 `PaletteManager.cs` 中注册
3. 添加命令打开面板

### 修改缓存策略

修改 `Services/CacheService.cs`:
```csharp
public async Task<string?> GetTranslationAsync(string originalText, string targetLanguage)
{
    // 自定义缓存逻辑...
}
```

## 部署和分发

### 构建发布版本

```bash
# Windows批处理脚本
.\build.bat

# 输出位置
dist\BiaogPlugin\
    ├── BiaogPlugin.dll           # 主插件DLL
    ├── Serilog.dll               # 依赖库
    ├── Microsoft.Data.Sqlite.dll
    └── EPPlus.dll
```

### 安装方式

#### 方式1：NETLOAD手动加载
```
1. 复制dist\BiaogPlugin\到用户电脑
2. 在AutoCAD中输入NETLOAD
3. 选择BiaogPlugin.dll
4. 输入BIAOGE_HELP查看命令
```

#### 方式2：自动加载（ApplicationPlugins）
```
1. 复制整个插件包到：
   C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\

2. 目录结构：
   BiaogPlugin.bundle\
       ├── PackageContents.xml  # 插件清单
       └── Contents\
           └── Windows\
               └── 2024\
                   ├── BiaogPlugin.dll
                   └── 依赖DLL...

3. 重启AutoCAD，插件自动加载
```

#### 方式3：acad.lsp启动脚本
```lisp
; 在Support路径下创建acad.lsp
(command "._NETLOAD" "C:\\Path\\To\\BiaogPlugin.dll")
```

### 企业批量部署

使用Group Policy或脚本：
```powershell
# deploy.ps1
$pluginPath = "\\server\share\BiaogPlugin"
$targetPath = "$env:ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle"

Copy-Item -Path $pluginPath -Destination $targetPath -Recurse -Force
```

## 功能清单

| 功能类别 | 命令 | 说明 | 状态 |
|---------|------|------|------|
| **翻译** | `BIAOGE_TRANSLATE` | 打开翻译面板 | ✅ |
|  | `BIAOGE_TRANSLATE_EN` | 快速翻译为英语 | ✅ |
| **算量** | `BIAOGE_CALCULATE` | 打开算量面板 | ✅ |
|  | `BIAOGE_EXPORTEXCEL` | 快速导出Excel清单 | ✅ |
|  | `BIAOGE_QUICKCOUNT` | 快速统计构件数量 | ✅ |
| **设置** | `BIAOGE_SETTINGS` | 打开设置对话框 | ✅ |
| **工具** | `BIAOGE_HELP` | 显示帮助信息 | ✅ |
|  | `BIAOGE_VERSION` | 显示版本信息 | ✅ |
|  | `BIAOGE_ABOUT` | 关于插件 | ✅ |
|  | `BIAOGE_CLEARCACHE` | 清除翻译缓存 | ✅ |
|  | `BIAOGE_TEXTCOUNT` | 统计文本实体 | ✅ |
|  | `BIAOGE_LAYERINFO` | 显示图层信息 | ✅ |
|  | `BIAOGE_BACKUP` | 备份当前图纸 | ✅ |
| **诊断** | `BIAOGE_DIAGNOSTIC` | 运行系统诊断 | ✅ |
|  | `BIAOGE_PERFORMANCE` | 性能监控报告 | ✅ |
|  | `BIAOGE_RESETPERF` | 重置性能统计 | ✅ |

## 插件vs桌面应用功能对比

| 功能 | AutoCAD插件 | 桌面应用（PyQt6/Avalonia） |
|-----|-------------|---------------------------|
| DWG读取准确性 | ✅ 100%（官方引擎） | ❌ 70-80%（ezdxf/Aspose.CAD） |
| AI智能翻译 | ✅ 8种语言 | ✅ 8种语言 |
| 构件识别算量 | ✅ 多策略识别 | ✅ 多策略识别 |
| Excel导出 | ✅ 三表格式 | ✅ 三表格式 |
| PDF导出 | ⏳ 计划中 | ✅ 已实现 |
| 工作流集成 | ✅ 无缝集成 | ❌ 需切换软件 |
| 平台支持 | ❌ 仅Windows | ✅ Windows/Mac/Linux |
| 独立分发 | ❌ 依赖AutoCAD | ✅ 独立运行 |
| 成本 | ✅ $0（已有AutoCAD） | ❌ Aspose.CAD $999/年 |

**结论：**
- 对于已有AutoCAD的建筑设计公司，**插件方案是最佳选择**
- 如果需要给没有AutoCAD的客户使用，才需要桌面应用

## 性能优化技巧

### 1. 批量处理
```csharp
// ✅ 批量处理（50条/次）
var batches = texts.Chunk(50);
foreach (var batch in batches)
{
    await TranslateBatchAsync(batch);
}

// ❌ 逐条处理（慢）
foreach (var text in texts)
{
    await TranslateAsync(text); // 每次API调用
}
```

### 2. 缓存优化
```csharp
// ✅ 使用内存缓存 + SQLite持久化
var cached = _memoryCache.Get(cacheKey);
if (cached == null)
{
    cached = await _sqliteCache.GetAsync(cacheKey);
    _memoryCache.Set(cacheKey, cached);
}
```

### 3. 并发控制
```csharp
// ✅ 使用SemaphoreSlim限制并发
private static readonly SemaphoreSlim _semaphore = new(5); // 最多5个并发

await _semaphore.WaitAsync();
try
{
    await ProcessAsync();
}
finally
{
    _semaphore.Release();
}
```

### 4. 性能监控
```csharp
// ✅ 使用PerformanceMonitor跟踪
var monitor = ServiceLocator.GetService<PerformanceMonitor>();
using (monitor.Measure("TranslateOperation"))
{
    await TranslateAsync();
}
```

## 故障排除

### 插件加载失败

1. **检查.NET版本**：
   ```
   dotnet --version
   # 应显示 8.0.x
   ```

2. **检查AutoCAD版本**：
   - 插件支持AutoCAD 2024/2025
   - 检查PackageContents.xml中的版本号

3. **查看日志**：
   ```
   %USERPROFILE%\.biaoge\logs\
   ```

### API调用失败

1. **检查API密钥**：
   - 运行 `BIAOGE_DIAGNOSTIC`
   - 查看"API连接检查"结果

2. **检查网络连接**：
   - 确保能访问 `dashscope.aliyuncs.com`
   - 检查防火墙设置

3. **查看详细错误**：
   ```
   %USERPROFILE%\.biaoge\logs\biaoge-{date}.log
   ```

### 缓存问题

1. **清除缓存**：
   ```
   BIAOGE_CLEARCACHE
   ```

2. **手动删除**：
   ```
   del %USERPROFILE%\.biaoge\cache.db
   ```

## 相关资源

- **Autodesk官方文档**: https://help.autodesk.com/view/OARX/2025/ENU/
- **AutoCAD DevBlog**: https://adndevblog.typepad.com/autocad/
- **Autodesk Platform Services**: https://aps.autodesk.com/
- **项目GitHub**: https://github.com/lechatcloud-ship-it/biaoge

## 许可证

商业软件 - 版权所有 © 2025

本软件为商业软件，未经授权不得用于商业用途。

---

**注意事项：**
1. 这是**AutoCAD插件**，不是独立桌面软件
2. **仅支持Windows**（Mac不支持.NET API）
3. 需要AutoCAD 2024/2025运行环境
4. 使用AutoCAD官方引擎，100%准确DWG读取
5. 对于已有AutoCAD的建筑设计公司，这是$0成本解决方案
