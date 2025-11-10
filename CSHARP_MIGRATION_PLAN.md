# C# Avalonia UI 完整迁移计划

## 项目概述

**目标**：将Python + PyQt6项目完整迁移到C# + Avalonia UI，1:1还原所有功能

**技术栈**：
- **UI框架**：Avalonia UI 11.x（跨平台XAML）
- **DWG引擎**：Aspose.CAD for .NET 25.x
- **渲染引擎**：SkiaSharp
- **AI服务**：阿里云百炼 REST API
- **数据库**：SQLite（Microsoft.Data.Sqlite）
- **语言**：C# 12 (.NET 8)
- **架构**：MVVM + Dependency Injection

---

## 功能清单（1:1迁移）

### 模块1：DWG处理 (src/dwg/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `parser.py` | `Services/DwgParserService.cs` | ezdxf DWG解析 | P0 |
| `parser_aspose.py` | `Services/AsposeDwgParser.cs` | Aspose.CAD DWG解析（主力） | P0 |
| `entities.py` | `Models/DwgEntities.cs` | DWG实体数据模型 | P0 |
| `renderer.py` | `Controls/DwgCanvas.cs` | Skia渲染引擎 | P0 |
| `spatial_index.py` | `Services/SpatialIndexService.cs` | R-tree空间索引 | P1 |
| `text_extractor.py` | `Services/TextExtractor.cs` | 文本提取 | P1 |
| `text_classifier.py` | `Services/TextClassifier.cs` | 文本分类 | P2 |
| `smart_translator.py` | `Services/SmartTranslator.cs` | 智能翻译 | P1 |
| `translation_pipeline.py` | `Services/TranslationPipeline.cs` | 翻译流水线 | P1 |
| `precision_modifier.py` | `Services/PrecisionModifier.cs` | 精度修改 | P2 |

### 模块2：翻译引擎 (src/translation/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `engine.py` | `Services/TranslationEngine.cs` | 翻译引擎核心 | P0 |
| `cache.py` | `Services/CacheService.cs` | LRU缓存系统 | P0 |
| `quality_control.py` | `Services/QualityControl.cs` | 翻译质量控制 | P1 |

### 模块3：算量模块 (src/calculation/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `component_recognizer.py` | `Services/ComponentRecognizer.cs` | 基础构件识别 | P1 |
| `advanced_recognizer.py` | `Services/AdvancedRecognizer.cs` | 高级构件识别 | P1 |
| `ultra_precise_recognizer.py` | `Services/UltraPreciseRecognizer.cs` | 超高精度识别 | P1 |
| `quantity_calculator.py` | `Services/QuantityCalculator.cs` | 工程量计算 | P1 |
| `result_validator.py` | `Services/ResultValidator.cs` | 结果验证 | P2 |

### 模块4：导出功能 (src/export/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `dwg_exporter.py` | `Services/DwgExporter.cs` | 基础DWG导出 | P1 |
| `advanced_dwg_exporter.py` | `Services/AdvancedDwgExporter.cs` | 高级DWG导出 | P1 |
| `pdf_exporter.py` | `Services/PdfExporter.cs` | PDF矢量导出 | P1 |
| `excel_exporter.py` | `Services/ExcelExporter.cs` | Excel清单导出 | P1 |

### 模块5：AI助手 (src/ai/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `ai_assistant.py` | `Services/AiAssistant.cs` | AI对话引擎 | P2 |
| `context_manager.py` | `Services/ContextManager.cs` | 上下文管理 | P2 |
| `assistant_widget.py` | `Views/AiChatView.axaml` | AI聊天界面 | P2 |

### 模块6：UI界面 (src/ui/)

| Python模块 | C# XAML视图 | 功能描述 | 优先级 |
|-----------|------------|---------|--------|
| `main_window.py` | `Views/MainWindow.axaml` | 主窗口 | P0 |
| `dwg_viewer.py` | `Views/DwgViewerView.axaml` | DWG查看器 | P0 |
| `translation.py` | `Views/TranslationPanel.axaml` | 翻译面板 | P0 |
| `calculation.py` | `Views/CalculationPanel.axaml` | 算量面板 | P1 |
| `export.py` | `Views/ExportPanel.axaml` | 导出面板 | P1 |
| `settings_dialog.py` | `Views/SettingsDialog.axaml` | 设置对话框（6选项卡） | P0 |
| `log_viewer.py` | `Views/LogViewerDialog.axaml` | 日志查看器 | P2 |
| `performance_panel.py` | `Views/PerformancePanel.axaml` | 性能监控 | P2 |
| `about.py` | `Views/AboutDialog.axaml` | 关于对话框 | P2 |
| `welcome.py` | `Views/WelcomeDialog.axaml` | 欢迎对话框 | P2 |
| `password_dialog.py` | `Views/PasswordDialog.axaml` | 密码对话框 | P2 |
| `batch_widget.py` | `Views/BatchProcessView.axaml` | 批处理界面 | P2 |
| `ai_chat_widget.py` | `Views/AiChatWidget.axaml` | AI聊天组件 | P2 |

