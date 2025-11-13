# 标哥AutoCAD插件 - AI编码助手指南

## 项目概述

标哥AutoCAD插件是一个专为建筑工程设计师打造的AutoCAD原生插件，集成了AI智能翻译、标哥AI助手、构件识别算量于一体。项目采用C# .NET Framework 4.8开发，基于AutoCAD官方.NET API，确保100%准确的DWG文件处理。

### 核心特性
- **AI智能翻译**: 支持92种语言，专业术语定制
- **标哥AI助手**: 基于阿里云百炼大模型的智能Agent架构
- **构件识别算量**: 自动识别图纸中的建筑构件并计算工程量
- **多版本兼容**: 支持AutoCAD 2018-2024版本
- **极致用户体验**: 快捷键、右键菜单、Ribbon工具栏多种操作方式

## 技术架构

### 技术栈
- **开发语言**: C# 9.0+ with .NET Framework 4.8
- **UI框架**: WPF + Windows Forms混合开发
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **HTTP客户端**: Microsoft.Extensions.Http
- **数据库**: SQLite (Microsoft.Data.Sqlite)
- **日志系统**: Serilog
- **Excel导出**: EPPlus
- **JSON处理**: System.Text.Json + Newtonsoft.Json

### 项目结构
```
BiaogAutoCADPlugin/
├── src/BiaogPlugin/              # 主插件项目
│   ├── Commands.cs               # AutoCAD命令定义
│   ├── PluginApplication.cs      # 插件入口点
│   ├── Models/                   # 数据模型
│   ├── Services/                 # 业务逻辑服务
│   ├── UI/                       # WPF用户界面
│   └── Extensions/               # 扩展方法
├── Installer-GUI/                # 安装程序项目
├── dist/                         # 构建输出目录
├── build-bundle.bat              # 插件构建脚本
└── build-installer.ps1           # 安装程序构建脚本
```

### 核心服务架构
```
AIAssistantService (Agent核心)
├── qwen3-max-preview (主Agent)
├── TranslationController (翻译调度)
├── ComponentRecognizer (构件识别)
├── QuantityCalculator (工程量计算)
└── DrawingContextManager (图纸上下文)
```

## 构建和开发流程

### 环境要求
- **Visual Studio 2022** 或更高版本
- **.NET 8.0 SDK** (用于安装程序构建)
- **AutoCAD 2018-2024** (开发调试)
- **Windows 10/11** x64

### 构建命令

#### 日常开发构建
```bash
# 构建插件DLL (输出到dist/BiaogPlugin.bundle/)
cd BiaogAutoCADPlugin
.\build-bundle.bat

# 构建安装程序 (输出到dist/安装程序.exe)
.\build-installer.ps1
```

#### 完整发布构建
```bash
# 构建插件
.\build-bundle.bat

# 构建安装程序  
.\build-installer.ps1

# 打包客户分发版本 (输出到桌面)
.\打包完整安装程序.ps1
```

### 调试配置
- **目标框架**: .NET Framework 4.8
- **平台目标**: x64
- **启动程序**: AutoCAD acad.exe (自动检测)
- **调试参数**: `/nologo`

## 代码组织规范

### 命名规范
- **命名空间**: `BiaogPlugin.*` (统一前缀)
- **类名**: PascalCase，如 `TranslationController`
- **方法名**: PascalCase，如 `TranslateDrawing()`
- **常量**: UPPER_CASE，如 `MAX_CONTEXT_LENGTH`
- **私有字段**: `_camelCase`，如 `_httpClient`

### 文件组织
```
Services/                       # 业务服务层
├── AIAssistantService.cs      # AI助手核心服务
├── TranslationController.cs   # 翻译控制器
├── BailianApiClient.cs        # 阿里云百炼API客户端
├── CacheService.cs            # SQLite缓存服务
└── ConfigManager.cs           # 配置管理

UI/                            # 用户界面层
├── TranslationPalette.xaml    # 翻译面板
├── AIPalette.xaml            # AI助手面板
├── SettingsDialog.xaml       # 设置对话框
└── Ribbon/                   # Ribbon管理

Models/                        # 数据模型层
├── TranslationModels.cs      # 翻译相关模型
├── TextEntity.cs             # 文本实体模型
└── PluginConfig.cs           # 插件配置模型
```

### AutoCAD命令命名
- **翻译命令**: `BIAOGE_TRANSLATE*` (BIAOGE前缀)
- **AI助手**: `BIAOGE_AI`
- **设置**: `BIAOGE_SETTINGS`
- **帮助**: `BIAOGE_HELP`

## 关键依赖管理

### AutoCAD API引用 (自适应版本)
项目自动检测AutoCAD 2018-2024安装路径，动态引用：
- `acdbmgd.dll` - 数据库管理
- `acmgd.dll` - AutoCAD管理
- `AcCoreMgd.dll` - 核心功能
- `Autodesk.AutoCAD.Interop` - COM互操作
- `AdWindows.dll` - Ribbon界面

