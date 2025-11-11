# 标哥 - AutoCAD .NET 插件版本

**专业的建筑工程CAD图纸翻译和算量工具 - AutoCAD插件**

基于 **AutoCAD .NET API** 开发，实现100%准确的DWG文件读取和处理。

---

## 🎯 技术选型说明

### 为什么选择AutoCAD .NET API而非Aspose.CAD？

| 特性 | Aspose.CAD | AutoCAD .NET API (本项目) |
|------|-----------|--------------------------|
| **DWG读取准确度** | 不确定，有数据丢失风险 | **100%准确** ✅ |
| **许可证成本** | $999/年 | 免费（公司已有AutoCAD）✅ |
| **运行环境** | 独立应用 | AutoCAD进程内插件 ✅ |
| **用户体验** | 需切换应用 | 无缝集成AutoCAD ✅ |
| **行业认可** | 通用库 | 行业标准（天正CAD、3D3S等）✅ |
| **文档支持** | 第三方 | Autodesk官方完整文档 ✅ |

### 关键优势

1. **100%准确的DWG读取** - 使用与AutoCAD相同的官方DWG引擎
2. **无额外成本** - 建筑设计公司已有AutoCAD授权
3. **行业标准做法** - 中国建筑行业著名插件都采用此方案
4. **完美集成** - 无缝嵌入AutoCAD工作流

---

## ✨ 功能特性

### 🤖 AI智能翻译

- ✅ **8种语言支持**：中/英/日/韩/法/德/西/俄
- ✅ **批量翻译**：50条/批，高效并发
- ✅ **智能缓存**：90%+命中率，降低API成本
- ✅ **质量控制**：格式保留、术语一致性
- ✅ **实时更新DWG**：翻译后直接更新AutoCAD图纸

### 📊 构件识别算量

- ✅ **超高精度识别**：99.9999%准确率目标
- ✅ **多策略融合**：正则表达式 + AI + 规范约束
- ✅ **建筑规范验证**：GB 50854-2013等标准
- ✅ **工程量计算**：自动计算体积、面积、费用
- ✅ **材料汇总**：生成详细的材料清单

### 📤 多格式导出

- ✅ **Excel导出**：工程量清单
- ✅ **PDF报告**：算量报告
- ✅ **CSV数据**：原始数据导出

---

## 🚀 快速开始

### 系统要求

- **AutoCAD 2024/2025** (支持.NET Framework 4.8 或 .NET 8)
- **Windows 10/11** (x64)
- **.NET Framework 4.8** 或 **.NET 8.0**
- **阿里云百炼API密钥** (用于AI翻译)

### 安装插件

#### 方法1：NETLOAD命令（开发/测试）

1. 打开AutoCAD
2. 命令行输入：`NETLOAD`
3. 选择 `BiaogPlugin.dll`
4. 插件加载成功！

#### 方法2：自动加载（生产环境）

1. 将 `BiaogPlugin.bundle` 文件夹复制到：
   ```
   C:\ProgramData\Autodesk\ApplicationPlugins\
   ```

2. 重启AutoCAD，插件自动加载

### 使用插件

#### 翻译功能

```
AutoCAD命令：BIAOGE_TRANSLATE
```

1. 打开DWG图纸
2. 执行命令 `BIAOGE_TRANSLATE`
3. 在右侧面板选择目标语言
4. 点击"开始翻译"
5. 翻译完成后，图纸自动更新

#### 算量功能

```
AutoCAD命令：BIAOGE_CALCULATE
```

1. 打开DWG图纸
2. 执行命令 `BIAOGE_CALCULATE`
3. 选择识别模式（快速/标准/超高精度）
4. 查看识别结果和工程量统计
5. 导出Excel清单

#### 设置

```
AutoCAD命令：BIAOGE_SETTINGS
```

配置：
- 阿里云百炼API密钥
- 翻译模型选择
- 缓存设置
- 算量规则

---

## 🏗️ 技术架构