### 模块7：工具类 (src/utils/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `config_manager.py` | `Services/ConfigManager.cs` | 配置管理 | P0 |
| `logger.py` | `Services/LoggerService.cs` | 日志系统 | P0 |
| `performance.py` | `Services/PerformanceMonitor.cs` | 性能监控 | P1 |
| `resource_manager.py` | `Services/ResourceManager.cs` | 资源管理 | P1 |
| `password_manager.py` | `Services/PasswordManager.cs` | 密码管理 | P2 |
| `progress_manager.py` | `Services/ProgressManager.cs` | 进度管理 | P1 |
| `error_recovery.py` | `Services/ErrorRecovery.cs` | 错误恢复 | P2 |

### 模块8：服务层 (src/services/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `bailian_client.py` | `Services/BailianApiClient.cs` | 百炼API客户端 | P0 |

### 模块9：批处理 (src/batch/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `processor.py` | `Services/BatchProcessor.cs` | 批处理引擎 | P2 |

### 模块10：领域模型 (src/domain/)

| Python模块 | C#模块 | 功能描述 | 优先级 |
|-----------|--------|---------|--------|
| `construction_terminology.py` | `Domain/ConstructionTerminology.cs` | 建筑术语库 | P1 |

---

## C# 项目结构

```
BiaogeCSharp/
├── BiaogeCSharp.sln                          # 解决方案文件
│
├── src/
│   ├── BiaogeCSharp/                         # 主应用项目
│   │   ├── BiaogeCSharp.csproj               # 项目文件
│   │   ├── App.axaml                         # 应用入口
│   │   ├── App.axaml.cs
│   │   ├── Program.cs                        # 主程序
│   │   │
│   │   ├── ViewModels/                       # MVVM视图模型
│   │   │   ├── ViewModelBase.cs              # ViewModel基类
│   │   │   ├── MainWindowViewModel.cs        # 主窗口VM
│   │   │   ├── DwgViewerViewModel.cs         # DWG查看器VM
│   │   │   ├── TranslationViewModel.cs       # 翻译面板VM
│   │   │   ├── CalculationViewModel.cs       # 算量面板VM
│   │   │   ├── ExportViewModel.cs            # 导出面板VM
│   │   │   ├── SettingsViewModel.cs          # 设置对话框VM
│   │   │   ├── LogViewerViewModel.cs         # 日志查看器VM
│   │   │   ├── PerformanceViewModel.cs       # 性能监控VM
│   │   │   ├── AiChatViewModel.cs            # AI聊天VM
│   │   │   └── BatchProcessViewModel.cs      # 批处理VM
│   │   │
│   │   ├── Views/                            # XAML视图
│   │   │   ├── MainWindow.axaml              # 主窗口
│   │   │   ├── MainWindow.axaml.cs
│   │   │   ├── DwgViewerView.axaml           # DWG查看器
│   │   │   ├── TranslationPanel.axaml        # 翻译面板
│   │   │   ├── CalculationPanel.axaml        # 算量面板
│   │   │   ├── ExportPanel.axaml             # 导出面板
│   │   │   ├── SettingsDialog.axaml          # 设置对话框（6选项卡）
│   │   │   ├── LogViewerDialog.axaml         # 日志查看器
│   │   │   ├── PerformancePanel.axaml        # 性能监控
│   │   │   ├── AboutDialog.axaml             # 关于对话框
│   │   │   ├── WelcomeDialog.axaml           # 欢迎对话框
│   │   │   ├── PasswordDialog.axaml          # 密码对话框
│   │   │   ├── BatchProcessView.axaml        # 批处理界面
│   │   │   └── AiChatWidget.axaml            # AI聊天组件
│   │   │
│   │   ├── Controls/                         # 自定义控件
│   │   │   ├── DwgCanvas.cs                  # DWG渲染画布（SkiaSharp）
│   │   │   ├── LayerTreeView.cs              # 图层树控件
│   │   │   ├── EntityListView.cs             # 实体列表控件
│   │   │   └── ProgressIndicator.cs          # 进度指示器
│   │   │
│   │   ├── Services/                         # 业务服务层
│   │   │   ├── DwgParserService.cs           # ezdxf解析器
│   │   │   ├── AsposeDwgParser.cs            # Aspose.CAD解析器
│   │   │   ├── DwgRenderer.cs                # DWG渲染器
│   │   │   ├── SpatialIndexService.cs        # 空间索引（R-tree）
│   │   │   ├── TextExtractor.cs              # 文本提取
│   │   │   ├── TextClassifier.cs             # 文本分类
│   │   │   ├── SmartTranslator.cs            # 智能翻译
│   │   │   ├── TranslationPipeline.cs        # 翻译流水线
│   │   │   ├── PrecisionModifier.cs          # 精度修改
│   │   │   ├── TranslationEngine.cs          # 翻译引擎核心
│   │   │   ├── CacheService.cs               # LRU缓存（SQLite）
│   │   │   ├── QualityControl.cs             # 质量控制
│   │   │   ├── ComponentRecognizer.cs        # 基础构件识别
│   │   │   ├── AdvancedRecognizer.cs         # 高级构件识别
│   │   │   ├── UltraPreciseRecognizer.cs     # 超高精度识别
│   │   │   ├── QuantityCalculator.cs         # 工程量计算
│   │   │   ├── ResultValidator.cs            # 结果验证
│   │   │   ├── DwgExporter.cs                # DWG导出
│   │   │   ├── AdvancedDwgExporter.cs        # 高级DWG导出
│   │   │   ├── PdfExporter.cs                # PDF导出
│   │   │   ├── ExcelExporter.cs              # Excel导出
│   │   │   ├── AiAssistant.cs                # AI助手
│   │   │   ├── ContextManager.cs             # 上下文管理
│   │   │   ├── BailianApiClient.cs           # 百炼API客户端
│   │   │   ├── ConfigManager.cs              # 配置管理
│   │   │   ├── LoggerService.cs              # 日志系统（Serilog）
│   │   │   ├── PerformanceMonitor.cs         # 性能监控
│   │   │   ├── ResourceManager.cs            # 资源管理
│   │   │   ├── PasswordManager.cs            # 密码管理
│   │   │   ├── ProgressManager.cs            # 进度管理
│   │   │   ├── ErrorRecovery.cs              # 错误恢复
│   │   │   └── BatchProcessor.cs             # 批处理引擎
│   │   │
│   │   ├── Models/                           # 数据模型
│   │   │   ├── DwgDocument.cs                # DWG文档模型
│   │   │   ├── DwgEntities.cs                # DWG实体（Line/Circle/Text/Polyline）
│   │   │   ├── Layer.cs                      # 图层模型
│   │   │   ├── TextStyle.cs                  # 文本样式
│   │   │   ├── TranslationResult.cs          # 翻译结果
│   │   │   ├── ComponentRecognitionResult.cs # 构件识别结果
│   │   │   ├── QuantityCalculationResult.cs  # 工程量计算结果
│   │   │   ├── ExportOptions.cs              # 导出选项
│   │   │   ├── CacheEntry.cs                 # 缓存条目
│   │   │   └── AppConfig.cs                  # 应用配置模型
│   │   │
│   │   ├── Domain/                           # 领域模型
│   │   │   ├── ConstructionTerminology.cs    # 建筑术语库
│   │   │   ├── ComponentType.cs              # 构件类型枚举
│   │   │   └── MaterialGrade.cs              # 材料等级
│   │   │
│   │   ├── Converters/                       # XAML值转换器
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   ├── NullToBoolConverter.cs
│   │   │   └── ColorToBrushConverter.cs
│   │   │
│   │   ├── Resources/                        # 资源文件
│   │   │   ├── Styles/                       # 样式文件
│   │   │   │   ├── App.axaml                 # 全局样式
│   │   │   │   └── Themes.axaml              # 主题定义
│   │   │   ├── Icons/                        # 图标资源
│   │   │   └── Localization/                 # 本地化资源
│   │   │       ├── zh-CN.resx                # 中文
│   │   │       └── en-US.resx                # 英文
│   │   │
│   │   └── Helpers/                          # 辅助类
│   │       ├── DialogHelper.cs               # 对话框辅助
│   │       ├── FileHelper.cs                 # 文件辅助
│   │       └── ValidationHelper.cs           # 验证辅助
│   │
│   └── BiaogeCSharp.Tests/                   # 单元测试项目
│       ├── BiaogeCSharp.Tests.csproj
│       ├── Services/
│       │   ├── DwgParserTests.cs
│       │   ├── TranslationEngineTests.cs
│       │   └── CacheServiceTests.cs
│       ├── ViewModels/
│       │   └── MainWindowViewModelTests.cs
│       └── TestData/
│           ├── sample.dwg
│           └── sample.dxf
│
├── docs/                                     # 文档
│   ├── ARCHITECTURE.md                       # 架构设计
│   ├── API.md                                # API文档
│   └── MIGRATION.md                          # 迁移笔记
│
├── .gitignore
├── README.md                                 # 项目说明
└── LICENSE                                   # 许可证

```

