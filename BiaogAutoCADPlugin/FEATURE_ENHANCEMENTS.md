# 标哥AutoCAD插件 - 功能增强设计文档

基于国内热门AutoCAD插件调研，结合用户实际使用场景设计的功能增强方案。

## 📋 调研总结

### 国内热门AutoCAD插件特点

**燕秀工具箱、海龙工具箱**
- 快捷操作集成（右键菜单、工具栏）
- 批量处理功能（批量重命名、批量修改）
- 图层管理增强
- 自动化操作

**天正建筑**
- 专业的建筑设计工具
- Ribbon界面集成
- 智能识别和标注
- 图层自动化管理

**通用痛点**
- 中英文输入法切换麻烦
- 重复性操作效率低
- 图层管理复杂
- 缺少直观的可视化工具栏

---

## 🎯 功能增强清单

### 1. 右键上下文菜单集成

**目标**: 让用户选中文本后右键直接翻译，无需打开面板

**实现方案**:
```csharp
// 使用ContextMenuExtension扩展右键菜单
public class BiaogContextMenu : IExtensionApplication
{
    void IExtensionApplication.Initialize()
    {
        // 注册右键菜单扩展
        ContextMenuExtension cme = new ContextMenuExtension();
        cme.Title = "标哥翻译";

        // 添加子菜单
        MenuItem translateToChinese = new MenuItem("翻译为中文（推荐）");
        MenuItem translateToEnglish = new MenuItem("翻译为英语");
        MenuItem translateToJapanese = new MenuItem("翻译为日语");
        // ...更多语言

        // 绑定事件处理
        translateToChinese.Click += TranslateToChinese_Click;

        // 注册到AutoCAD
        RXClass rxClass = Entity.GetClass(typeof(DBText));
        Application.AddObjectContextMenuExtension(rxClass, cme);
    }
}
```

**菜单结构**:
```
右键点击文本 →
  ├─ 标哥翻译 ▶
  │   ├─ 翻译为中文（推荐）⭐
  │   ├─ 翻译为英语
  │   ├─ 翻译为日语
  │   ├─ 翻译为韩语
  │   ├─ ─────────────
  │   ├─ 翻译预览...
  │   └─ 更多语言...
  ├─ 标哥AI助手 ▶
  │   ├─ 询问AI关于此文本
  │   └─ 批量智能处理
  └─ 标哥工具 ▶
      ├─ 复制文本
      └─ 查看属性
```

**技术要点**:
- 使用 `ContextMenuExtension` 注册菜单
- 支持 `DBText`, `MText`, `AttributeReference`
- 菜单项根据选中对象类型动态显示
- 支持多选时批量翻译

---

### 2. Ribbon工具栏界面

**目标**: 提供直观的可视化操作界面

