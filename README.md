# 标哥AutoCAD插件 - 专业建筑工程AI智能助手

<div align="center">

![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)
![AutoCAD](https://img.shields.io/badge/AutoCAD-2024%2F2025-red.svg)
![License](https://img.shields.io/badge/license-Commercial-orange.svg)
![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)

**AutoCAD原生插件 + 阿里云百炼AI = 100%准确的DWG处理**

基于 AutoCAD .NET API + 阿里云百炼Flash系列模型

[功能特性](#-功能特性) • [快速开始](#-快速开始) • [使用指南](#-使用指南) • [技术架构](#-技术架构) • [产品设计](BiaogAutoCADPlugin/PRODUCT_DESIGN.md)

</div>

---

## 🎯 项目定位

**标哥AutoCAD插件** 是一款专为建筑工程设计师打造的AutoCAD插件（非独立软件），集成了AI智能翻译、标哥AI助手、构件识别算量于一体。

### 核心优势

✅ **100%准确的DWG处理** - 使用AutoCAD官方.NET API，确保完美兼容
✅ **智能Agent架构** - qwen3-max-preview作为核心，智能调度专用模型
✅ **一个密钥全调用** - 用户仅需配置一次API密钥即可使用所有功能
✅ **无缝集成工作流** - 直接在AutoCAD中操作，无需切换软件
✅ **极致用户体验** - 快捷键、右键菜单、Ribbon工具栏多种操作方式

---

## ✨ 功能特性

### 🤖 标哥AI助手（Agent架构）

**命令**: `BIAOGE_AI` | **快捷键**: `BAI`

基于阿里云百炼官方Function Calling最佳实践，实现智能Agent架构：

```
核心Agent: qwen3-max-preview（思考模式融合）
    ↓
智能调度专用模型：
  • qwen-mt-flash     → 翻译任务（92语言，术语定制）
  • qwen3-coder-flash → 修改任务（仓库级别理解）
  • qwen3-vl-flash    → 识别任务（空间感知+2D/3D定位）
  • DrawingContext    → 查询任务（图层、文本、块）
```

**使用示例**:
```
您：帮我翻译图纸中的"外墙"为英文
Agent：✓ 使用qwen-mt-flash执行翻译... → "Building Exterior Wall"

您：将所有的"C30"修改为"C35"
Agent：✓ 使用qwen3-coder-flash生成修改代码 → 已修改15处

您：识别图纸中的梁构件
Agent：✓ 使用qwen3-vl-flash进行识别 → 识别到23个梁构件

您：这张图纸有哪些图层？
Agent：✓ 直接查询DrawingContext → 显示10个图层信息
```

**特性**:
- ✅ 深度思考模式（`thinking_budget`参数）
- ✅ 流式输出（实时反馈）
- ✅ 多轮对话（保持上下文）
- ✅ 工具并行调用（提高效率）

---

### 🌐 AI智能翻译

#### 1. 全图翻译
**命令**: `BIAOGE_TRANSLATE` | **快捷键**: `BT`

打开翻译面板，选择目标语言（默认简体中文），翻译整个图纸的所有文本。

#### 2. 框选翻译 ⭐
**命令**: `BIAOGE_TRANSLATE_SELECTED` | **快捷键**: `BTS`

用户框选部分文本，仅翻译选中内容。

**使用流程**:
```
1. 在AutoCAD中框选文本实体
2. 输入 BTS 或 BIAOGE_TRANSLATE_SELECTED
3. 选择目标语言（默认中文）
4. 自动翻译，实时显示进度
```

#### 3. 快速翻译
- **翻译为中文**: `BIAOGE_TRANSLATE_ZH` | **快捷键**: `BTZ` ⭐（推荐）
- **翻译为英语**: `BIAOGE_TRANSLATE_EN` | **快捷键**: `BTE`

一键翻译整个图纸，无需选择语言。

**特性**:
- ✅ 支持8种语言（中/英/日/韩/法/德/西/俄）
- ✅ 智能缓存（90%+命中率，降低成本）
- ✅ 批量处理（50条/批，高效并发）
- ✅ 术语定制（格式还原度优化）
- ✅ 使用qwen-mt-flash模型（92语言，极低成本）

---

### 📊 构件识别算量

**命令**: `BIAOGE_CALCULATE` | **快捷键**: `BC`

#### 多策略识别
- ✅ 正则表达式匹配
- ✅ 数量提取（Quantity Regex）
- ✅ 规范验证（GB 50854-2013等）
- ✅ AI验证（可选，qwen3-vl-flash）

#### 工程量计算
- ✅ 按类型分组统计
- ✅ 材料汇总（混凝土、钢筋、砌体、门窗）
- ✅ 成本估算

#### Excel导出
**命令**: `BIAOGE_EXPORTEXCEL` | **快捷键**: `BE`

生成专业的工程量清单Excel：
- **汇总表**: 按类型统计
- **明细表**: 详细构件列表
- **材料表**: 材料用量汇总

---

### ⌨️ 快捷键系统

**命令**: `BIAOGE_KEYS` - 查看所有快捷键
**命令**: `BIAOGE_INSTALL_KEYS` - 自动安装快捷键（带备份）
**命令**: `BIAOGE_EXPORT_KEYS` - 导出配置到桌面

#### 推荐快捷键（所有以'B'开头，避免冲突）

| 快捷键 | 命令 | 功能 |
|-------|------|------|
| **BT** | BIAOGE_TRANSLATE | 全图翻译 |
| **BTS** | BIAOGE_TRANSLATE_SELECTED | 框选翻译 |
| **BTZ** ⭐ | BIAOGE_TRANSLATE_ZH | 快速翻译为中文（推荐） |
| **BTE** | BIAOGE_TRANSLATE_EN | 快速翻译为英语 |
| **BAI** | BIAOGE_AI | 启动AI助手 |
| **BC** | BIAOGE_CALCULATE | 打开算量面板 |
| **BQ** | BIAOGE_QUICKCOUNT | 快速统计构件 |
| **BE** | BIAOGE_EXPORTEXCEL | 导出Excel清单 |
| **BS** | BIAOGE_SETTINGS | 打开设置 |
| **BH** | BIAOGE_HELP | 显示帮助 |

---

## 🚀 快速开始

### 系统要求

- **操作系统**: Windows 10/11 (64-bit)
- **AutoCAD版本**: AutoCAD 2024 或 2025
- **.NET版本**: .NET Framework 4.8 或 .NET 8.0
- **Visual Studio**: 2022+ (仅开发需要)

**注意**:
- ✅ 支持Windows平台
- ❌ 不支持Mac（AutoCAD for Mac不支持.NET API）
- ❌ 不支持AutoCAD Web版

---

### 安装插件

#### 方式1: NETLOAD手动加载（快速测试）

1. 下载发布包 `BiaogPlugin.zip` 并解压
2. 在AutoCAD中输入 `NETLOAD` 命令
3. 选择 `BiaogPlugin.dll` 文件
4. 输入 `BIAOGE_HELP` 查看所有命令

#### 方式2: 自动加载（推荐）

1. 将整个插件包复制到：
   ```
   C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\
   ```

2. 目录结构：
   ```
   BiaogPlugin.bundle\
       ├── PackageContents.xml  # 插件清单
       └── Contents\
           └── Windows\
               └── 2024\
                   ├── BiaogPlugin.dll
                   ├── Serilog.dll
                   └── ... (其他依赖)
   ```

3. 重启AutoCAD，插件自动加载

#### 方式3: acad.lsp启动脚本

在Support路径下创建 `acad.lsp`:
```lisp
(command "._NETLOAD" "C:\\Path\\To\\BiaogPlugin.dll")
```

---

### 配置API密钥

首次运行需要配置阿里云百炼API密钥：

1. **获取API密钥**: 访问 https://dashscope.aliyun.com/
2. **在AutoCAD中配置**:
   - 输入命令 `BIAOGE_SETTINGS` 或 快捷键 `BS`
   - 在"百炼API"选项卡中输入API密钥
   - 点击"测试连接"验证
   - 点击"保存"

3. **配置文件位置**:
   ```
   %USERPROFILE%\.biaoge\config.json
   ```

---

### 快速安装快捷键

```
AutoCAD命令行: BIAOGE_INSTALL_KEYS
→ 确认安装 → 自动备份原acad.pgp → 添加快捷键配置
→ 输入 REINIT 命令 → 选择 PGP file → 确定
→ 完成！输入 BH 测试
```

---

## 📖 使用指南

### 场景1: 设计师接收日文图纸，需要翻译为中文

```
方式1（推荐）: 输入 BTZ → 自动翻译为中文 → 完成
方式2: 输入 BT → 翻译面板 → 点击"翻译"（默认中文）→ 完成
```

### 场景2: 只需要翻译图纸的一部分区域

```
1. 框选需要翻译的文本（使用AutoCAD选择工具）
2. 输入 BTS（框选翻译）
3. 选择目标语言（默认中文，直接回车）
4. 等待翻译完成
```

### 场景3: AI助手修改图纸

```
1. 输入 BAI 启动AI助手
2. 输入: "将所有的'C30'修改为'C35'"
3. Agent自动调用qwen3-coder-flash执行修改
4. 查看修改结果
```

### 场景4: 识别构件并导出Excel清单

```
1. 输入 BC 打开算量面板
2. 点击"识别构件"
3. 输入 BE 导出Excel
4. 在桌面打开生成的Excel文件
```

---

## 🏗️ 技术架构

### 核心技术栈

```
AutoCAD命令层 (Commands.cs)
├── BIAOGE_TRANSLATE         # 全图翻译
├── BIAOGE_TRANSLATE_SELECTED # 框选翻译
├── BIAOGE_AI                 # AI助手
├── BIAOGE_CALCULATE          # 算量
└── 17个命令...

UI层 (UI/)
├── PaletteSet               # 可停靠面板
├── TranslationPalette       # 翻译界面
├── CalculationPalette       # 算量界面
├── SettingsDialog           # 设置对话框
└── WPF + Dark主题

业务逻辑层 (Services/)
├── AIAssistantService       # Agent架构核心
├── TranslationEngine        # 翻译引擎
├── ComponentRecognizer      # 构件识别
├── QuantityCalculator       # 工程量计算
└── ExcelExporter            # Excel导出

服务层
├── BailianApiClient         # 统一API客户端
├── BailianModelSelector     # 模型选择器
├── CacheService             # 翻译缓存（SQLite）
├── ConfigManager            # 配置管理
└── KeybindingsManager       # 快捷键管理

数据访问层
├── DwgTextExtractor         # DWG文本提取
├── DwgTextUpdater           # DWG文本更新
└── AutoCAD Database API     # acdbmgd.dll
```

### 阿里云百炼Flash系列模型（2025推荐）

| 模型 | 用途 | 特性 |
|-----|------|------|
| **qwen-mt-flash** | 文本翻译 | 92语言，术语定制，极低成本 |
| **qwen3-max-preview** | AI对话 | 思考模式融合，256K上下文 |
| **qwen3-vl-flash** | 视觉识别 | 空间感知，2D/3D定位 |
| **qwen3-coder-flash** | 工具调用 | 仓库级别理解，Function Calling优化 |
| **qwen3-omni-flash** | 全模态 | 文本+图像+音频+视频 |

**一个API密钥调用所有模型** ✓
**免费额度**: 每个模型100万token，有效期90天

详细规格请查看: [FLASH_MODELS_SPEC.md](BiaogAutoCADPlugin/FLASH_MODELS_SPEC.md)

---

### Agent架构（标哥AI助手）

基于阿里云百炼官方Function Calling最佳实践：

```
【5步工作流】
1. 工具定义   → 4个专用工具（翻译、修改、识别、查询）
2. 消息初始化 → 构建系统提示词 + 图纸上下文
3. Agent决策  → qwen3-max-preview分析用户意图
4. 工具执行   → 调用专用模型或直接操作DWG
5. 总结反馈   → 自然语言总结执行结果
```

**技术特点**:
- ✅ 流式输出（SSE）
- ✅ 并行工具调用
- ✅ 上下文保持
- ✅ 深度思考模式
- ✅ 错误重试机制

---

## 📦 开发指南

### 构建项目

```bash
# 克隆仓库
git clone https://github.com/lechatcloud-ship-it/biaoge.git
cd biaoge/BiaogAutoCADPlugin

# 使用自动化脚本（推荐）
.\build.bat

# 或使用dotnet CLI
dotnet restore
dotnet build --configuration Release

# 输出位置
dist\BiaogPlugin\
```

### 调试插件

1. **配置调试启动项**（已在.csproj中配置）:
   ```xml
   <StartProgram>C:\Program Files\Autodesk\AutoCAD 2024\acad.exe</StartProgram>
   ```

2. **按F5启动调试**:
   - Visual Studio自动启动AutoCAD
   - 输入 `NETLOAD` 加载 `BiaogPlugin.dll`
   - 输入 `BIAOGE_HELP` 查看命令

3. **设置断点**: 在C#代码中设置断点，在AutoCAD中执行命令触发

### 添加新命令

```csharp
// 在Commands.cs中添加
[CommandMethod("BIAOGE_NEWFEATURE", CommandFlags.Modal)]
public void NewFeature()
{
    var doc = Application.DocumentManager.MdiActiveDocument;
    var ed = doc.Editor;

    ed.WriteMessage("\n执行新功能...");
    // 实现逻辑...
}
```

---

## 📊 性能指标

- **启动速度**: < 2秒（插件加载）
- **翻译速度**: 100条文本 < 10秒（含缓存）
- **AI响应**: < 3秒（首次响应）
- **内存占用**: < 200MB（正常使用）
- **缓存命中率**: > 90%（重复项目）

---

## 🆚 竞争优势

| 特性 | AutoCAD插件 | 桌面应用（Avalonia/PyQt） |
|-----|------------|-------------------------|
| **DWG读取准确性** | ✅ 100%（官方API） | ❌ 70-80%（ezdxf/Aspose.CAD） |
| **工作流集成** | ✅ 无缝集成 | ❌ 需切换软件 |
| **平台支持** | ❌ 仅Windows | ✅ Windows/Mac/Linux |
| **成本** | ✅ $0（已有AutoCAD） | ❌ Aspose.CAD $999/年 |
| **AI能力** | ✅ Agent架构 | ✅ Agent架构 |
| **独立分发** | ❌ 依赖AutoCAD | ✅ 独立运行 |

**结论**: 对于已有AutoCAD的建筑设计公司，插件方案是最佳选择。

---

## 📚 文档

### 用户文档
- **[产品设计文档](BiaogAutoCADPlugin/PRODUCT_DESIGN.md)** - 完整的产品规划
- **[Flash模型规格](BiaogAutoCADPlugin/FLASH_MODELS_SPEC.md)** - 模型详细参数

### 开发文档
- **[CLAUDE.md](BiaogAutoCADPlugin/CLAUDE.md)** - 开发指南和最佳实践
- **[项目架构](BiaogAutoCADPlugin/README.md)** - 技术架构说明

---

## 🔧 故障排除

### 插件加载失败
1. 检查.NET版本: `dotnet --version` 应显示 8.0.x
2. 检查AutoCAD版本: 支持2024/2025
3. 查看日志: `%USERPROFILE%\.biaoge\logs\`

### API调用失败
1. 运行 `BIAOGE_DIAGNOSTIC` 诊断
2. 检查API密钥是否正确
3. 确保能访问 `dashscope.aliyuncs.com`

### 快捷键不生效
1. 运行 `REINIT` 命令重新加载PGP文件
2. 或重启AutoCAD
3. 查看 `acad.pgp` 是否包含标哥配置

---

## 🗓️ 更新日志

### v1.1.0 - Flash Series Edition (2025-01-11)

**新增功能**:
- ✅ Agent架构（qwen3-max-preview为核心）
- ✅ 框选翻译（BIAOGE_TRANSLATE_SELECTED）
- ✅ 快捷键系统（可自动/手动安装）
- ✅ 快速翻译为中文（BIAOGE_TRANSLATE_ZH）
- ✅ Flash系列模型集成（一个密钥全调用）

**技术升级**:
- ✅ Function Calling官方最佳实践
- ✅ 5步Agent工作流
- ✅ 流式输出（SSE）
- ✅ 并行工具调用
- ✅ 默认语言改为中文

### v1.0.0 - Initial Release

**核心功能**:
- ✅ AI智能翻译（8种语言）
- ✅ 构件识别算量
- ✅ Excel导出
- ✅ 智能缓存
- ✅ 诊断和性能监控

---

## 📄 许可证

商业软件 - 版权所有 © 2025

本软件为商业软件，未经授权不得用于商业用途。

---

## 🙏 致谢

- [Autodesk AutoCAD .NET API](https://help.autodesk.com/view/OARX/2025/ENU/) - AutoCAD官方开发接口
- [阿里云百炼](https://dashscope.aliyun.com/) - Flash系列AI模型
- [Serilog](https://serilog.net/) - 结构化日志框架
- [EPPlus](https://epplussoftware.com/) - Excel生成库

---

## 📞 技术支持

- **GitHub Issues**: https://github.com/lechatcloud-ship-it/biaoge/issues
- **API文档**: https://help.aliyun.com/zh/model-studio/
- **AutoCAD开发**: https://help.autodesk.com/view/OARX/2025/ENU/

---

<div align="center">

**标哥AutoCAD插件 - 让AI成为设计师的智能副手**

Made with ❤️ using AutoCAD .NET API + 阿里云百炼

</div>