---

## NuGet依赖包

```xml
<ItemGroup>
  <!-- UI框架 -->
  <PackageReference Include="Avalonia" Version="11.0.10" />
  <PackageReference Include="Avalonia.Desktop" Version="11.0.10" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.10" />
  <PackageReference Include="Avalonia.Fonts.Inter" Version="11.0.10" />
  <PackageReference Include="Avalonia.ReactiveUI" Version="11.0.10" />

  <!-- DWG处理 -->
  <PackageReference Include="Aspose.CAD" Version="25.4.0" />

  <!-- 渲染引擎 -->
  <PackageReference Include="SkiaSharp" Version="2.88.7" />
  <PackageReference Include="SkiaSharp.Views.Avalonia" Version="2.88.7" />

  <!-- 数据库 -->
  <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />

  <!-- HTTP客户端 -->
  <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />

  <!-- JSON处理 -->
  <PackageReference Include="System.Text.Json" Version="8.0.0" />

  <!-- Excel导出 -->
  <PackageReference Include="EPPlus" Version="7.0.10" />

  <!-- PDF导出 -->
  <PackageReference Include="PdfSharp" Version="6.0.0" />

  <!-- 日志 -->
  <PackageReference Include="Serilog" Version="3.1.1" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />

  <!-- 依赖注入 -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />

  <!-- MVVM -->
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />

  <!-- 空间索引 -->
  <PackageReference Include="RBush" Version="3.2.0" />
</ItemGroup>
```