**实现方案**:
```xml
<!-- BiaogRibbon.xaml -->
<RibbonTab xmlns="http://schemas.autodesk.com/wss/xaml/ribbon"
           Text="标哥工具"
           Title="标哥 - AI智能助手"
           Id="BIAOGE_TAB">

    <!-- 翻译面板 -->
    <RibbonPanelSource Id="TRANSLATION_PANEL" Text="AI翻译">
        <RibbonRowPanel>
            <!-- 大按钮 - 快速翻译为中文 -->
            <RibbonButton Text="翻译为中文&#x0a;(推荐)"
                          Size="Large"
                          Image="Resources/translate_zh_32.png"
                          LargeImage="Resources/translate_zh_32.png"
                          CommandHandler="BiaogRibbonCommands"
                          CommandParameter="BIAOGE_TRANSLATE_ZH"
                          ToolTip="一键翻译整个图纸为简体中文"/>
        </RibbonRowPanel>

        <RibbonRowBreak/>

        <RibbonRowPanel>
            <!-- 小按钮组 -->
            <RibbonButton Text="框选翻译" Size="Standard"
                          Image="Resources/translate_selected_16.png"
                          CommandParameter="BIAOGE_TRANSLATE_SELECTED"/>
            <RibbonButton Text="全图翻译" Size="Standard"
                          Image="Resources/translate_all_16.png"
                          CommandParameter="BIAOGE_TRANSLATE"/>
        </RibbonRowPanel>

        <RibbonRowPanel>
            <RibbonButton Text="图层翻译" Size="Standard"
                          Image="Resources/translate_layer_16.png"
                          CommandParameter="BIAOGE_TRANSLATE_LAYER"/>
            <RibbonButton Text="翻译预览" Size="Standard"
                          Image="Resources/preview_16.png"
                          CommandParameter="BIAOGE_PREVIEW"/>
        </RibbonRowPanel>
    </RibbonPanelSource>

    <!-- AI助手面板 -->
    <RibbonPanelSource Id="AI_PANEL" Text="AI助手">
        <RibbonRowPanel>
            <RibbonButton Text="标哥AI&#x0a;助手"
                          Size="Large"
                          LargeImage="Resources/ai_assistant_32.png"
                          CommandParameter="BIAOGE_AI"/>
        </RibbonRowPanel>
    </RibbonPanelSource>

    <!-- 算量面板 -->
    <RibbonPanelSource Id="CALC_PANEL" Text="工程算量">
        <RibbonRowPanel>
            <RibbonButton Text="智能&#x0a;识别"
                          Size="Large"
                          LargeImage="Resources/recognize_32.png"
                          CommandParameter="BIAOGE_CALCULATE"/>
        </RibbonRowPanel>

        <RibbonRowBreak/>

        <RibbonRowPanel>
            <RibbonButton Text="快速统计" Size="Standard"
                          CommandParameter="BIAOGE_QUICKCOUNT"/>
            <RibbonButton Text="导出Excel" Size="Standard"
                          CommandParameter="BIAOGE_EXPORTEXCEL"/>
        </RibbonRowPanel>
    </RibbonPanelSource>

    <!-- 设置面板 -->
    <RibbonPanelSource Id="SETTINGS_PANEL" Text="设置">
        <RibbonRowPanel>
            <RibbonButton Text="插件设置" Size="Standard"
                          CommandParameter="BIAOGE_SETTINGS"/>
            <RibbonButton Text="快捷键" Size="Standard"
                          CommandParameter="BIAOGE_KEYS"/>
        </RibbonRowPanel>

        <RibbonRowPanel>
            <RibbonButton Text="帮助" Size="Standard"
                          CommandParameter="BIAOGE_HELP"/>
            <RibbonButton Text="关于" Size="Standard"
                          CommandParameter="BIAOGE_ABOUT"/>
        </RibbonRowPanel>
    </RibbonPanelSource>
</RibbonTab>
```

**加载代码**:
```csharp
public class RibbonManager
{
    public static void LoadRibbon()
    {
        // 加载XAML定义的Ribbon
        var ribbonControl = ComponentManager.Ribbon;
        var ribbonTab = RibbonServices.RibbonPaletteSet.RibbonControl.FindTab("BIAOGE_TAB");

        if (ribbonTab == null)
        {
            // 从资源加载XAML
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("BiaogPlugin.UI.BiaogRibbon.xaml"))
            {
                using (var reader = new StreamReader(stream))
                {
                    var xaml = reader.ReadToEnd();
                    var tab = RibbonServices.RibbonPaletteSet.CreateRibbonTab(xaml);
                    ribbonControl.Tabs.Add(tab);
                }
            }
        }
    }
}
```

**图标资源**:
- 32x32 PNG图标（大按钮）
- 16x16 PNG图标（小按钮）
- 使用Material Design风格
- Dark/Light主题自适应

---

### 3. 图层翻译功能

**目标**: 支持按图层选择性翻译，提高效率

**使用场景**:
- 建筑图纸：只翻译"墙体"图层
- 结构图纸：只翻译"梁柱"图层
- 批量处理：选择多个图层一次性翻译

**实现方案**:

**UI界面** (`LayerTranslationDialog.xaml`):
```xml
<Window x:Class="BiaogPlugin.UI.LayerTranslationDialog"
        Width="500" Height="600"
        Background="#1E1E1E"
        Title="图层翻译 - 标哥插件">
    <Grid Margin="20">
        <!-- 图层列表 -->
        <ListBox x:Name="LayerListBox" SelectionMode="Multiple">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <CheckBox Content="{Binding LayerName}"
                              IsChecked="{Binding IsSelected}"
                              Foreground="White">
                        <CheckBox.ToolTip>
                            <TextBlock>
                                <Run Text="文本数量: "/>
                                <Run Text="{Binding TextCount}"/>
                                <LineBreak/>
                                <Run Text="颜色: "/>
                                <Run Text="{Binding ColorName}"/>
                            </TextBlock>
                        </CheckBox.ToolTip>
                    </CheckBox>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <!-- 统计信息 -->
        <TextBlock Text="已选择 {0} 个图层，共 {1} 个文本实体"/>

        <!-- 操作按钮 -->
        <Button Content="开始翻译" Click="TranslateButton_Click"/>
    </Grid>
</Window>
```

**命令实现**:
```csharp
[CommandMethod("BIAOGE_TRANSLATE_LAYER", CommandFlags.Modal)]
public async void TranslateByLayer()
{
    // 1. 获取所有图层
    var layers = GetAllLayers();

    // 2. 显示图层选择对话框
    var dialog = new LayerTranslationDialog(layers);
    if (dialog.ShowDialog() != true) return;

    // 3. 获取选中图层的所有文本
    var selectedLayers = dialog.SelectedLayers;
    var textEntities = ExtractTextFromLayers(selectedLayers);

    // 4. 执行翻译
    await TranslateTexts(textEntities, dialog.TargetLanguage);
}
```

---

### 4. 单文本快速翻译（双击或右键）

**目标**: 提供所见即所得的翻译体验

**实现方案**:

**双击监听器**:
```csharp
public class TextDoubleClickHandler : IExtensionApplication
{
    private DocumentCollection _docs;

    void IExtensionApplication.Initialize()
    {
        _docs = Application.DocumentManager;
        _docs.DocumentActivated += OnDocumentActivated;
    }

    private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
    {
        // 注册双击事件
        e.Document.ImpliedSelectionChanged += OnImpliedSelectionChanged;
    }

    private async void OnImpliedSelectionChanged(object sender, EventArgs e)
    {
        var doc = sender as Document;
        if (doc == null) return;

        // 检查是否启用双击翻译
        var settings = ServiceLocator.GetService<ConfigManager>();
        if (!settings.Config.EnableDoubleClickTranslation) return;

        // 获取选中的对象
        var selection = doc.Editor.SelectImplied();
        if (selection.Status != PromptStatus.OK) return;

        var objIds = selection.Value.GetObjectIds();
        if (objIds.Length != 1) return; // 只处理单个对象

        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var obj = tr.GetObject(objIds[0], OpenMode.ForRead);

            // 检查是否为文本实体
            if (obj is DBText dbText)
            {
                // 显示快速翻译菜单
                ShowQuickTranslateMenu(dbText, doc.Editor.GetPoint(new PromptPointOptions("\n")));
            }

            tr.Commit();
        }
    }
}
```

**快速翻译弹窗**:
```xml
<!-- QuickTranslatePopup.xaml -->
<Popup x:Name="QuickTranslatePopup" PlacementTarget="{Binding}">
    <Border Background="#2D2D30" BorderBrush="#0078D4" BorderThickness="2"
            CornerRadius="5" Padding="10">
        <StackPanel>
            <TextBlock Text="原文:" Foreground="#888"/>
            <TextBlock x:Name="OriginalText" Foreground="White" Margin="0,0,0,10"/>

            <TextBlock Text="翻译:" Foreground="#888"/>
            <TextBlock x:Name="TranslatedText" Foreground="#4EC9B0" FontWeight="Bold"/>

            <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                <Button Content="✓ 应用" Click="ApplyButton_Click"/>
                <Button Content="✗ 取消" Click="CancelButton_Click"/>
                <Button Content="更多语言..." Click="MoreLanguagesButton_Click"/>
            </StackPanel>
        </StackPanel>
    </Border>
</Popup>
```

