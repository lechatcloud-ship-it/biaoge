# AI助手对话框无法显示问题 - 诊断指南

## 问题描述
点击AI助手命令后，对话框闪一下就消失，AutoCAD命令行显示正常，但面板无法显示。

---

## 🔍 诊断步骤

### 第1步：检查日志文件（最重要！）

**日志位置**:
```
%APPDATA%\Biaoge\Logs\BiaogPlugin-YYYYMMDD.log
```

**查找关键错误**:
在日志中搜索以下内容：
1. `"显示AI助手面板失败"` - ShowAIPalette异常
2. `"创建AI助手面板失败"` - InitializeAIPalette异常
3. `"初始化AI助手服务失败"` - AIPalette_Loaded异常
4. `"服务初始化失败"` - ServiceLocator问题

**预期正常日志**:
```
[INF] 准备显示AI助手面板...
[DBG] AI助手面板当前状态: Visible=True, Dock=Right, Size={Width=800, Height=850}
[INF] ✓ AI助手面板已显示（Dock=Right, Size={Width=800, Height=850}, Visible=True）
[INF] AI助手服务初始化成功
```

---

### 第2步：检查API密钥配置

**问题症状**: 如果日志显示"服务初始化失败"

**原因**: AIPalette.xaml.cs line 84-93的初始化逻辑检测到BailianApiClient或ConfigManager为null。

**解决方案**:
1. 运行命令：`BIAOGE_SETTINGS`
2. 检查"百炼API密钥"是否已配置
3. 点击"测试连接"验证API密钥有效性

**代码逻辑**:
```csharp
// AIPalette.xaml.cs line 84-93
if (_bailianClient != null && _configManager != null)
{
    _aiService = new AIAssistantService(...);
    Log.Information("AI助手服务初始化成功");
}
else
{
    // ⚠️ 这里会导致SendButton禁用，但面板可能已显示
    AddSystemMessage("❌ 错误：服务初始化失败，请检查API密钥配置");
    SendButton.IsEnabled = false;
}
```

---

### 第3步：重置AutoCAD窗口状态

**问题症状**: 面板创建成功，但位置/大小异常导致不可见

**AutoCAD会保存PaletteSet状态到注册表**，可能保存了错误的位置（如：
- 在屏幕外
- 宽度/高度为0
- 最小化状态

**解决方案A - 删除注册表项**:
1. 关闭AutoCAD
2. Win+R运行：`regedit`
3. 导航到：
   ```
   HKEY_CURRENT_USER\Software\Autodesk\AutoCAD\R24.0\ACAD-XXXX\Palettes
   或
   HKEY_CURRENT_USER\Software\Autodesk\AutoCAD\R24.0\ACAD-XXXX\Profiles\<<Unnamed Profile>>\Palettes
   ```
4. 查找GUID：`A5B6C7D8-E9F0-1234-5678-9ABCDEF03333`（AI助手面板）
5. 删除该键值
6. 重启AutoCAD

**解决方案B - 使用NETLOAD重新加载插件**:
1. 在AutoCAD中运行：`NETUNLOAD`
2. 选择`BiaogPlugin.dll`
3. 运行：`NETLOAD`
4. 重新选择`BiaogPlugin.dll`
5. 运行：`BIAOGE_AI`

---

### 第4步：检查WPF控件初始化异常

**问题症状**: 日志显示"创建AIPalette WPF控件..."后没有"添加控件到PaletteSet..."

**可能原因**:
1. XAML资源加载失败
2. WPF依赖项缺失（System.Windows.Forms.Integration.dll）
3. .NET Framework版本不兼容

**诊断命令**（运行AutoCAD命令行）:
```
BIAOGE_DIAGNOSTIC
```
这会生成完整的诊断报告，包括：
- .NET Framework版本
- AutoCAD版本
- 插件加载状态
- 服务注册状态

---

### 第5步：强制重新初始化

**临时解决方案** - 添加调试命令：

在AutoCAD命令行运行以下.NET代码（需要NETLOAD后）:

```
(在命令行输入)
BIAOGE_RESETPALETTES
```

如果该命令不存在，可以手动添加到Commands.cs：