---

## 迁移实施计划（4周）

### Week 1: 基础架构 + DWG核心功能

**目标**：建立项目基础，实现DWG解析和渲染

#### Day 1: 项目初始化
- [ ] 创建Avalonia解决方案
- [ ] 配置NuGet依赖
- [ ] 设置MVVM架构基础
- [ ] 配置依赖注入（DI）
- [ ] 设置日志系统（Serilog）

#### Day 2-3: DWG解析
- [ ] 实现 `AsposeDwgParser.cs`
- [ ] 实现 `DwgEntities.cs`（Line/Circle/Text/Polyline）
- [ ] 实现 `DwgDocument.cs`
- [ ] 单元测试（加载DWG文件）

#### Day 4-5: DWG渲染引擎
- [ ] 实现 `DwgCanvas.cs`（SkiaSharp渲染）
- [ ] 实现基本实体渲染（LINE/CIRCLE/TEXT/POLYLINE）
- [ ] 实现视口变换（平移、缩放）
- [ ] 性能测试（50K实体 < 10ms）

#### Day 6-7: 主窗口UI
- [ ] 实现 `MainWindow.axaml`（3栏布局）
- [ ] 实现 `MainWindowViewModel.cs`
- [ ] 实现文件打开功能
- [ ] 实现图层控制面板

**里程碑验收**：
- ✅ 能打开DWG文件
- ✅ 精确渲染所有基本实体
- ✅ UI响应流畅
- ✅ 性能达标

---

### Week 2: 翻译引擎 + 缓存系统

**目标**：实现完整的翻译功能

#### Day 8-9: 百炼API客户端
- [ ] 实现 `BailianApiClient.cs`
- [ ] 支持多模型配置
- [ ] 实现批量翻译（50条/批）
- [ ] 错误处理和重试机制
- [ ] API连接测试

#### Day 10-11: 缓存系统
- [ ] 实现 `CacheService.cs`（SQLite）
- [ ] 实现LRU缓存算法
- [ ] 实现缓存统计（命中率）
- [ ] 实现缓存清理策略

#### Day 12-13: 翻译引擎核心
- [ ] 实现 `TranslationEngine.cs`
- [ ] 实现 `QualityControl.cs`
- [ ] 实现 `TranslationPipeline.cs`
- [ ] 集成缓存系统