**设置选项**:
```csharp
public class TranslationSettings
{
    /// <summary>
    /// 启用双击文本快速翻译
    /// </summary>
    public bool EnableDoubleClickTranslation { get; set; } = true;

    /// <summary>
    /// 双击翻译默认语言
    /// </summary>
    public string DoubleClickTargetLanguage { get; set; } = "zh";

    /// <summary>
    /// 显示翻译预览（不直接应用）
    /// </summary>
    public bool ShowTranslationPreview { get; set; } = true;
}
```

---

### 5. 翻译预览功能

**目标**: 翻译前预览效果，避免误操作

**实现方案**:

**预览对话框** (`TranslationPreviewDialog.xaml`):
```xml
<Window Width="900" Height="700" Title="翻译预览 - 标哥插件">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="5"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- 原文列表 -->
        <GroupBox Grid.Column="0" Header="原文">
            <DataGrid x:Name="OriginalGrid" ItemsSource="{Binding OriginalTexts}"
                      AutoGenerateColumns="False">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="图层" Binding="{Binding Layer}" Width="80"/>
                    <DataGridTextColumn Header="类型" Binding="{Binding Type}" Width="60"/>
                    <DataGridTextColumn Header="内容" Binding="{Binding Content}" Width="*"/>
                </DataGrid.Columns>
            </DataGrid>
        </GroupBox>

        <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Stretch"/>

        <!-- 翻译结果列表 -->
        <GroupBox Grid.Column="2" Header="翻译结果（预览）">
            <DataGrid x:Name="TranslatedGrid" ItemsSource="{Binding TranslatedTexts}"
                      AutoGenerateColumns="False">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="图层" Binding="{Binding Layer}" Width="80"/>
                    <DataGridTextColumn Header="类型" Binding="{Binding Type}" Width="60"/>
                    <DataGridTextColumn Header="内容" Binding="{Binding Content}" Width="*"/>
                    <DataGridTemplateColumn Header="操作" Width="100">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal">
                                    <Button Content="✓" ToolTip="确认翻译"
                                            Click="ConfirmButton_Click"/>
                                    <Button Content="✗" ToolTip="跳过"
                                            Click="SkipButton_Click"/>
                                    <Button Content="✎" ToolTip="手动编辑"
                                            Click="EditButton_Click"/>
                                </StackPanel>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </GroupBox>

        <!-- 底部操作栏 -->
        <StackPanel Grid.Row="1" Grid.ColumnSpan="3" Orientation="Horizontal">
            <Button Content="全部应用" Click="ApplyAllButton_Click"/>
            <Button Content="应用选中项" Click="ApplySelectedButton_Click"/>
            <Button Content="取消" Click="CancelButton_Click"/>

            <Separator/>

            <TextBlock Text="预览模式"/>
            <ComboBox>
                <ComboBoxItem Content="对照模式" IsSelected="True"/>
                <ComboBoxItem Content="仅显示译文"/>
                <ComboBoxItem Content="差异高亮"/>
            </ComboBox>
        </StackPanel>
    </Grid>
</Window>
```

**命令实现**:
```csharp
[CommandMethod("BIAOGE_PREVIEW", CommandFlags.Modal)]
public async void ShowTranslationPreview()
{
    // 1. 提取所有文本
    var extractor = new DwgTextExtractor();
    var textEntities = await Task.Run(() => extractor.ExtractAllText());

    // 2. 执行翻译（不应用）
    var engine = new TranslationEngine(...);
    var translations = await engine.TranslateBatchWithCacheAsync(...);

    // 3. 显示预览对话框
    var preview = new TranslationPreviewDialog
    {
        OriginalTexts = textEntities,
        TranslatedTexts = translations
    };

    if (preview.ShowDialog() == true)
    {
        // 4. 应用用户确认的翻译
        var confirmedItems = preview.ConfirmedItems;
        ApplyTranslations(confirmedItems);
    }
}
```

---

### 6. 创新功能集成

基于调研发现的用户痛点，集成以下创新功能：

#### 6.1 智能输入法切换

**功能**: 输入命令时自动切换英文，编辑文本时切换中文

