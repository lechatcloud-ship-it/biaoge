# 翻译覆盖率诊断报告

**诊断日期**: 2025-11-17
**问题**: 用户反馈"外键块的文本根本就不会翻译，很多块就不会翻译"

---

## 当前实现分析

### 文本提取 (DwgTextExtractor.cs)

#### ✅ 已支持的文本类型
1. **DBText** (单行文本) - 完全支持
2. **MText** (多行文本) - 完全支持
3. **AttributeReference** (属性文本) - 在块引用中
4. **AttributeDefinition** (属性定义) - 在块定义中
5. **Leader** (引线) - 提取Annotation内容
6. **Dimension** (标注) - 9种子类型全部支持
7. **Table** (表格) - 提取单元格文本

#### ⚠️ 部分支持
- **XRef块 (外部引用)**: 提取文本 ✅ | 更新文本 ❌ (只读限制)
- **嵌套块**: 提取文本 ✅ | 可能遗漏深层嵌套
- **动态块**: 提取文本 ✅ | 可能遗漏动态属性

#### ❌ 未支持 / 可能遗漏
1. **标注文本覆盖 (Dimension Text Override)** - 需验证
2. **多重引线 (MLeader)** - 需验证
3. **块属性 (Attribute)** 的可见性过滤 - 可能跳过不可见属性
4. **覆盖引用 (Overlay Reference)** - 与XRef类似的只读问题
5. **标注替代文本** - 用户自定义的标注文本

### 文本更新 (DwgTextUpdater.cs)

#### ✅ 可以更新
1. DBText - 完全支持
2. MText - 完全支持
3. AttributeReference - 完全支持
4. AttributeDefinition - 完全支持
5. Leader - 通过MText支持
6. Dimension - 通过TextOverride支持
7. Table - 通过Cell.TextString支持

#### ❌ 无法更新
1. **XRef块文本** - AutoCAD只读限制
2. **Overlay块文本** - AutoCAD只读限制
3. **锁定图层的文本** - 图层锁定
4. **锁定的块** - 块定义锁定

---

## 核心问题诊断

### 问题1: XRef/Overlay块文本无法翻译

**原因**: AutoCAD API不允许修改外部引用的内容
**当前行为**:
- 提取阶段：正常提取文本 ✅
- 翻译阶段：正常翻译 ✅
- 更新阶段：跳过更新 ❌ (line 144: `return false`)

**解决方案选项**:

#### 方案A: Bind XRef (推荐) ⭐
```csharp
// 翻译前自动绑定XRef为本地块
public void BindXRefsBeforeTranslation()
{
    foreach (var xref in GetAllXRefs())
    {
        if (UserWantsToTranslate(xref))
        {
            BindXRef(xref, insertBind: false); // Bind as regular block
        }
    }
}
```
**优点**: 完全解决问题，XRef内容变为可编辑
**缺点**: 改变图纸结构，增加文件大小

#### 方案B: 创建覆盖图层
```csharp
// 在XRef文本位置创建新的翻译文本
public void CreateTranslationOverlay()
{
    foreach (var xrefText in xrefTexts)
    {
        CreateOverlayText(xrefText.Position, translatedContent);
    }
}
```
**优点**: 不改变原图
**缺点**: 双重文本，混乱

#### 方案C: 仅提示用户
```csharp
// 翻译后生成XRef翻译报告
public void GenerateXRefReport()
{
    Log.Warning($"检测到{xrefCount}个XRef文本无法自动更新");
    SaveTranslationReport("xref_translations.txt");
}
```
**优点**: 简单
**缺点**: 用户体验差

**推荐**: 方案A + 用户确认

### 问题2: 嵌套块文本可能遗漏

**当前实现**:
```csharp
// DwgTextExtractor.cs line 584
foreach (ObjectId entityId in blockDef)
{
    var ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
    if (ent is BlockReference nestedBlockRef)
    {
        ProcessBlockReference(nestedBlockRef, ...); // 递归处理
    }
}
```

**潜在问题**:
- `processedBlocks` 集合可能阻止多次访问同一块定义
- 深层嵌套 (>5层) 可能超过递归限制

**验证方法**:
```csharp
// 添加嵌套深度跟踪
private void ProcessBlockReference(BlockReference blockRef, int depth = 0)
{
    if (depth > 10)
    {
        Log.Warning($"块嵌套深度超过10层: {blockRef.Name}");
        return;
    }
    // ...
}
```

### 问题3: 动态块属性可能遗漏

**动态块** (Dynamic Blocks) 包含特殊属性:
- `DynamicBlockReferenceProperty` - 动态属性 (可见性、位置等)
- 可能包含隐藏的文本内容

