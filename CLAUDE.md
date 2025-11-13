# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 项目概述

**标哥AutoCAD插件** - 基于AutoCAD .NET API的专业建筑工程AI智能助手插件，集成AI翻译、智能Agent、构件识别算量功能。

### 核心技术栈
- **语言**: C# (.NET Framework 4.8)
- **UI框架**: WPF (Windows Presentation Foundation)
- **AutoCAD API**: acdbmgd.dll, acmgd.dll, AcCoreMgd.dll (支持AutoCAD 2018-2024)
- **AI服务**: 阿里云百炼 Flash系列模型 (qwen-mt-flash, qwen3-max-preview, qwen3-vl-flash, qwen3-coder-flash)
- **数据库**: SQLite (翻译缓存)
- **日志**: Serilog
- **Excel**: EPPlus

---

## 构建和开发命令

### 开发环境要求
- Visual Studio 2022+
- AutoCAD 2018-2024 (任一版本即可)
- .NET Framework 4.8
- Windows 10/11 (64-bit)

### 构建命令

```bash
# 【推荐】构建插件Bundle (生成完整的插件包)
cd BiaogAutoCADPlugin
.\build-bundle.bat

# 构建安装程序 (生成安装程序EXE)
.\build-installer.ps1

# 使用dotnet CLI构建
cd BiaogAutoCADPlugin/src/BiaogPlugin
dotnet restore
dotnet build --configuration Release

# 清理构建产物
.\clean-dist.ps1
```

### 调试

1. 在Visual Studio中打开 `BiaogAutoCADPlugin.sln`
2. 按F5启动调试（会自动启动AutoCAD）
3. 在AutoCAD命令行输入 `NETLOAD`
4. 选择 `BiaogPlugin.dll`
5. 输入命令如 `BIAOGE_HELP` 测试

### 测试

```bash
# API连接测试
cd BiaogAutoCADPlugin
.\test-api.ps1

# 诊断测试
.\诊断测试.ps1

# 在AutoCAD中运行诊断
# 命令: BIAOGE_DIAGNOSTIC
```

---

## 代码架构

### 项目结构

```
BiaogAutoCADPlugin/
├── src/BiaogPlugin/                  # 主插件项目
│   ├── PluginApplication.cs          # 插件入口 (IExtensionApplication)
│   ├── Commands.cs                   # AutoCAD命令集 (30+命令)
│   ├── Services/                     # 业务逻辑层 (26个服务)
│   │   ├── AIAssistantService.cs     # AI Agent核心实现
│   │   ├── BailianApiClient.cs       # 百炼API统一客户端
│   │   ├── TranslationEngine.cs      # 翻译引擎
│   │   ├── TranslationController.cs  # 翻译流程控制
│   │   ├── CacheService.cs           # SQLite缓存服务
│   │   ├── DwgTextExtractor.cs       # DWG文本提取
│   │   ├── DwgTextUpdater.cs         # DWG文本更新
│   │   └── ...
│   ├── UI/                           # WPF用户界面
│   │   ├── AIPalette.xaml(.cs)       # AI助手主界面
│   │   ├── TranslationPalette.xaml   # 翻译工具面板
│   │   ├── SettingsDialog.xaml       # 设置对话框
│   │   └── ...
│   ├── Models/                       # 数据模型
│   └── Extensions/                   # 扩展功能
└── Installer-GUI/                    # 安装程序项目
```

### 核心架构原则

#### 1. AutoCAD .NET API 使用模式

所有DWG操作必须遵循标准模式:

```csharp
// 标准事务模式 (Transaction Pattern)
var doc = Application.DocumentManager.MdiActiveDocument;
var db = doc.Database;

using (var tr = db.TransactionManager.StartTransaction())
{
    // 读取/修改DWG数据
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    // ...

    tr.Commit(); // 或 tr.Abort()
}

// 写入操作需要文档锁定
using (var docLock = doc.LockDocument())
{
    using (var tr = db.TransactionManager.StartTransaction())
    {
        // 修改DWG数据
        tr.Commit();
    }
}
```

#### 2. 异步命令处理

AutoCAD命令支持异步操作:

```csharp
[CommandMethod("BIAOGE_TRANSLATE_ZH", CommandFlags.Modal)]
public async void QuickTranslateToChinese()
{
    try
    {
        // 异步操作...
        await TranslationController.TranslateCurrentDrawing("zh");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "翻译失败");
        ed.WriteMessage($"\n翻译失败: {ex.Message}");
    }
}
```

**重要**: 命令方法可以是 `async void`，但内部必须妥善处理异常。