#### Day 14: 翻译UI
- [ ] 实现 `TranslationPanel.axaml`
- [ ] 实现 `TranslationViewModel.cs`
- [ ] 实现进度显示
- [ ] 实现翻译结果预览

**里程碑验收**：
- ✅ 翻译功能完整工作
- ✅ 缓存命中率 > 90%
- ✅ 支持8种语言
- ✅ 成本 < ¥0.05/图纸

---

### Week 3: 算量模块 + 导出功能

**目标**：实现构件识别和多格式导出

#### Day 15-16: 构件识别
- [ ] 实现 `ComponentRecognizer.cs`
- [ ] 实现 `AdvancedRecognizer.cs`
- [ ] 实现 `UltraPreciseRecognizer.cs`
- [ ] 实现 `ResultValidator.cs`
- [ ] 实现构件类型库

#### Day 17: 工程量计算
- [ ] 实现 `QuantityCalculator.cs`
- [ ] 实现材料汇总
- [ ] 实现成本估算

#### Day 18-19: 导出功能
- [ ] 实现 `AdvancedDwgExporter.cs`（R2010/R2013/R2018/R2024）
- [ ] 实现 `PdfExporter.cs`（矢量PDF）
- [ ] 实现 `ExcelExporter.cs`（工程量清单）

#### Day 20-21: 算量+导出UI
- [ ] 实现 `CalculationPanel.axaml`
- [ ] 实现 `ExportPanel.axaml`
- [ ] 实现 `CalculationViewModel.cs`
- [ ] 实现 `ExportViewModel.cs`

**里程碑验收**：
- ✅ 构件识别准确率 > 95%
- ✅ 支持所有导出格式
- ✅ 导出文件质量合格

---

### Week 4: 设置系统 + 高级功能 + 测试

**目标**：完成设置系统和所有高级功能

#### Day 22-23: 设置系统（6选项卡）
- [ ] 实现 `SettingsDialog.axaml`（6选项卡）
  - [ ] 百炼配置选项卡
  - [ ] 翻译设置选项卡
  - [ ] 性能优化选项卡
  - [ ] 界面设置选项卡
  - [ ] 文件路径选项卡
  - [ ] 高级选项选项卡
- [ ] 实现 `SettingsViewModel.cs`
- [ ] 实现 `ConfigManager.cs`（JSON持久化）

#### Day 24: 高级功能
- [ ] 实现 `PerformanceMonitor.cs`
- [ ] 实现 `PerformancePanel.axaml`
- [ ] 实现 `LogViewerDialog.axaml`
- [ ] 实现 `AboutDialog.axaml`

#### Day 25: AI助手（可选）
- [ ] 实现 `AiAssistant.cs`
- [ ] 实现 `ContextManager.cs`
- [ ] 实现 `AiChatWidget.axaml`

#### Day 26-27: 批处理功能（可选）
- [ ] 实现 `BatchProcessor.cs`
- [ ] 实现 `BatchProcessView.axaml`

#### Day 28: 性能优化与测试
- [ ] 性能基准测试
- [ ] 内存优化
- [ ] 多线程优化（async/await）
- [ ] 集成测试
- [ ] UI/UX优化

**最终验收**：
- ✅ 所有功能1:1还原
- ✅ 性能超越Python版本3-5倍
- ✅ 跨平台运行稳定
- ✅ 代码质量达到商业标准

---

## 关键技术实现示例

### 1. DWG精确渲染（核心）

