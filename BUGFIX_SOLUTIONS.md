# 标哥AutoCAD插件 - Bug修复方案

基于对AutoCAD官方文档、阿里云百炼API文档和社区最佳实践的深入调研，以下是7个问题的完整解决方案。

---

## 问题1: Ribbon工具栏不自动显示（AutoCAD 2024兼容性）

### 根本原因
AutoCAD 2024的Ribbon初始化时间比早期版本更长，当前代码最多尝试5次激活（约0.5秒），不足以确保Ribbon完全就绪。

### 解决方案
**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/UI/Ribbon/RibbonManager.cs`

**修改位置**: `OnIdleActivateTab` 方法（第201-249行）

**关键修改**:
1. 将最大尝试次数从5次增加到20次（AutoCAD 2024需要）
2. 添加Tab存在性检查（`Ribbon.Tabs.Contains`）
3. 添加`UpdateLayout()`强制刷新
4. 重置尝试计数器避免后续调用失败

```csharp
// 修改第228行
if (_activateAttempts >= 20)  // 原本是5

// 在第220行之前添加Tab检查
if (!ComponentManager.Ribbon.Tabs.Contains(_biaogTab))
{
    Log.Warning("Tab不在Ribbon.Tabs集合中，重新添加");
    ComponentManager.Ribbon.Tabs.Add(_biaogTab);
}

// 在第223行后添加强制刷新
try
{
    ComponentManager.Ribbon.UpdateLayout();
}
catch { /* 某些版本可能不支持 */ }

// 在所有return和退出点添加计数器重置
_activateAttempts = 0;
```

---

## 问题2: BIAOGE_AI命令需要输入两次才显示

### 根本原因
`PaletteManager.ShowAIPalette()`在首次调用时执行初始化，但"强制渲染技巧"（两次Size调整 + Toggle Visible）后立即设置`Visible=false`，导致首次调用实际上不显示面板。

### 解决方案
**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/UI/PaletteManager.cs`

**修改位置**: `ShowAIPalette` 方法（第349-410行）

**关键修改**:
```csharp
// 修改第364-378行的强制渲染逻辑
if (isFirstTime)
{
    Log.Debug("AI助手面板未初始化，开始初始化...");
    InitializeAIPalette();

    // ✅ 修复：强制渲染后不要设置Visible=false
    if (_aiPaletteSet != null)
    {
        Log.Debug("第一次创建，执行强制渲染技巧...");

        // 技巧：调整两次Size（不同值）触发渲染
        var tempSize = new System.Drawing.Size(810, 860);
        _aiPaletteSet.Size = tempSize;

        // ❌ 删除：_aiPaletteSet.Visible = true;
        // ❌ 删除：_aiPaletteSet.Visible = false;

        Log.Debug("强制渲染完成");
    }
}
```

**同样修复**:
- `ShowTranslationPalette()` (第91-147行)
- `ShowCalculationPalette()` (第209-264行)

---

## 问题3+4: 实现AI助手和深度思考的流式输出

### 根本原因
当前代码没有实现阿里云百炼的SSE流式输出，所有响应都是一次性返回。

### 解决方案

#### 3.1 修改BailianApiClient - 添加流式输出支持