#### 3. AI Agent 工作流 (阿里云百炼最佳实践)

实现在 `AIAssistantService.cs`，遵循5步工作流:

```
第1步: 工具定义 → GetAvailableTools()
第2步: 消息初始化 → BuildAgentSystemPrompt()
第3步: Agent决策 → _bailianClient.ChatCompletionStreamAsync()
第4步: 工具执行 → ExecuteTool()
第5步: 总结反馈 → 返回结果
```

#### 4. 服务定位模式

使用 `ServiceLocator` 管理依赖:

```csharp
// 服务注册 (PluginApplication.Initialize())
ServiceLocator.Register(new BailianApiClient(_sharedHttpClient));
ServiceLocator.Register(new CacheService());
ServiceLocator.Register(new TranslationEngine());

// 服务获取
var apiClient = ServiceLocator.Get<BailianApiClient>();
```

#### 5. 缓存机制

- **存储位置**: `~/.biaoge/cache.db` (SQLite)
- **TTL**: 30天自动过期
- **索引**: 复合索引 (source_text, target_language)
- **线程安全**: 使用 `SemaphoreSlim` 保护初始化

#### 6. 配置管理

- **存储位置**: `~/.biaoge/config.json`
- **类型**: 强类型配置 (PluginConfig.cs)
- **访问**: `ConfigManager.Instance.Config`

---

## 关键技术细节

### AutoCAD版本兼容性

项目使用 `.NET Framework 4.8` 编译，兼容 AutoCAD 2018-2024:

- AutoCAD 2018-2020 → 使用 `Contents/2018/` 目录
- AutoCAD 2021-2024 → 使用 `Contents/2021/` 目录

`.csproj` 文件自动检测AutoCAD安装路径 (优先2021):

```xml
<AcadVersion Condition="'$(AcadVersion)' == '' And Exists('C:\Program Files\Autodesk\AutoCAD 2021\acdbmgd.dll')">2021</AcadVersion>
<AcadPath Condition="'$(AcadVersion)' == '2021'">C:\Program Files\Autodesk\AutoCAD 2021</AcadPath>
```

### 线程安全

1. **静态HttpClient**: 避免Socket耗尽
2. **锁机制**: 保护API密钥、Token统计
3. **SemaphoreSlim**: 异步初始化
4. **文档锁定**: 防止AutoCAD冲突
5. **事务管理**: 保证原子性

### 异常处理

- **全局捕获**: `CommandExceptionHandler.ExecuteSafely()`
- **API重试**: 指数退避机制 (最多3次)
- **日志记录**: Serilog结构化日志

### 性能优化

1. **缓存**: 双级缓存 (内存 + SQLite)
2. **批量处理**: 批量去重、并行翻译
3. **异步延迟初始化**: 快速启动
4. **Token管理**: 上下文长度控制 (256K限制)

---

## 常用命令和快捷键

### 核心命令

| 命令 | 快捷键 | 功能 |
|-----|-------|------|
| `BIAOGE_TRANSLATE_ZH` | `BTZ` | 一键翻译为中文 (推荐) |
| `BIAOGE_TRANSLATE_EN` | `BTE` | 一键翻译为英语 |
| `BIAOGE_TRANSLATE_SELECTED` | `BTS` | 框选翻译 |
| `BIAOGE_AI` | `BAI` | 启动AI助手 |
| `BIAOGE_SETTINGS` | `BS` | 打开设置 |
| `BIAOGE_HELP` | `BH` | 显示帮助 |
| `BIAOGE_DIAGNOSTIC` | - | 运行诊断 |

### 快捷键安装

```
命令: BIAOGE_INSTALL_KEYS
→ 自动备份 acad.pgp
→ 添加快捷键配置
→ 运行 REINIT 命令重新加载
```

---

## 开发规范

### 添加新命令

```csharp
// 在Commands.cs中添加
[CommandMethod("BIAOGE_NEWFEATURE", CommandFlags.Modal)]
public async void NewFeature()
{
    try
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;

        ed.WriteMessage("\n执行新功能...");

        // 实现逻辑...

        ed.WriteMessage("\n完成!");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "新功能执行失败");
        ed.WriteMessage($"\n错误: {ex.Message}");
    }
}
```

### 添加新服务

1. 在 `Services/` 目录创建服务类
2. 在 `PluginApplication.Initialize()` 中注册:
   ```csharp
   ServiceLocator.Register(new MyNewService());
   ```
3. 在需要的地方获取:
   ```csharp
   var service = ServiceLocator.Get<MyNewService>();
   ```

### DWG操作