### 核心技术栈

```
UI层
├── WPF (Windows Presentation Foundation)
├── AutoCAD PaletteSet (工具面板)
└── 模态对话框

业务逻辑层
├── AutoCAD .NET API (DWG读写)
├── 翻译引擎 (批量处理+质量控制)
└── 算量引擎 (超高精度构件识别)

服务层
├── 阿里云百炼 REST API
├── SQLite缓存
└── Serilog日志

数据层
├── AutoCAD Database (DWG数据)
├── SQLite (翻译缓存)
└── JSON配置文件
```

### 项目结构

```
BiaogAutoCADPlugin/
├── src/
│   └── BiaogPlugin/
│       ├── BiaogPlugin.csproj         # 插件项目（DLL）
│       ├── PluginApplication.cs       # 插件入口（IExtensionApplication）
│       ├── Commands.cs                # AutoCAD命令定义
│       │
│       ├── UI/                        # WPF用户界面
│       │   ├── TranslationPalette.xaml
│       │   ├── CalculationPalette.xaml
│       │   └── SettingsDialog.xaml
│       │
│       ├── Services/                  # 业务逻辑
│       │   ├── DwgTextExtractor.cs    # DWG文本提取（AutoCAD API）
│       │   ├── TranslationEngine.cs   # 翻译引擎
│       │   ├── BailianApiClient.cs    # 百炼API客户端
│       │   ├── CacheService.cs        # SQLite缓存
│       │   ├── ComponentRecognizer.cs # 构件识别
│       │   └── ConfigManager.cs       # 配置管理
│       │
│       ├── Models/                    # 数据模型
│       │   ├── TextEntity.cs
│       │   ├── TranslationResult.cs
│       │   └── ComponentRecognitionResult.cs
│       │
│       └── Utilities/
│           └── AutoCADHelper.cs       # AutoCAD操作辅助类
│
├── tests/                             # 单元测试
├── docs/                              # 文档
└── README.md                          # 本文档
```

---

## 🔧 开发指南

### 环境搭建

1. **安装Visual Studio 2022**
   - 工作负载：.NET桌面开发

2. **安装AutoCAD SDK**
   - 从NuGet安装：`AutoCAD.NET.Core` 和 `AutoCAD.NET.Model`
   - 或手动引用AutoCAD安装目录的DLL

3. **配置项目**
   ```xml
   <PropertyGroup>
     <TargetFramework>net48</TargetFramework> <!-- AutoCAD 2024 -->
     <!-- 或 -->
     <TargetFramework>net8.0-windows</TargetFramework> <!-- AutoCAD 2025 -->
     <PlatformTarget>x64</PlatformTarget>
   </PropertyGroup>
   ```

4. **引用AutoCAD程序集**
   ```xml
   <ItemGroup>
     <Reference Include="acdbmgd">
       <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll</HintPath>
       <Private>False</Private>
     </Reference>
     <Reference Include="acmgd">
       <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acmgd.dll</HintPath>
       <Private>False</Private>
     </Reference>
     <Reference Include="AcCoreMgd">
       <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\AcCoreMgd.dll</HintPath>
       <Private>False</Private>
     </Reference>
   </ItemGroup>
   ```

### 调试配置

在Visual Studio中设置调试：

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <StartAction>Program</StartAction>
  <StartProgram>C:\Program Files\Autodesk\AutoCAD 2024\acad.exe</StartProgram>
  <StartArguments>/nologo /b "startup.scr"</StartArguments>
</PropertyGroup>
```

### 关键API使用

#### 访问当前DWG数据库

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

Document doc = Application.DocumentManager.MdiActiveDocument;
Database db = doc.Database;

using (Transaction tr = db.TransactionManager.StartTransaction())
{
    // 读取/修改DWG数据

    tr.Commit(); // 或 tr.Abort()
}
```

#### 遍历所有实体

