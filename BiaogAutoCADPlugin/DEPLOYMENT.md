# AutoCAD插件部署指南

本文档提供详细的AutoCAD插件部署和安装指南。

---

## 📋 目录

1. [部署前准备](#部署前准备)
2. [构建插件](#构建插件)
3. [部署方法](#部署方法)
4. [首次配置](#首次配置)
5. [验证安装](#验证安装)
6. [故障排除](#故障排除)
7. [升级指南](#升级指南)
8. [卸载指南](#卸载指南)

---

## 部署前准备

### 系统要求

- **操作系统**: Windows 10/11 (x64)
- **AutoCAD版本**: AutoCAD 2024 或更高版本
- **.NET Framework**: 4.8 或更高版本
- **磁盘空间**: 至少50MB可用空间
- **网络**: 需要访问阿里云服务（翻译功能）

### 权限要求

- **标准用户权限**: NETLOAD手动加载
- **管理员权限**: 自动加载到ApplicationPlugins目录（推荐生产环境）

### 前置检查

1. **检查AutoCAD版本**
   ```
   在AutoCAD命令行输入: VERSION
   确认版本号为2024或更高
   ```

2. **检查.NET Framework**
   ```
   控制面板 -> 程序和功能 -> 启用或关闭Windows功能
   确认 .NET Framework 4.8 已安装
   ```

3. **获取API密钥**
   - 访问 [阿里云百炼控制台](https://dashscope.console.aliyun.com/)
   - 创建API密钥（格式：sk-xxxxxxxx）
   - 保存密钥（后续配置时需要）

---

## 构建插件

### 方法1: 使用自动化脚本（推荐）

#### Windows环境

```batch
# 在项目根目录执行
build.bat
```

构建完成后，输出文件位于: `dist/BiaogPlugin/`

#### Linux/Mac环境（交叉编译）

```bash
# 赋予执行权限
chmod +x build.sh

# 执行构建
./build.sh
```

### 方法2: Visual Studio手动构建

1. 打开Visual Studio 2022
2. 打开 `BiaogPlugin.sln`
3. 选择 **Release** 配置
4. 菜单: **Build -> Build Solution** (或按F6)
5. 输出位于: `src/BiaogPlugin/bin/Release/net48/`

### 方法3: 命令行构建

```bash
# 还原依赖
dotnet restore BiaogPlugin.sln

# 构建Release版本
dotnet build BiaogPlugin.sln --configuration Release
```

### 构建产物清单

构建成功后，确认以下文件存在：

**核心文件**:
- ✅ `BiaogPlugin.dll` - 插件主程序
- ✅ `BiaogPlugin.pdb` - 调试符号（可选）

**依赖库**:
- ✅ `Serilog.dll` - 日志系统
- ✅ `Microsoft.Data.Sqlite.dll` - SQLite数据库
- ✅ `System.Text.Json.dll` - JSON处理
- ✅ `SQLitePCLRaw.*.dll` - SQLite原生库

**注意**: AutoCAD的DLL（acdbmgd.dll等）不应包含在输出中。

---

## 部署方法

### 方法1: NETLOAD手动加载（开发/测试环境）

**适用场景**: 开发调试、临时测试

**步骤**:

1. 复制所有构建产物到目标位置（如 `C:\Plugins\BiaogPlugin\`）

2. 打开AutoCAD

3. 命令行输入:
   ```
   NETLOAD
   ```

4. 在文件选择对话框中，选择 `BiaogPlugin.dll`

5. 点击"加载"

6. 看到欢迎消息表示加载成功：
   ```
   ╔══════════════════════════════════════════════════╗
   ║      标哥 - 建筑工程CAD翻译工具 v1.0           ║
   ╚══════════════════════════════════════════════════╝
   ```

**优点**:
- ✅ 无需管理员权限
- ✅ 适合开发调试
- ✅ 可以随时卸载

**缺点**:
- ❌ 每次启动AutoCAD需要重新加载
- ❌ 不适合生产环境

---

### 方法2: ApplicationPlugins自动加载（生产环境推荐）

**适用场景**: 生产部署、多用户环境

**步骤**:

#### 2.1 创建Bundle结构

创建以下目录结构:

```
C:\ProgramData\Autodesk\ApplicationPlugins\
└── BiaogPlugin.bundle\
    ├── PackageContents.xml  (必需)
    └── Contents\
        └── Windows\
            └── 2024\  (AutoCAD版本号)
                ├── BiaogPlugin.dll
                ├── Serilog.dll
                └── ... (其他依赖DLL)
```

#### 2.2 创建PackageContents.xml

在 `BiaogPlugin.bundle\` 目录下创建 `PackageContents.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage
  SchemaVersion="1.0"
  ProductType="Application"
  Name="BiaogPlugin"
  Description="标哥 - 建筑工程CAD翻译工具"
  Author="Your Company"
  AppVersion="1.0.0"
  ProductCode="{12345678-1234-1234-1234-123456789012}"
  UpgradeCode="{12345678-1234-1234-1234-123456789012}">

  <CompanyDetails
    Name="Your Company"
    Url="https://your-website.com"
    Email="support@your-company.com" />

  <RuntimeRequirements
    OS="Win64"
    Platform="AutoCAD"
    SeriesMin="R24.0"
    SeriesMax="R25.0" />

  <Components>
    <RuntimeRequirements
      OS="Win64"
      Platform="AutoCAD"
      SeriesMin="R24.0"
      SeriesMax="R25.0" />
    <ComponentEntry
      AppName="BiaogPlugin"
      Version="1.0.0"
      ModuleName="./Contents/Windows/2024/BiaogPlugin.dll"
      AppDescription="AutoCAD翻译插件"
      LoadOnCommandInvocation="False"
      LoadOnAutoCADStartup="True" />
  </Components>
</ApplicationPackage>
```

#### 2.3 复制文件

将所有构建产物复制到:
```
C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents\Windows\2024\
```

#### 2.4 设置权限

确保普通用户有读取和执行权限:
```batch
icacls "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle" /grant Users:(OI)(CI)RX /T
```

#### 2.5 重启AutoCAD

关闭并重新启动AutoCAD，插件会自动加载。

**优点**:
- ✅ 自动加载，无需手动操作
- ✅ 适合生产环境
- ✅ 支持多用户

**缺点**:
- ❌ 需要管理员权限部署
- ❌ 升级需要重启AutoCAD

---

### 方法3: acad.lsp启动脚本加载

**适用场景**: 需要自定义启动逻辑

创建或编辑 `acad.lsp` (位于AutoCAD支持文件搜索路径):

```lisp
;; 自动加载标哥插件
(defun S::STARTUP ()
  (command "_NETLOAD" "C:\\Plugins\\BiaogPlugin\\BiaogPlugin.dll")
  (princ "\n标哥插件已加载")
)
```

---

## 首次配置

### 1. 打开设置对话框

在AutoCAD命令行输入:
```
BIAOGE_SETTINGS
```

### 2. 配置API密钥

在"百炼API"选项卡:
1. 输入阿里云百炼API密钥
2. 选择翻译模型（推荐: qwen-mt-plus）
3. 点击"测试连接"验证
4. 确认看到"✓ 连接成功"

### 3. 配置翻译选项

在"翻译设置"选项卡:
- ✅ **启用翻译缓存** - 推荐开启（提升速度，降低成本）
- ✅ **跳过纯数字文本** - 推荐开启（跳过尺寸标注）
- ✅ **跳过短文本** - 推荐开启（少于2字符）

### 4. 保存设置

点击"保存"按钮

### 5. 验证配置

配置文件位置:
```
%USERPROFILE%\.biaoge\config.json
```

打开文件确认内容类似:
```json
{
  "Bailian:ApiKey": "sk-your-api-key",
  "Bailian:TextTranslationModel": "qwen-mt-plus",
  "Translation:UseCache": true,
  "Translation:SkipNumbers": true,
  "Translation:SkipShortText": true
}
```

---

## 验证安装

### 基础验证

1. **检查插件加载**
   ```
   命令: BIAOGE_HELP
   ```
   应显示帮助信息

2. **检查版本**
   ```
   命令: BIAOGE_VERSION
   ```
   应显示版本号 1.0.0

3. **测试翻译面板**
   ```
   命令: BIAOGE_TRANSLATE
   ```
   应打开翻译工具面板

### 功能验证

1. **打开测试DWG文件**
   - 打开一个包含中文文本的DWG文件

2. **执行翻译**
   ```
   命令: BIAOGE_TRANSLATE
   ```
   - 选择目标语言: 英语
   - 点击"开始翻译"
   - 等待翻译完成

3. **检查结果**
   - 图纸中的中文应已翻译为英文
   - 查看统计信息（缓存命中率、成功率等）

### 日志验证

查看日志文件:
```
%APPDATA%\Biaoge\Logs\BiaogPlugin-YYYYMMDD.log
```

应包含类似内容:
```
2025-11-11 10:00:00.000 [INF] 标哥 - AutoCAD翻译插件正在初始化...
2025-11-11 10:00:01.000 [INF] 所有服务初始化完成
2025-11-11 10:00:02.000 [INF] API密钥已加载
```

---

## 故障排除

### 问题1: 插件加载失败

**症状**: NETLOAD后提示错误或无响应

**解决方法**:

1. **检查AutoCAD版本兼容性**
   ```
   插件需要AutoCAD 2024+
   确认: 命令行输入 VERSION
   ```

2. **检查.NET Framework版本**
   ```
   需要.NET Framework 4.8
   控制面板 -> 程序和功能 -> 启用或关闭Windows功能
   ```

3. **检查依赖DLL**
   ```
   确认所有DLL在同一目录
   特别是: Serilog.dll, Microsoft.Data.Sqlite.dll
   ```

4. **查看错误日志**
   ```
   AutoCAD命令行可能显示详细错误
   或查看: %APPDATA%\Biaoge\Logs\
   ```

---

### 问题2: 翻译功能不工作

**症状**: 点击"开始翻译"后无反应或报错

**解决方法**:

1. **验证API密钥**
   ```
   BIAOGE_SETTINGS -> 测试连接
   ```

2. **检查网络连接**
   ```
   确保可以访问 dashscope.aliyuncs.com
   ```

3. **查看错误信息**
   ```
   翻译面板的日志查看器会显示详细错误
   ```

4. **清除缓存**
   ```
   BIAOGE_CLEARCACHE
   ```

---

### 问题3: 中文显示乱码

**症状**: 翻译后的文本显示为乱码

**解决方法**:

1. **检查文本样式字体**
   ```
   AutoCAD命令: STYLE
   确保字体支持中文（如"宋体"、"黑体"）
   ```

2. **检查系统区域设置**
   ```
   控制面板 -> 区域 -> 管理 -> 更改系统区域设置
   确认为"中文(简体,中国)"
   ```

---

### 问题4: 性能问题

**症状**: 翻译速度慢或AutoCAD卡顿

**解决方法**:

1. **启用缓存**
   ```
   BIAOGE_SETTINGS -> 翻译设置 -> 启用翻译缓存
   ```

2. **分批处理**
   ```
   对于大型图纸，按图层翻译
   ```

3. **检查系统资源**
   ```
   任务管理器检查内存和CPU使用率
   ```

---

## 升级指南

### 升级步骤

1. **备份配置**
   ```
   备份: %USERPROFILE%\.biaoge\config.json
   备份: %USERPROFILE%\.biaoge\cache.db
   ```

2. **卸载旧版本**
   - 如果使用NETLOAD: 关闭AutoCAD
   - 如果使用ApplicationPlugins: 删除旧的bundle文件夹

3. **部署新版本**
   - 按照部署方法安装新版本

4. **恢复配置**
   - 配置文件通常可以直接使用
   - 或在设置对话框中重新配置

5. **验证升级**
   ```
   BIAOGE_VERSION  (检查版本号)
   BIAOGE_HELP     (测试功能)
   ```

### 兼容性注意事项

- 配置文件格式向后兼容
- 缓存数据库可能需要清空（如果结构变更）
- API密钥无需重新配置

---

## 卸载指南

### 方法1: NETLOAD加载的插件

1. 关闭AutoCAD
2. 删除插件文件夹
3. （可选）删除配置文件夹: `%USERPROFILE%\.biaoge`

### 方法2: ApplicationPlugins自动加载的插件

1. 关闭AutoCAD
2. 删除bundle文件夹:
   ```
   C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle
   ```
3. （可选）删除配置和缓存:
   ```
   %USERPROFILE%\.biaoge
   %APPDATA%\Biaoge
   ```

### 完全卸载

删除所有相关文件和数据:

```batch
@echo off
REM 删除插件文件
rmdir /s /q "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle"

REM 删除用户配置
rmdir /s /q "%USERPROFILE%\.biaoge"

REM 删除日志文件
rmdir /s /q "%APPDATA%\Biaoge"

echo 卸载完成
```

---

## 批量部署

### 企业环境批量部署脚本

```batch
@echo off
REM ================================================================
REM 标哥插件批量部署脚本 - 企业版
REM ================================================================

setlocal

REM 源文件位置（构建输出）
set SOURCE_DIR=%~dp0dist\BiaogPlugin

REM 目标位置
set DEPLOY_DIR=C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents\Windows\2024

REM 创建目录结构
echo 创建目录结构...
mkdir "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle" 2>nul
mkdir "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents" 2>nul
mkdir "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents\Windows" 2>nul
mkdir "%DEPLOY_DIR%" 2>nul

REM 复制文件
echo 复制插件文件...
xcopy "%SOURCE_DIR%\*.*" "%DEPLOY_DIR%\" /Y /I /Q

REM 复制PackageContents.xml
echo 复制配置文件...
copy "PackageContents.xml" "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\" /Y

REM 设置权限
echo 设置文件权限...
icacls "C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle" /grant Users:(OI)(CI)RX /T /Q

echo.
echo 部署完成！
echo 请重启AutoCAD以加载插件。
echo.
pause
```

### 组策略部署

通过组策略推送安装脚本到企业内所有工作站。

---

## 技术支持

如遇到问题，请收集以下信息并联系技术支持:

1. AutoCAD版本号（命令: VERSION）
2. 插件版本号（命令: BIAOGE_VERSION）
3. 日志文件: `%APPDATA%\Biaoge\Logs\BiaogPlugin-YYYYMMDD.log`
4. 配置文件: `%USERPROFILE%\.biaoge\config.json` (隐藏API密钥)
5. 错误截图或错误信息

---

## 附录

### 文件位置汇总

| 类型 | 位置 |
|------|------|
| 插件DLL | `C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents\Windows\2024\` |
| 配置文件 | `%USERPROFILE%\.biaoge\config.json` |
| 缓存数据库 | `%USERPROFILE%\.biaoge\cache.db` |
| 日志文件 | `%APPDATA%\Biaoge\Logs\` |

### AutoCAD命令汇总

| 命令 | 说明 |
|------|------|
| `BIAOGE_TRANSLATE` | 打开翻译工具面板 |
| `BIAOGE_TRANSLATE_EN` | 快速翻译为英语 |
| `BIAOGE_CALCULATE` | 打开算量工具面板 |
| `BIAOGE_SETTINGS` | 打开设置对话框 |
| `BIAOGE_HELP` | 显示帮助信息 |
| `BIAOGE_VERSION` | 显示版本信息 |
| `BIAOGE_ABOUT` | 关于插件 |
| `BIAOGE_CLEARCACHE` | 清除翻译缓存 |

---

**版权所有 © 2025 - 标哥团队**