- **必须使用事务** (`Transaction`)
- **写入必须加锁** (`doc.LockDocument()`)
- **及时释放资源** (使用 `using` 语句)
- **错误处理** (捕获异常并记录日志)

### AI Agent工具调用

添加新工具函数:

1. 在 `AIAssistantService.GetAvailableTools()` 中定义工具:
   ```csharp
   new Tool {
       Name = "my_new_tool",
       Description = "描述工具功能",
       Parameters = new { /* JSON Schema */ }
   }
   ```

2. 在 `ExecuteTool()` 中实现工具逻辑:
   ```csharp
   case "my_new_tool":
       return await HandleMyNewTool(arguments);
   ```

### 日志规范

```csharp
using Serilog;

// 信息日志
Log.Information("翻译完成: {Count}个文本", count);

// 调试日志
Log.Debug("缓存命中: {Text}", text);

// 错误日志
Log.Error(ex, "API调用失败");
```

---

## 分发流程

### 构建完整分发包

```powershell
# 1. 构建插件Bundle
.\build-bundle.bat

# 2. 构建安装程序
.\build-installer.ps1

# 3. 打包完整版本 (带使用说明)
.\打包完整安装程序.ps1

# 4. 分发桌面上的文件夹或ZIP
```

### dist/ 目录结构

```
dist/
├── 安装程序.exe              # 智能安装程序 (73MB)
├── BiaogPlugin.bundle/        # 完整插件包 (22MB)
│   ├── PackageContents.xml
│   └── Contents/
│       ├── 2018/              # AutoCAD 2018-2020
│       └── 2021/              # AutoCAD 2021-2024
└── README.md
```

---

## 阿里云百炼 Flash 系列模型

### 模型选择策略

| 模型 | 用途 | 特性 |
|-----|------|------|
| `qwen-mt-flash` | 文本翻译 | 92语言，术语定制，极低成本 |
| `qwen3-max-preview` | AI Agent核心 | 思考模式，256K上下文 |
| `qwen3-vl-flash` | 视觉识别 | 空间感知，2D/3D定位 |
| `qwen3-coder-flash` | 工具调用 | 仓库级别理解 |

### API配置

- **端点**: `https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions`
- **协议**: OpenAI兼容模式
- **认证**: `Authorization: Bearer sk-xxx`

---

## 故障排除

### 插件加载失败

1. 检查.NET版本: `dotnet --version` (应显示8.0.x)
2. 检查AutoCAD版本: 支持2018-2024
3. 查看日志: `%APPDATA%\Biaoge\Logs\`
4. 使用 `NETLOAD` 手动加载查看详细错误

### API调用失败

1. 运行 `BIAOGE_DIAGNOSTIC` 诊断
2. 检查API密钥: `BIAOGE_SETTINGS`
3. 测试连接: `.\test-api.ps1`

### 依赖程序集加载失败

插件使用动态程序集解析 (`OnAssemblyResolve`)，如果仍失败:

1. 检查 `Contents/2018/` 或 `Contents/2021/` 目录是否包含所有依赖DLL
2. 确保 `System.Memory.dll`, `System.Text.Json.dll` 等关键依赖存在

---

## 重要文件路径

### 配置和数据
- 配置文件: `%USERPROFILE%\.biaoge\config.json`
- 缓存数据库: `%USERPROFILE%\.biaoge\cache.db`
- 日志文件: `%APPDATA%\Biaoge\Logs\BiaogPlugin-yyyyMMdd.log`

### 安装位置
- 插件安装目录: `C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\`
- AutoCAD支持目录: `%APPDATA%\Autodesk\ApplicationPlugins\`

---

## 参考文档

- [AutoCAD .NET API文档 (2025)](https://help.autodesk.com/view/OARX/2025/ENU/)
- [阿里云百炼API文档](https://help.aliyun.com/zh/model-studio/)
- [项目详细文档](BiaogAutoCADPlugin/README.md)
- [产品设计文档](BiaogAutoCADPlugin/PRODUCT_DESIGN.md)
- [Flash模型规格](BiaogAutoCADPlugin/FLASH_MODELS_SPEC.md)
- [构建流程说明](BiaogAutoCADPlugin/构建流程说明.md)

---

## 开发最佳实践

1. **始终使用事务模式** 操作DWG数据
2. **写入操作必须加文档锁**
3. **异步命令方法妥善处理异常**
4. **使用ServiceLocator获取服务**
5. **记录详细日志** (Serilog)
6. **编写单元测试** (如适用)
7. **遵循AutoCAD API最佳实践**
8. **保持代码与阿里云百炼官方最佳实践一致**