**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/Services/BailianApiClient.cs`

**添加新方法**:
```csharp
/// <summary>
/// 流式对话 - 使用SSE协议实时返回生成的文本
/// </summary>
/// <param name="messages">对话消息列表</param>
/// <param name="model">使用的模型</param>
/// <param name="onChunkReceived">每收到一个chunk时的回调</param>
/// <param name="onThinkingReceived">深度思考内容回调（可选）</param>
/// <param name="cancellationToken">取消令牌</param>
public async Task ChatCompletionStreamAsync(
    List<ChatMessage> messages,
    string model,
    Action<string> onChunkReceived,
    Action<string>? onThinkingReceived = null,
    CancellationToken cancellationToken = default)
{
    var apiKey = GetApiKey();
    if (string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("未配置API密钥");
    }

    var requestBody = new
    {
        model = model,
        messages = messages.Select(m => new {
            role = m.Role,
            content = m.Content
        }),
        stream = true,  // ✅ 启用流式输出
        stream_options = new { include_usage = true }  // ✅ 包含Token使用统计
    };

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
    {
        Content = JsonContent.Create(requestBody)
    };
    httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

    using var response = await _httpClient.SendAsync(
        httpRequest,
        HttpCompletionOption.ResponseHeadersRead,  // ✅ 关键：立即返回响应头
        cancellationToken);

    response.EnsureSuccessStatusCode();

    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new System.IO.StreamReader(stream);

    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
            continue;

        var json = line.Substring(6); // 移除"data: "前缀
        if (json == "[DONE]")
            break;

        try
        {
            var chunk = JsonSerializer.Deserialize<JsonElement>(json);

            // 提取delta内容
            if (chunk.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("delta", out var delta))
                {
                    // 检查是否是深度思考内容
                    if (delta.TryGetProperty("reasoning_content", out var reasoningContent))
                    {
                        var thinkingText = reasoningContent.GetString();
                        if (!string.IsNullOrEmpty(thinkingText))
                        {
                            onThinkingReceived?.Invoke(thinkingText);
                        }
                    }

                    // 常规内容
                    if (delta.TryGetProperty("content", out var content))
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            onChunkReceived(text);
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "解析SSE chunk失败: {Json}", json);
        }
    }
}
```

#### 3.2 修改AIAssistantService - 支持流式回调

**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/Services/AIAssistantService.cs`

**修改`SendMessageAsync`方法签名**:
```csharp
public async Task SendMessageAsync(
    string userMessage,
    bool enableDeepThinking = false,
    Action<string>? onContentChunk = null,  // ✅ 新增：内容流式回调
    Action<string>? onThinkingChunk = null, // ✅ 新增：思考流式回调
    CancellationToken cancellationToken = default)
{
    // ... 现有逻辑 ...

    // ✅ 使用流式API
    if (onContentChunk != null || onThinkingChunk != null)
    {
        await _bailianClient.ChatCompletionStreamAsync(
            _conversationHistory,
            model,
            onContentChunk: onContentChunk,
            onThinkingReceived: onThinkingChunk,
            cancellationToken: cancellationToken
        );
    }
    else
    {
        // 非流式模式（向后兼容）
        response = await _bailianClient.ChatCompletionAsync(...);
    }
}
```

#### 3.3 修改AIPalette.xaml.cs - 实现流式UI更新

**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/UI/AIPalette.xaml.cs`

**修改`SendMessageAsync`方法** (第351行开始):
```csharp
private async Task SendMessageAsync()
{
    // ... 现有初始化代码 ...

    try
    {
        StringBuilder fullResponse = new StringBuilder();
        StringBuilder fullThinking = new StringBuilder();

        // ✅ 创建流式渲染器
        var contentRenderer = new StreamingMarkdownRenderer(aiRichTextBox);
        StreamingMarkdownRenderer? thinkingRenderer = null;

        if (_deepThinking && thinkingRichTextBox != null)
        {
            thinkingRenderer = new StreamingMarkdownRenderer(thinkingRichTextBox);
        }

        // ✅ 发送消息并接收流式响应
        await _aiService.SendMessageAsync(
            userInput,
            enableDeepThinking: _deepThinking,
            onContentChunk: (chunk) =>
            {
                // ✅ 在UI线程上更新
                Dispatcher.Invoke(() =>
                {
                    fullResponse.Append(chunk);
                    contentRenderer.AppendText(chunk);  // 流式渲染Markdown
                    ScrollToBottom();
                });
            },
            onThinkingChunk: (chunk) =>
            {
                if (_deepThinking && thinkingRenderer != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        fullThinking.Append(chunk);
                        thinkingRenderer.AppendText(chunk);
                        ScrollToBottom();
                    });
                }
            },
            cancellationToken: _cancellationTokenSource.Token
        );

        Log.Information("流式输出完成");
    }
    catch (OperationCanceledException)
    {
        StatusText.Text = "已取消";
    }
    finally
    {
        _isProcessing = false;
        SendButton.IsEnabled = true;
    }
}
```

---

## 问题5: 块文本提取不全 - 嵌套块未完全递归

### 根本原因
`DwgTextExtractor.ExtractFromNestedBlock()`方法可能缺失或未深度递归所有层级的嵌套块。

### 解决方案

**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/Services/DwgTextExtractor.cs`