```csharp
// Controls/DwgCanvas.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using Aspose.CAD.FileFormats.Cad;
using Aspose.CAD.FileFormats.Cad.CadObjects;

public class DwgCanvas : Control
{
    private CadImage? _cadImage;
    private float _zoom = 1.0f;
    private SKPoint _offset = SKPoint.Empty;

    public static readonly StyledProperty<CadImage?> DocumentProperty =
        AvaloniaProperty.Register<DwgCanvas, CadImage?>(nameof(Document));

    public CadImage? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    static DwgCanvas()
    {
        AffectsRender<DwgCanvas>(DocumentProperty);
    }

    public override void Render(DrawingContext context)
    {
        if (Document == null) return;

        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null) return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        RenderDwg(canvas, Document);
    }

    private void RenderDwg(SKCanvas canvas, CadImage cadImage)
    {
        canvas.Clear(SKColors.DarkGray);
        canvas.Save();

        // 应用视口变换
        canvas.Translate(_offset);
        canvas.Scale(_zoom, _zoom);

        // 遍历所有实体 - 强类型渲染
        foreach (CadBaseEntity entity in cadImage.Entities)
        {
            switch (entity)
            {
                case CadLine line:
                    DrawLine(canvas, line);
                    break;

                case CadCircle circle:
                    DrawCircle(canvas, circle);
                    break;

                case CadText text:
                    DrawText(canvas, text);
                    break;

                case CadLwPolyline polyline:
                    DrawPolyline(canvas, polyline);
                    break;
            }
        }

        canvas.Restore();
    }

    private void DrawLine(SKCanvas canvas, CadLine line)
    {
        using var paint = new SKPaint
        {
            Color = GetColor(line.ColorValue),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        canvas.DrawLine(
            (float)line.FirstPoint.X,
            (float)line.FirstPoint.Y,
            (float)line.SecondPoint.X,
            (float)line.SecondPoint.Y,
            paint
        );
    }

    private void DrawCircle(SKCanvas canvas, CadCircle circle)
    {
        using var paint = new SKPaint
        {
            Color = GetColor(circle.ColorValue),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        canvas.DrawCircle(
            (float)circle.CenterPoint.X,
            (float)circle.CenterPoint.Y,
            (float)circle.Radius,
            paint
        );
    }

    private void DrawText(SKCanvas canvas, CadText text)
    {
        using var paint = new SKPaint
        {
            Color = GetColor(text.ColorValue),
            TextSize = (float)text.Height,
            IsAntialias = true
        };

        var textContent = text.DefaultValue ?? string.Empty;

        canvas.Save();
        canvas.Translate((float)text.FirstAlignmentPoint.X, (float)text.FirstAlignmentPoint.Y);
        canvas.RotateDegrees((float)text.Rotation);
        canvas.Scale(1, -1); // Y轴翻转
        canvas.DrawText(textContent, 0, 0, paint);
        canvas.Restore();
    }

    private void DrawPolyline(SKCanvas canvas, CadLwPolyline polyline)
    {
        if (polyline.Vertices.Count < 2) return;

        using var paint = new SKPaint
        {
            Color = GetColor(polyline.ColorValue),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        using var path = new SKPath();
        var firstVertex = polyline.Vertices[0];
        path.MoveTo((float)firstVertex.X, (float)firstVertex.Y);

        foreach (var vertex in polyline.Vertices.Skip(1))
        {
            path.LineTo((float)vertex.X, (float)vertex.Y);
        }

        if (polyline.Flag.HasFlag(CadPolylineFlag.Closed))
        {
            path.Close();
        }

        canvas.DrawPath(path, paint);
    }

    private SKColor GetColor(short colorValue)
    {
        // ACI颜色索引转RGB
        return colorValue switch
        {
            1 => SKColors.Red,
            2 => SKColors.Yellow,
            3 => SKColors.Green,
            4 => SKColors.Cyan,
            5 => SKColors.Blue,
            6 => SKColors.Magenta,
            7 => SKColors.White,
            _ => SKColors.White
        };
    }

    // 鼠标交互
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var delta = e.Delta.Y;
        var factor = delta > 0 ? 1.15f : 1 / 1.15f;

        _zoom *= factor;
        _zoom = Math.Clamp(_zoom, 0.01f, 100.0f);

        InvalidateVisual();
    }
}
```

### 2. 百炼API客户端

```csharp
// Services/BailianApiClient.cs
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class BailianApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BailianApiClient> _logger;
    private readonly string _apiKey;

    public BailianApiClient(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<BailianApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["Bailian:ApiKey"] ?? throw new InvalidOperationException("API Key not configured");

        _httpClient.BaseAddress = new Uri("https://dashscope.aliyuncs.com");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<List<string>> TranslateBatchAsync(
        List<string> texts,
        string targetLanguage,
        string model = "qwen-mt-plus",
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        const int batchSize = 50;

        var batches = texts.Chunk(batchSize).ToList();

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i].ToList();

            try
            {
                var request = new
                {
                    model = model,
                    input = new
                    {
                        source_language = "zh",
                        target_language = targetLanguage,
                        source_texts = batch
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(
                    "/api/v1/services/translation/batch-translate",
                    request,
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<BailianBatchResponse>(
                    cancellationToken: cancellationToken
                );

                if (result?.Output?.Translations != null)
                {
                    results.AddRange(result.Output.Translations);
                }

                // 更新进度
                progress?.Report((i + 1.0) / batches.Count * 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量翻译失败: batch {BatchIndex}", i);
                // 失败时返回原文
                results.AddRange(batch);
            }
        }

        return results;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                model = "qwen-mt-plus",
                input = new
                {
                    source_language = "zh",
                    target_language = "en",
                    source_text = "测试"
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/services/translation/translate",
                request,
                cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// 数据模型
public record BailianBatchResponse(
    BailianOutput Output,
    BailianUsage Usage
);

public record BailianOutput(
    List<string> Translations
);

public record BailianUsage(
    int InputTokens,
    int OutputTokens
);
```