**实现**:
```csharp
public class InputMethodManager
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmSetConversionStatus(IntPtr hIMC, int fdwConversion, int fdwSentence);

    public static void SwitchToEnglish()
    {
        var hWnd = GetForegroundWindow();
        var hIMC = ImmGetContext(hWnd);

        // 关闭中文输入
        ImmSetConversionStatus(hIMC, 0, 0);
    }

    public static void SwitchToChinese()
    {
        var hWnd = GetForegroundWindow();
        var hIMC = ImmGetContext(hWnd);

        // 开启中文输入
        ImmSetConversionStatus(hIMC, 1, 1);
    }
}

// 监听命令行事件
public class CommandLineMonitor
{
    void Initialize()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;

        // 命令开始 - 切换到英文
        doc.CommandWillStart += (s, e) => InputMethodManager.SwitchToEnglish();

        // 命令结束 - 恢复中文
        doc.CommandEnded += (s, e) => InputMethodManager.SwitchToChinese();
    }
}
```

#### 6.2 翻译历史记录

**功能**: 记录所有翻译操作，支持一键恢复

**实现**:
```csharp
public class TranslationHistory
{
    public class HistoryRecord
    {
        public DateTime Timestamp { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public string TargetLanguage { get; set; }
        public ObjectId ObjectId { get; set; }
    }

    private List<HistoryRecord> _records = new List<HistoryRecord>();

    public void AddRecord(HistoryRecord record)
    {
        _records.Add(record);
        SaveToDatabase();
    }

    public void UndoLastTranslation()
    {
        if (_records.Count == 0) return;

        var last = _records.Last();
        // 恢复原文
        UpdateText(last.ObjectId, last.OriginalText);

        _records.RemoveAt(_records.Count - 1);
    }
}
```

#### 6.3 批量文本替换增强

**功能**: 结合AI的智能批量替换

**实现**:
```csharp
[CommandMethod("BIAOGE_SMART_REPLACE", CommandFlags.Modal)]
public async void SmartReplace()
{
    // 1. 用户输入查找内容
    var findText = GetUserInput("查找内容:");

    // 2. AI智能建议替换内容
    var aiSuggestions = await GetAISuggestions(findText);

    // 3. 显示替换对话框
    var dialog = new SmartReplaceDialog
    {
        FindText = findText,
        Suggestions = aiSuggestions
    };

    if (dialog.ShowDialog() == true)
    {
        // 4. 执行批量替换
        BatchReplace(findText, dialog.ReplaceText);
    }
}
```

#### 6.4 翻译质量评估

**功能**: AI评估翻译质量并给出建议

**实现**:
```csharp
public class TranslationQualityAssessor
{
    public async Task<QualityReport> AssessQuality(string original, string translated)
    {
        var prompt = $@"
评估以下翻译质量：
原文：{original}
译文：{translated}

请从以下维度评估（1-5分）：
1. 准确性（是否准确传达原意）
2. 流畅性（译文是否通顺）
3. 专业性（是否使用专业术语）
4. 格式还原（是否保持原格式）

输出JSON格式：
{{
    ""accuracy"": 5,
    ""fluency"": 4,
    ""professionalism"": 5,
    ""format"": 5,
    ""suggestions"": ""建议..."
}}
";

        var result = await _bailianClient.ChatAsync(prompt, "qwen3-max-preview");
        return JsonSerializer.Deserialize<QualityReport>(result);
    }
}
```

---

## 🛠️ 实现优先级

### Phase 1 - 核心体验（本次迭代）
1. ✅ 右键上下文菜单集成
2. ✅ Ribbon工具栏界面
3. ✅ 图层翻译功能

### Phase 2 - 用户体验增强
4. ⏳ 单文本快速翻译（双击/右键）
5. ⏳ 翻译预览功能
6. ⏳ 智能输入法切换

### Phase 3 - 高级功能
7. ⏳ 翻译历史记录
8. ⏳ 批量智能替换
9. ⏳ 翻译质量评估

---

## 📐 技术架构