**添加/增强方法**:
```csharp
/// <summary>
/// ✅ 递归提取嵌套块内的所有文本（支持无限层级）
/// </summary>
private void ExtractFromNestedBlock(
    BlockReference blockRef,
    Transaction tr,
    List<TextEntity> texts,
    string spaceName,
    int nestingLevel = 1,  // 跟踪嵌套深度
    HashSet<ObjectId>? processedBlocks = null)  // 防止循环引用
{
    // 防止无限递归（循环块引用）
    if (nestingLevel > 100)
    {
        Log.Warning($"嵌套深度超过100层，可能存在循环引用");
        return;
    }

    processedBlocks ??= new HashSet<ObjectId>();

    // 防止重复处理同一个块定义
    if (processedBlocks.Contains(blockRef.BlockTableRecord))
    {
        return;
    }
    processedBlocks.Add(blockRef.BlockTableRecord);

    try
    {
        var blockDef = (BlockTableRecord)tr.GetObject(
            blockRef.BlockTableRecord,
            OpenMode.ForRead);

        // 跳过匿名块和特殊块
        if (blockDef.IsAnonymous || blockDef.IsLayout || blockDef.IsFromExternalReference)
        {
            return;
        }

        // 遍历块定义内的所有实体
        foreach (ObjectId objId in blockDef)
        {
            var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
            if (ent == null) continue;

            // 1. 提取直接的文本实体
            var textEntity = ExtractTextFromEntity(ent, objId);
            if (textEntity != null)
            {
                // ✅ 计算世界坐标（考虑块的变换矩阵）
                textEntity.Position = textEntity.Position.TransformBy(blockRef.BlockTransform);
                textEntity.SpaceName = $"{spaceName}:Block({blockDef.Name}):Level{nestingLevel}";
                texts.Add(textEntity);
            }

            // 2. 递归处理嵌套的块参照
            if (ent is BlockReference nestedBlockRef)
            {
                // 提取嵌套块的属性
                ExtractBlockReferenceAttributes(nestedBlockRef, tr, texts, spaceName);

                // ✅ 递归到下一层级
                ExtractFromNestedBlock(
                    nestedBlockRef,
                    tr,
                    texts,
                    spaceName,
                    nestingLevel + 1,
                    processedBlocks);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, $"提取嵌套块失败: {blockRef.Name}");
    }
}

/// <summary>
/// ✅ 提取所有块定义内部的文本（用于定义中的常量文本）
/// </summary>
private void ExtractFromAllBlockDefinitions(
    BlockTable bt,
    Transaction tr,
    List<TextEntity> texts)
{
    var processedBlocks = new HashSet<ObjectId>();

    foreach (ObjectId btrId in bt)
    {
        if (processedBlocks.Contains(btrId)) continue;
        processedBlocks.Add(btrId);

        try
        {
            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

            // 跳过布局空间（已在主流程处理）
            if (btr.IsLayout) continue;

            // 跳过匿名块和外部参照
            if (btr.IsAnonymous || btr.IsFromExternalReference) continue;

            // 提取块定义内的文本
            foreach (ObjectId objId in btr)
            {
                var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                var textEntity = ExtractTextFromEntity(ent, objId);
                if (textEntity != null)
                {
                    textEntity.SpaceName = $"BlockDefinition:{btr.Name}";
                    texts.Add(textEntity);
                }
            }

            Log.Debug($"从块定义中提取了文本，共处理 {processedBlocks.Count} 个块定义");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"处理块定义失败: {btrId}");
        }
    }
}
```