### 3. MVVM主窗口

```csharp
// ViewModels/MainWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using Aspose.CAD.FileFormats.Cad;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AsposeDwgParser _dwgParser;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private CadImage? _currentDocument;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private float _zoom = 1.0f;

    public TranslationViewModel TranslationViewModel { get; }
    public CalculationViewModel CalculationViewModel { get; }
    public ExportViewModel ExportViewModel { get; }

    public MainWindowViewModel(
        AsposeDwgParser dwgParser,
        TranslationViewModel translationViewModel,
        CalculationViewModel calculationViewModel,
        ExportViewModel exportViewModel,
        ILogger<MainWindowViewModel> logger)
    {
        _dwgParser = dwgParser;
        TranslationViewModel = translationViewModel;
        CalculationViewModel = calculationViewModel;
        ExportViewModel = exportViewModel;
        _logger = logger;
    }

    [RelayCommand]
    private async Task OpenDwgFileAsync()
    {
        try
        {
            var topLevel = App.Current.MainWindow;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "打开DWG文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DWG文件")
                    {
                        Patterns = new[] { "*.dwg", "*.dxf" }
                    }
                }
            });

            if (files.Count > 0)
            {
                await LoadDwgFileAsync(files[0].Path.LocalPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开文件失败");
            await ShowErrorAsync("打开失败", ex.Message);
        }
    }

    private async Task LoadDwgFileAsync(string filePath)
    {
        IsBusy = true;
        StatusText = "正在加载DWG文件...";

        try
        {
            // 异步加载
            CurrentDocument = await Task.Run(() => _dwgParser.Parse(filePath));

            StatusText = $"加载完成：{CurrentDocument.Entities.Count} 个实体";

            _logger.LogInformation("成功加载DWG文件: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载DWG文件失败: {FilePath}", filePath);
            StatusText = "加载失败";
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(Zoom * 1.25f, 100.0f);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(Zoom / 1.25f, 0.01f);
    }

    [RelayCommand]
    private void FitToView()
    {
        // 实现自适应视图
        Zoom = 1.0f;
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        // 显示错误对话框
        await MessageBox.ShowAsync(title, message);
    }
}
```

```xml
<!-- Views/MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:BiaogeCSharp.ViewModels"
        xmlns:controls="using:BiaogeCSharp.Controls"
        x:DataType="vm:MainWindowViewModel"
        x:Class="BiaogeCSharp.Views.MainWindow"
        Title="表哥 - 建筑工程CAD翻译工具"
        Width="1400" Height="900">

    <Design.DataContext>
        <vm:MainWindowViewModel/>
    </Design.DataContext>

    <DockPanel>
        <!-- 顶部菜单栏 -->
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="文件">
                <MenuItem Header="打开DWG文件" Command="{Binding OpenDwgFileCommand}"/>
                <Separator/>
                <MenuItem Header="退出"/>
            </MenuItem>
            <MenuItem Header="视图">
                <MenuItem Header="放大" Command="{Binding ZoomInCommand}"/>
                <MenuItem Header="缩小" Command="{Binding ZoomOutCommand}"/>
                <MenuItem Header="适应窗口" Command="{Binding FitToViewCommand}"/>
            </MenuItem>
            <MenuItem Header="工具">
                <MenuItem Header="设置"/>
            </MenuItem>
        </Menu>

        <!-- 底部状态栏 -->
        <StatusBar DockPanel.Dock="Bottom">
            <TextBlock Text="{Binding StatusText}"/>
            <ProgressBar IsIndeterminate="True"
                        IsVisible="{Binding IsBusy}"
                        Width="100" Margin="10,0"/>
        </StatusBar>

        <!-- 主内容区 -->
        <Grid ColumnDefinitions="250,*,350">
            <!-- 左侧：图层控制 -->
            <Border Grid.Column="0" Background="#2D2D30" Padding="10">
                <StackPanel>
                    <TextBlock Text="图层" FontSize="18" FontWeight="Bold"
                              Foreground="White" Margin="0,0,0,10"/>
                    <!-- 图层列表 -->
                </StackPanel>
            </Border>

            <!-- 中间：DWG渲染画布 -->
            <Border Grid.Column="1" Background="Black">
                <controls:DwgCanvas Document="{Binding CurrentDocument}"/>
            </Border>

            <!-- 右侧：功能面板 -->
            <Border Grid.Column="2" Background="#252526">
                <TabControl>
                    <TabItem Header="翻译">
                        <ContentControl Content="{Binding TranslationViewModel}"/>
                    </TabItem>
                    <TabItem Header="算量">
                        <ContentControl Content="{Binding CalculationViewModel}"/>
                    </TabItem>
                    <TabItem Header="导出">
                        <ContentControl Content="{Binding ExportViewModel}"/>
                    </TabItem>
                </TabControl>
            </Border>
        </Grid>
    </DockPanel>
</Window>
```