### 文件结构
```
BiaogPlugin/
├── UI/
│   ├── ContextMenus/
│   │   ├── TextContextMenu.cs           # 文本右键菜单
│   │   └── LayerContextMenu.cs          # 图层右键菜单
│   ├── Ribbon/
│   │   ├── BiaogRibbon.xaml            # Ribbon界面定义
│   │   ├── RibbonManager.cs            # Ribbon管理器
│   │   └── RibbonCommandHandler.cs     # 命令处理器
│   ├── Dialogs/
│   │   ├── LayerTranslationDialog.xaml # 图层翻译对话框
│   │   ├── TranslationPreviewDialog.xaml # 翻译预览
│   │   └── QuickTranslatePopup.xaml    # 快速翻译弹窗
│   └── Resources/
│       └── Icons/                       # Ribbon图标资源
├── Services/
│   ├── LayerTranslationService.cs      # 图层翻译服务
│   ├── InputMethodManager.cs           # 输入法管理
│   ├── TranslationHistory.cs           # 翻译历史
│   └── QualityAssessor.cs              # 质量评估
└── Extensions/
    ├── ContextMenuExtensions.cs        # 右键菜单扩展
    └── DoubleClickHandler.cs           # 双击处理器
```

### 关键类设计

**LayerTranslationService**:
```csharp
public class LayerTranslationService
{
    public List<LayerInfo> GetAllLayers();
    public List<DwgTextEntity> ExtractTextFromLayers(List<string> layerNames);
    public async Task TranslateLayerTexts(List<string> layerNames, string targetLang);
}
```

**RibbonManager**:
```csharp
public class RibbonManager
{
    public static void LoadRibbon();
    public static void UnloadRibbon();
    public static void UpdateRibbonState(bool enabled);
}
```

**ContextMenuManager**:
```csharp
public class ContextMenuManager
{
    public static void RegisterTextContextMenu();
    public static void UnregisterContextMenu();
    private static void OnTextContextMenu(object sender, ContextMenuEventArgs e);
}
```

---

## 🎨 UI/UX设计原则

1. **一致性**: 所有界面遵循Dark主题，使用统一的颜色方案
2. **简洁性**: 操作步骤最少化，一键完成常用操作
3. **反馈性**: 所有操作提供即时视觉反馈
4. **可预测性**: 危险操作提供预览和确认机制
5. **可配置性**: 所有智能功能可以开关

---

## 📝 配置示例

```json
{
  "UI": {
    "EnableRibbon": true,
    "EnableContextMenu": true,
    "EnableDoubleClickTranslation": true,
    "ShowTranslationPreview": true
  },
  "InputMethod": {
    "AutoSwitch": true,
    "CommandModeIME": "英文",
    "TextModeIME": "中文"
  },
  "Translation": {
    "DefaultTargetLanguage": "zh",
    "EnableHistory": true,
    "HistoryMaxSize": 1000,
    "EnableQualityAssessment": false
  }
}
```

---

## 🚀 部署说明

**Ribbon资源打包**:
1. 将XAML文件设置为"嵌入的资源"
2. 图标文件添加到Resources文件夹
3. 在csproj中配置资源清单

**安装检查**:
- 检查AutoCAD版本（2024+）
- 检测.NET Framework版本（4.8+）
- 验证Ribbon控件可用性

---

## 📊 性能优化

1. **延迟加载**: Ribbon界面延迟加载，减少启动时间
2. **异步操作**: 所有翻译操作异步执行，不阻塞UI
3. **缓存策略**: 翻译结果缓存，提高响应速度
4. **批量处理**: 优化批量翻译性能，减少API调用

---

## ✅ 测试清单

### 右键菜单测试
- [ ] DBText右键菜单显示
- [ ] MText右键菜单显示
- [ ] AttributeReference右键菜单显示
- [ ] 多选时菜单行为
- [ ] 菜单项点击响应

### Ribbon测试
- [ ] Ribbon标签页正确显示
- [ ] 所有按钮图标正确加载
- [ ] 按钮点击执行正确命令
- [ ] Ribbon状态更新

### 图层翻译测试
- [ ] 图层列表正确显示
- [ ] 多选图层功能
- [ ] 翻译进度显示
- [ ] 翻译结果正确应用

### 预览功能测试
- [ ] 对照模式显示
- [ ] 单项确认/跳过
- [ ] 批量应用
- [ ] 取消操作

---

**版本**: v1.2.0
**更新日期**: 2025-01-11
**作者**: 标哥AI助手团队