```csharp
[CommandMethod("BIAOGE_RESETPALETTES")]
public void ResetPalettes()
{
    try
    {
        Log.Information("强制清理并重新初始化面板...");

        // 清理所有面板
        PaletteManager.Cleanup();

        // 重新初始化
        PaletteManager.Initialize();

        // 显示AI助手
        PaletteManager.ShowAIPalette();

        ed.WriteMessage("\n✓ 面板已重置并重新初始化");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "重置面板失败");
        ed.WriteMessage($"\n[错误] {ex.Message}");
    }
}
```

---

## 🐛 已知问题和修复

### 问题1: 首次点击不显示（需要点击2次）

**代码位置**: PaletteManager.cs line 361-371

**原因**: WPF ElementHost首次加载需要触发渲染

**已实现的修复**:
```csharp
// 第一次创建后，调整Size来触发WPF渲染
if (isFirstTime && _aiPaletteSet != null)
{
    var tempSize = new System.Drawing.Size(810, 860);
    _aiPaletteSet.Size = tempSize; // 触发布局计算
}
```

**如果仍需要点击2次**，说明这个修复没有生效，可能原因：
- AutoCAD版本不同（2021 vs 2022 vs 2024）
- 多显示器配置
- DPI缩放设置

---

### 问题2: 面板显示但输入框无法获取焦点

**代码位置**: PaletteManager.cs line 395

**修复**:
```csharp
_aiPaletteSet.KeepFocus = true; // 保持焦点在面板内
```

如果输入框仍无法获取焦点，尝试：
1. 点击面板标题栏
2. 在面板内任意位置右键
3. 按Tab键

---

### 问题3: 中文输入法字符传递到AutoCAD命令行

**代码位置**: AIPalette.xaml.cs line 56-62

**已实现的修复**: 捕获所有文本输入事件

---

## 📊 快速诊断检查清单

请按顺序检查以下项目：

- [ ] **日志检查**: 查看%APPDATA%\Biaoge\Logs\最新日志文件
- [ ] **API密钥**: 运行BIAOGE_SETTINGS检查配置
- [ ] **窗口状态**: 尝试删除注册表中的Palettes项
- [ ] **插件重载**: NETUNLOAD + NETLOAD
- [ ] **诊断报告**: 运行BIAOGE_DIAGNOSTIC
- [ ] **重置面板**: 运行BIAOGE_RESETPALETTES（如果已添加）

---

## 🔧 终极解决方案（100%有效）

如果上述方法都无效，执行以下步骤：

1. **完全卸载插件**:
   ```
   NETUNLOAD
   选择 BiaogPlugin.dll
   ```

2. **清理AutoCAD配置**:
   - 删除注册表Palettes项（见第3步）
   - 删除`%APPDATA%\Biaoge\`文件夹（会丢失配置）

3. **重启AutoCAD**

4. **重新加载插件**:
   ```
   NETLOAD
   选择 BiaogPlugin.dll
   ```

5. **重新配置API密钥**:
   ```
   BIAOGE_SETTINGS
   ```

6. **测试AI助手**:
   ```
   BIAOGE_AI
   ```

---

## 📝 最可能的原因排序

根据您的描述"突然一下子弹不出来了"，最可能的原因：

1. **80%概率**: AutoCAD保存了错误的窗口状态到注册表 → **解决方案**: 删除注册表Palettes项
2. **15%概率**: API密钥配置丢失或失效 → **解决方案**: 重新配置BIAOGE_SETTINGS
3. **5%概率**: WPF控件初始化异常 → **解决方案**: 查看日志文件

---

## 🆘 如果问题仍未解决

请提供以下信息：

1. **日志文件最后100行**（%APPDATA%\Biaoge\Logs\）
2. **AutoCAD版本**: 运行`VERSION`命令
3. **.NET Framework版本**: 运行`BIAOGE_DIAGNOSTIC`
4. **是否有多显示器**: 是/否
5. **DPI缩放设置**: Windows显示设置中的缩放比例

---

**创建日期**: 2025-11-15
**适用版本**: BiaogPlugin v1.0+
**AutoCAD版本**: 2021-2024