---

## 配置文件示例

### appsettings.json

```json
{
  "Bailian": {
    "ApiKey": "sk-your-api-key-here",
    "Endpoint": "https://dashscope.aliyuncs.com",
    "MultimodalModel": "qwen-vl-max",
    "ImageTranslationModel": "qwen-vl-max",
    "TextTranslationModel": "qwen-mt-plus",
    "Timeout": 30000,
    "MaxRetries": 3
  },
  "Translation": {
    "BatchSize": 50,
    "CacheEnabled": true,
    "CacheTTL": 2592000,
    "QualityControl": true
  },
  "Performance": {
    "SpatialIndexEnabled": true,
    "EntityThreshold": 100,
    "Antialiasing": true,
    "MaxMemoryMB": 500
  },
  "UI": {
    "Theme": "Dark",
    "Language": "zh-CN"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "FilePath": "logs/biaoge-.log"
  }
}
```

---

## 启动代码

### Program.cs

```csharp
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace BiaogeCSharp;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // 配置Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("logs/biaoge-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
```

### App.axaml.cs

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace BiaogeCSharp;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;
    public static new App Current => (App)Application.Current!;
    public Window MainWindow => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置服务
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 配置
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // 日志
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // HTTP客户端
        services.AddHttpClient<BailianApiClient>();

        // 业务服务
        services.AddSingleton<AsposeDwgParser>();
        services.AddSingleton<CacheService>();
        services.AddSingleton<TranslationEngine>();
        services.AddSingleton<ComponentRecognizer>();
        services.AddSingleton<ConfigManager>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TranslationViewModel>();
        services.AddTransient<CalculationViewModel>();
        services.AddTransient<ExportViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}
```

---

## 测试策略

### 单元测试示例

```csharp
// BiaogeCSharp.Tests/Services/DwgParserTests.cs
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

public class DwgParserTests
{
    private readonly AsposeDwgParser _parser;

    public DwgParserTests()
    {
        _parser = new AsposeDwgParser(NullLogger<AsposeDwgParser>.Instance);
    }

    [Fact]
    public void Parse_ValidDwgFile_ShouldReturnDocument()
    {
        // Arrange
        var filePath = "TestData/sample.dwg";

        // Act
        var document = _parser.Parse(filePath);

        // Assert
        document.Should().NotBeNull();
        document.Entities.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_InvalidFile_ShouldThrowException()
    {
        // Arrange
        var filePath = "nonexistent.dwg";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _parser.Parse(filePath));
    }
}
```

---

## 性能目标

| 指标 | Python版本 | C#目标 | 提升倍数 |
|-----|-----------|--------|---------|
| DWG加载时间 | 2.5s | 0.6s | 4.2x |
| 50K实体渲染 | 45ms | 6ms | 7.5x |
| 内存占用 | 600MB | 150MB | 4.0x |
| 批量翻译速度 | 15s/500条 | 4s/500条 | 3.8x |
| 启动时间 | 3.2s | 0.8s | 4.0x |
| 打包大小 | 180MB | 45MB (AOT) | 4.0x |

---

## 下一步行动

1. **立即开始**：创建Avalonia项目
2. **Week 1优先级**：DWG解析和渲染（核心功能）
3. **持续集成**：每周验收里程碑
4. **质量保证**：单元测试覆盖率 > 80%

---

**文档版本**：1.0
**创建日期**：2025-11-09
**作者**：Claude Code
**状态**：待执行