```csharp
BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
BlockTableRecord btr = (BlockTableRecord)tr.GetObject(
    bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

foreach (ObjectId objId in btr)
{
    Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;

    // 处理不同类型实体
    if (ent is Line line) { /* ... */ }
    else if (ent is Circle circle) { /* ... */ }
    // ...
}
```

#### 修改文本内容

```csharp
// 单行文本
DBText dbText = tr.GetObject(objectId, OpenMode.ForWrite) as DBText;
dbText.TextString = "新文本";

// 多行文本
MText mText = tr.GetObject(objectId, OpenMode.ForWrite) as MText;
mText.Contents = "新文本";
```

---

## 📊 性能指标

| 指标 | 目标 | 说明 |
|------|------|------|
| DWG读取准确度 | **100%** | 使用AutoCAD官方引擎 |
| 文本提取速度 | < 1s | 50K实体图纸 |
| 批量翻译速度 | 4s/500条 | 含缓存查询 |
| 缓存命中率 | > 90% | 重复文本自动复用 |
| 内存占用 | < 200MB | 正常工作负载 |
| AutoCAD响应 | 无卡顿 | 异步处理 |

---

## 🔐 API密钥配置

### 方法1：环境变量（推荐开发环境）

```bash
setx DASHSCOPE_API_KEY "sk-your-api-key-here"
```

### 方法2：插件设置（推荐生产环境）

1. 执行命令：`BIAOGE_SETTINGS`
2. 切换到"阿里云百炼"选项卡
3. 输入API密钥
4. 点击"测试连接"验证
5. 保存设置

配置文件位置：`%APPDATA%\Biaoge\config.json`

---

## 📚 参考资源

### 官方文档

- [AutoCAD .NET API文档 (2025)](https://help.autodesk.com/view/OARX/2025/ENU/)
- [AutoCAD开发者中心](http://autodesk.com/developautocad)
- [阿里云百炼API文档](https://help.aliyun.com/zh/dashscope/)

### 中文资源

- [CAD开发者社区](https://www.cadn.net.cn/) - 中文教程和问题交流
- [AutoCAD .NET API学习指南](https://www.cnblogs.com/junqilian/archive/2012/04/23/2466723.html)

### 示例项目

- [Through the Interface Blog](https://through-the-interface.typepad.com/) - Kean Walmsley的AutoCAD开发博客
- [AutoCAD DevBlog](https://adndevblog.typepad.com/autocad/)

---

## 🐛 常见问题

### Q: 插件加载失败？

A: 检查以下项目：
1. 确认AutoCAD版本与插件.NET Framework版本匹配
2. 确认所有依赖DLL都在插件目录
3. 检查AutoCAD命令行的错误信息
4. 使用`NETLOAD`命令手动加载，查看详细错误

### Q: 翻译后中文乱码？

A: 确保以下设置：
1. DWG文件的文本样式字体支持中文（如"宋体"）
2. 百炼API返回的文本编码正确
3. AutoCAD语言设置为中文

### Q: 性能优化建议？

A:
1. 开启翻译缓存（默认开启）
2. 使用批量处理模式
3. 大图纸分图层翻译
4. 定期清理过期缓存

### Q: 如何处理密码保护的DWG？

A: AutoCAD .NET API可以访问已在AutoCAD中打开的DWG，因此：
1. 先在AutoCAD中手动打开密码保护的DWG
2. 输入密码
3. 然后运行插件命令

---

## 📄 许可证

商业软件 - 版权所有 © 2025

未经授权不得用于商业用途。

---

## 🙏 致谢

- [Autodesk AutoCAD](https://www.autodesk.com/products/autocad/) - 官方DWG引擎
- [阿里云百炼](https://dashscope.aliyun.com/) - AI翻译服务
- AutoCAD开发者社区 - 技术支持和经验分享

---

<div align="center">

**标哥 - 专业建筑工程CAD工具的AutoCAD .NET实现**

使用AutoCAD官方API实现100%准确的DWG处理

</div>