**解决方案**:
```csharp
if (blockRef.IsDynamicBlock)
{
    foreach (DynamicBlockReferenceProperty prop in blockRef.DynamicBlockReferencePropertyCollection)
    {
        if (prop.PropertyName.Contains("Text") || prop.PropertyName.Contains("Label"))
        {
            // 提取动态属性中的文本
        }
    }
}
```

### 问题4: MLeader (多重引线) 支持

**当前状态**: 未明确提取
**AutoCAD API**: `MLeader` 类包含文本内容

**解决方案**:
```csharp
else if (ent is MLeader mleader)
{
    // MLeader可能包含MText内容
    if (mleader.ContentType == ContentType.MTextContent)
    {
        var mtext = mleader.MText;
        texts.Add(new TextEntity
        {
            Type = TextEntityType.MLeader,
            Content = mtext?.Text ?? string.Empty,
            ...
        });
    }
}
```

---

## 8K Token限制问题

### 当前分段翻译实现

**文件**: BailianApiClient.cs::TranslateWithSegmentationAsync()
**触发条件**: 文本长度 > MaxCharsPerBatch (3500字符)

**当前策略**:
```csharp
// 按换行符分段
var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
// 累积到3500字符后分段
```

**潜在问题**:
1. 字符数 ≠ Token数 (中文1字符≈1.5 tokens，英文1字符≈0.25 tokens)
2. 3500字符可能实际超过8192 tokens
3. 没有考虑translation_options的token消耗 (~2000+ tokens for terms+tm_list)

### 优化方案

#### 动态Token估算
```csharp
private int EstimateTokens(string text)
{
    int chineseChars = text.Count(c => c > 0x4E00 && c < 0x9FA5);
    int otherChars = text.Length - chineseChars;
    return (int)(chineseChars * 1.5 + otherChars * 0.25);
}

private const int MAX_INPUT_TOKENS = 6000; // 留2000给系统参数
```

#### 并行分段翻译
```csharp
// 当前是串行翻译各段，改为并行
var tasks = segments.Select(seg => TranslateAsync(seg, ...));
var results = await Task.WhenAll(tasks);
```

---

## 翻译过滤逻辑

### 当前过滤规则 (EngineeringTranslationConfig.cs)

#### ShouldSkipText()判断:
1. 空字符串
2. 纯数字/符号
3. 纯ASCII技术标识符 (如 "ML-1", "C30")
4. 长度 < 2

**潜在问题**:
- "A/1" 轴线标注可能被跳过 (纯ASCII)
- "详见SD-102" 可能被跳过 (大部分ASCII)

### 改进方案

#### 更智能的过滤
```csharp
private bool ShouldTranslate(string text)
{
    // 1. 跳过纯技术标识符
    if (IsDrawingNumber(text)) return false; // "SD-102", "A-001"
    if (IsMaterialGrade(text)) return false; // "C30", "HRB400"
    if (IsAxisLabel(text)) return false; // "A/1", "①-①"

    // 2. 包含中文/日文/韩文 → 翻译
    if (ContainsCJK(text)) return true;

    // 3. 包含常见英文工程词汇 → 翻译
    if (ContainsEngineeringKeywords(text)) return true;

    // 4. 默认跳过
    return false;
}
```

---

## 行动计划

### 优先级P0 (立即修复)
1. ✅ **支持MLeader文本提取和更新**
2. ✅ **修复Token估算，避免超8K限制**
3. ✅ **XRef处理：添加Bind选项或用户提示**
4. ✅ **验证动态块属性提取**

### 优先级P1 (本周完成)
1. **完善翻译过滤逻辑**
2. **优化分段翻译性能 (并行化)**
3. **添加嵌套深度跟踪和诊断**

### 优先级P2 (下周完成)
1. **深度测试各类复杂图纸**
2. **编写翻译覆盖率测试用例**
3. **文档化所有支持的文本类型**

---

## 测试验证

### 创建测试图纸
1. **xref_test.dwg**: 包含外部引用块的文本
2. **nested_blocks_test.dwg**: 5层嵌套块
3. **mleader_test.dwg**: 各类多重引线
4. **dynamic_block_test.dwg**: 动态块测试
5. **mixed_test.dwg**: 综合测试 (表格、标注、块、xref)

### 诊断命令
```
BIAOGE_DIAGNOSTIC
→ 输出当前图纸的文本类型统计
→ 显示XRef数量、嵌套深度、动态块数量
```

---

**结论**: 需要立即实施P0修复，特别是MLeader支持、Token限制修复、XRef处理策略