### NuGet包版本锁定
```xml
<!-- HTTP客户端 - 兼容.NET Framework 4.8 -->
<PackageReference Include="Microsoft.Extensions.Http" Version="6.0.0" />

<!-- JSON处理 - 解决类型初始化异常 -->
<PackageReference Include="System.Text.Json" Version="6.0.10" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

<!-- SQLite数据库 - 提高兼容性 -->
<PackageReference Include="Microsoft.Data.Sqlite" Version="6.0.33" />

<!-- 配置管理 - 兼容版本 -->
<PackageReference Include="Microsoft.Extensions.Configuration" Version="6.0.1" />
```

## 测试策略

### 单元测试
- **服务层测试**: 独立测试TranslationController、CacheService等
- **API测试**: 验证BailianApiClient的请求/响应处理
- **配置测试**: 确保ConfigManager正确读写配置

### 集成测试
- **AutoCAD集成**: 验证命令在AutoCAD中的执行
- **UI测试**: 验证WPF面板的加载和交互
- **数据库测试**: 验证SQLite缓存功能

### 手动测试清单
```
□ 插件加载 (NETLOAD)
□ 翻译功能 (BIAOGE_TRANSLATE)
□ AI助手对话 (BIAOGE_AI)
□ 设置面板 (BIAOGE_SETTINGS)
□ 快捷键绑定
□ Ribbon工具栏
□ 多语言支持
□ 缓存机制
□ Excel导出
```

## 部署和分发

### Bundle结构
```
BiaogPlugin.bundle/
├── PackageContents.xml          # AutoCAD自动加载配置
├── README.txt                   # 安装说明
└── Contents/
    ├── 2018/                    # AutoCAD 2018-2020
    │   ├── BiaogPlugin.dll
    │   └── [50个依赖DLL]
    └── 2021/                    # AutoCAD 2021-2024
        ├── BiaogPlugin.dll
        └── [50个依赖DLL]
```

### 安装位置
```
C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\
```

### 用户配置路径
```
%APPDATA%\Biaog\
├── config.json     # 插件配置
└── cache.db        # 翻译缓存
```

## 安全考虑

### API密钥管理
- **存储位置**: `%APPDATA%\Biaog\config.json`
- **加密方式**: 明文存储 (本地使用)
- **访问控制**: 仅当前用户可读写

### 网络安全
- **HTTPS**: 所有API调用使用HTTPS
- **超时设置**: HTTP客户端超时5分钟
- **重试策略**: 实现指数退避重试

### 数据安全
- **本地缓存**: SQLite数据库存储翻译结果
- **日志脱敏**: 日志中不记录API密钥
- **异常处理**: 不暴露敏感信息给最终用户

## 性能优化

### 缓存策略
- **翻译缓存**: SQLite本地缓存，避免重复API调用
- **配置缓存**: 内存缓存配置对象
- **会话缓存**: 维护AI对话上下文

### 异步处理
- **UI响应**: 使用async/await保持UI响应
- **后台任务**: 翻译操作在后台线程执行
- **进度报告**: 实时显示操作进度

### 内存管理
- **HttpClient复用**: 静态单例避免Socket耗尽
- **及时释放**: 正确释放AutoCAD对象
- **大对象处理**: 分批处理大量文本实体

## 故障排除

### 常见构建错误
```
错误: 找不到AutoCAD引用
解决: 安装AutoCAD或设置AUTOCAD_PATH环境变量

错误: System.Memory类型初始化异常
解决: 确保所有依赖DLL版本兼容.NET Framework 4.8

错误: SQLite相关异常
解决: 检查Microsoft.Data.Sqlite版本是否为6.0.33
```

### 运行时问题
```
问题: 插件无法加载
检查: 验证PackageContents.xml格式和路径

问题: 翻译功能异常
检查: 确认API密钥配置正确
检查: 验证网络连接和API配额

问题: AI助手无响应
检查: 查看日志文件了解详细错误
检查: 确认阿里云百炼服务状态
```

## 开发最佳实践

### 代码质量
- **异常处理**: 所有用户操作都要有try-catch
- **日志记录**: 使用Serilog记录关键操作和错误
- **用户反馈**: 通过AutoCAD命令行提供清晰反馈

### 性能考虑
- **批量操作**: 尽量批量处理实体，减少数据库访问
- **异步UI**: 保持UI线程响应，避免卡顿
- **资源清理**: 及时释放COM对象和文件句柄

### 用户体验
- **快捷键**: 为常用功能提供快捷键
- **进度提示**: 长时间操作显示进度条
- **错误友好**: 用户友好的错误消息

## 版本管理

### 当前版本
- **插件版本**: 1.0.0.0
- **安装程序版本**: 随插件同步
- **API兼容性**: 阿里云百炼最新API

### 版本历史记录
版本历史文件保存在 `dist/v*.*.*-*.md`，记录每个版本的功能更新和修复内容。

---

**最后更新**: 2025年11月13日
**维护团队**: 标哥AutoCAD插件开发团队