---

## 问题6: 算量面板无法弹出

### 根本原因
与问题2相同：首次初始化后立即设置`Visible=false`。

### 解决方案
参考问题2的解决方案，修改`ShowCalculationPalette()`方法（第209-264行）。

---

## 问题7: AI助手光标始终在输入框，无法切换到AutoCAD命令行

### 根本原因
`PaletteSet.KeepFocus = true`会强制保持焦点在面板内部，导致无法切换到AutoCAD命令行。

### 解决方案

**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/UI/PaletteManager.cs`

**修改位置**: `ShowAIPalette`方法（第397行）

**关键修改**:
```csharp
// 修改第397行
// ❌ 删除：_aiPaletteSet.KeepFocus = true;
// ✅ 改为：_aiPaletteSet.KeepFocus = false;  // 允许焦点切换到AutoCAD

// 同时修改ShowTranslationPalette和ShowCalculationPalette
```

**文件**: `BiaogAutoCADPlugin/src/BiaogPlugin/UI/AIPalette.xaml.cs`

**优化焦点管理逻辑**:
```csharp
// 修改第193-206行的InputTextBox_LostFocus方法
private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
{
    try
    {
        // ✅ 修复：仅当焦点移动到AutoCAD主窗口时才记录警告
        // 如果焦点移动到面板内的其他控件（如按钮），这是正常行为
        var focusedElement = FocusManager.GetFocusedElement(this);
        if (focusedElement == null)
        {
            Log.Debug("AI助手输入框失去焦点（可能切换到AutoCAD命令行）");
        }
        else
        {
            Log.Debug($"焦点移动到面板内的其他控件: {focusedElement.GetType().Name}");
        }

        // ❌ 删除：不再强制抢回焦点
        // Dispatcher.BeginInvoke(new Action(() => {
        //     if (!InputTextBox.IsFocused) {
        //         Keyboard.Focus(InputTextBox);
        //     }
        // }), DispatcherPriority.Background);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "处理输入框失去焦点失败");
    }
}
```

---

## 实施步骤

1. **备份当前代码**
   ```bash
   git add -A
   git commit -m "backup: 修复前备份"
   ```

2. **逐个应用修复**（按优先级）
   - 问题1: Ribbon工具栏
   - 问题2: BIAOGE_AI双击
   - 问题6: 算量面板
   - 问题7: 焦点管理
   - 问题5: 块文本提取
   - 问题3+4: 流式输出（最复杂，需要添加新代码）

3. **测试验证**
   - 在AutoCAD 2024中测试Ribbon自动显示
   - 测试所有面板的首次调用
   - 测试块文本提取的完整性
   - 测试流式输出的流畅性

4. **提交修复**
   ```bash
   git add -A
   git commit -m "fix: 修复7个关键问题（详见BUGFIX_SOLUTIONS.md）"
   git push
   ```

---

## 参考资料

1. **AutoCAD .NET API**
   - [Ribbon不加载问题 (AutoCAD 2024)](https://forums.autodesk.com/t5/net/ribbon-not-loading-on-autocad-2024/m-p/12541503)
   - [PaletteSet显示问题](https://forums.autodesk.com/t5/net/custom-palette-display-issue/td-p/8228560)
   - [块文本提取最佳实践](https://help.autodesk.com/view/OARX/2023/ENU/?guid=GUID-BA69D85A-2AED-43C2-B5B7-73022B5F28F8)

2. **阿里云百炼**
   - [流式输出官方文档](https://help.aliyun.com/zh/model-studio/stream)
   - [SSE协议实现](https://help.aliyun.com/zh/model-studio/user-guide/streaming-output)

3. **WPF焦点管理**
   - [AutoCAD PaletteSet焦点问题](https://forums.autodesk.com/t5/net-forum/palette-lose-focus-to-fire-textbox-validated-event/td-p/6729318)

---

**创建日期**: 2025-11-14
**审查人**: Claude (Sonnet 4.5)
**状态**: 待实施
