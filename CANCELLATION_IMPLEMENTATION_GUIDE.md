# CancellationToken 实施指南

## 实施步骤

### 步骤1: 更新方法签名（添加CancellationToken参数）

#### 示例1: TranslationController.TranslateCurrentDrawing()

**当前代码**:
```csharp
public async Task<TranslationStatistics> TranslateCurrentDrawing(
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null)
{
    // ...
}
```

**修改为**:
```csharp
public async Task<TranslationStatistics> TranslateCurrentDrawing(
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    // ...
}
```

---

### 步骤2: 将CancellationToken传递给内部异步调用

#### 示例2: 传递给TranslateBatchWithCacheAsync()

**当前代码** (TranslationController.cs, Line 122-128):
```csharp
var translations = await _translationEngine.TranslateBatchWithCacheAsync(
    allTexts.Select(t => t.Content).ToList(),
    targetLanguage,
    apiProgress,
    System.Threading.CancellationToken.None  // ❌ 硬编码
);
```

**修改为**:
```csharp
var translations = await _translationEngine.TranslateBatchWithCacheAsync(
    allTexts.Select(t => t.Content).ToList(),
    targetLanguage,
    apiProgress,
    cancellationToken  // ✅ 使用方法参数
);
```

---

#### 示例3: 传递给Task.Delay()

**当前代码**:
```csharp
await Task.Delay(1000);  // ❌ 无法被取消
```

**修改为**:
```csharp
await Task.Delay(1000, cancellationToken);  // ✅ 支持取消
```

---

#### 示例4: 传递给SqliteCommand

**当前代码**:
```csharp
using (var reader = await command.ExecuteReaderAsync())
{
    // ...
}
```

**修改为**:
```csharp
using (var reader = await command.ExecuteReaderAsync(cancellationToken))
{
    // ...
}
```

---

### 步骤3: 在循环中检查取消请求

#### 示例5: 处理大量项目的循环

**当前代码**:
```csharp
for (int i = 0; i < items.Count; i++)
{
    var result = await ProcessItemAsync(items[i]);
    // ...
}
```

**修改为**:
```csharp
for (int i = 0; i < items.Count; i++)
{
    cancellationToken.ThrowIfCancellationRequested();  // ✅ 检查取消请求
    
    var result = await ProcessItemAsync(items[i], cancellationToken);
    // ...
}
```

或使用foreach:
```csharp
foreach (var item in items)
{
    cancellationToken.ThrowIfCancellationRequested();
    
    var result = await ProcessItemAsync(item, cancellationToken);
    // ...
}
```

---

### 步骤4: 处理OperationCanceledException

#### 示例6: 在Commands.cs中处理取消

**修改后的命令方法**:
```csharp
[CommandMethod("BIAOGE_TRANSLATE_ZH", CommandFlags.Modal)]
public async void QuickTranslateToChinese()
{
    var _currentCommandCts = new CancellationTokenSource();
    
    try
    {
        ed.WriteMessage("\n开始翻译（输入ESC或运行BIAOGE_CANCEL取消）...");
        
        var result = await _translationController.TranslateCurrentDrawing(
            "zh",
            progress: new Progress<TranslationProgress>(p => 
            {
                ed.WriteMessage($"\r进度: {p.Percentage}% ({p.ProcessedCount}/{p.TotalCount})");
            }),
            cancellationToken: _currentCommandCts.Token
        );
        
        ed.WriteMessage($"\n✅ 翻译完成: {result.SuccessCount} 成功, {result.FailureCount} 失败");
    }
    catch (OperationCanceledException)
    {
        ed.WriteMessage("\n⚠️ 翻译已被用户取消");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "翻译失败");
        ed.WriteMessage($"\n❌ 错误: {ex.Message}");
    }
    finally
    {
        _currentCommandCts?.Dispose();
    }
}
```

---

## 详细修改清单

### HIGH优先级（必须修改）

#### 1. LayerTranslationService.cs - Line 280

**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/LayerTranslationService.cs`

**当前**:
```csharp
public static async Task<TranslationStatistics> TranslateLayerTexts(
    List<string> layerNames,
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null)
{
    // ... Line 276
    var translations = await engine.TranslateBatchWithCacheAsync(
        textEntities.Select(t => t.Content).ToList(),
        targetLanguage,
        apiProgress,
        System.Threading.CancellationToken.None  // ❌ 问题！
    );
```

**修改为**:
```csharp
public static async Task<TranslationStatistics> TranslateLayerTexts(
    List<string> layerNames,
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null,
    CancellationToken cancellationToken = default)  // ✅ 添加参数
{
    // ... Line 276
    var translations = await engine.TranslateBatchWithCacheAsync(
        textEntities.Select(t => t.Content).ToList(),
        targetLanguage,
        apiProgress,
        cancellationToken  // ✅ 改为使用参数
    );
```

---

#### 2. TranslationController.cs - Line 45

**修改方法签名**:
```csharp
public async Task<TranslationStatistics> TranslateCurrentDrawing(
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null,
    CancellationToken cancellationToken = default)  // ✅ 添加
```

**修改内部调用**:
- Line 118: `var allTexts = _extractor.ExtractAllText();` → 考虑改为异步版本
- Line 123-128: 修改CancellationToken.None为cancellationToken
- Line 162-170: 修改AddRecordsAsync调用，添加cancellationToken

---

#### 3. AIAssistantService.cs - Line 74

**修改方法签名**:
```csharp
public async Task<AssistantResponse> ChatStreamAsync(
    string userMessage,
    Action<string>? onContentChunk = null,
    CancellationToken cancellationToken = default)  // ✅ 添加
```

**修改内部调用**:
- Line 121: `agentDecision = await _openAIClient.CompleteStreamingAsync(..., cancellationToken)`
- Line 261: `await ExecuteTool(toolCall, onStreamChunk, cancellationToken)`

---

#### 4. AIComponentRecognizer.cs - Line 55

**修改方法签名**:
```csharp
public async Task<List<ComponentRecognitionResult>> RecognizeAsync(
    List<TextEntity> textEntities,
    List<string>? layerNames = null,
    CalculationPrecision precision = CalculationPrecision.Budget,
    CancellationToken cancellationToken = default)  // ✅ 添加
```

**修改内部调用**:
- Line 84: 传递 `cancellationToken` 给 `_ruleRecognizer.RecognizeFromTextEntitiesAsync()`
- Line 110: 传递 `cancellationToken` 给 `VerifyWithVLModelAsync()`

---

#### 5. DrawingVisionAnalyzer.cs - Line 56

**修改方法签名**:
```csharp
public async Task<List<VisionRecognizedComponent>> AnalyzeDrawingAsync(
    string? exportImagePath = null,
    VisionAnalysisLevel analysisLevel = VisionAnalysisLevel.Standard,
    CancellationToken cancellationToken = default)  // ✅ 添加
```

**修改内部调用**:
- Line 67: `string imagePath = exportImagePath ?? await ExportCurrentViewToImage(cancellationToken);`
- Line 84: 传递 `cancellationToken` 给 `CallVisionModelAsync()`
- Line 89: 传递 `cancellationToken` 给 `CrossValidateWithGeometry()`

---

### MEDIUM优先级（建议修改）

#### 6. CacheService.cs - 所有异步方法

**修改 GetTranslationAsync (Line 92)**:
```csharp
public async Task<string?> GetTranslationAsync(
    string sourceText, 
    string targetLanguage, 
    int expirationDays = 30,
    CancellationToken cancellationToken = default)  // ✅ 添加
{
    await EnsureInitializedAsync();
    
    using (var connection = new SqliteConnection(_connectionString))
    {
        await connection.OpenAsync(cancellationToken);  // ✅ 传递
        
        using (var command = connection.CreateCommand())
        {
            // ...
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))  // ✅ 传递
            {
                // ...
            }
        }
    }
}
```

**修改 SetTranslationAsync (Line 137)**:
```csharp
public async Task SetTranslationAsync(
    string sourceText, 
    string targetLanguage, 
    string translatedText,
    CancellationToken cancellationToken = default)  // ✅ 添加
{
    await EnsureInitializedAsync();
    
    using (var connection = new SqliteConnection(_connectionString))
    {
        await connection.OpenAsync(cancellationToken);  // ✅ 传递
        
        using (var command = connection.CreateCommand())
        {
            // ...
            await command.ExecuteNonQueryAsync(cancellationToken);  // ✅ 传递
        }
    }
}
```

---

#### 7. TranslationHistory.cs - 所有异步方法

**修改 AddRecordsAsync (Line 164)**:
```csharp
public async Task AddRecordsAsync(
    List<HistoryRecord> records,
    CancellationToken cancellationToken = default)  // ✅ 添加
{
    try
    {
        await EnsureInitializedAsync();
        
        using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);  // ✅ 传递
            
            // ... 循环中添加检查
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();  // ✅ 检查取消
                
                // ...
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);  // ✅ 传递
            }
        }
    }
    // ...
}
```

---

#### 8. TranslationEngine.cs - 更新缓存调用

**当前代码 (Line 88-101)**:
```csharp
for (int i = 0; i < texts.Count; i++)
{
    var cached = await _cacheService.GetTranslationAsync(texts[i], targetLanguage);
    if (cached != null)
    {
        results.Add(cached);
    }
    else
    {
        results.Add(""); // 占位
        uncachedTexts.Add(texts[i]);
        uncachedIndices.Add(i);
    }
}
```

**修改为**:
```csharp
for (int i = 0; i < texts.Count; i++)
{
    cancellationToken.ThrowIfCancellationRequested();  // ✅ 添加检查
    
    var cached = await _cacheService.GetTranslationAsync(
        texts[i], 
        targetLanguage,
        cancellationToken: cancellationToken);  // ✅ 传递（需要CacheService支持）
    if (cached != null)
    {
        results.Add(cached);
    }
    else
    {
        results.Add(""); // 占位
        uncachedTexts.Add(texts[i]);
        uncachedIndices.Add(i);
    }
}
```

---

## 测试清单

在修改每个方法后，请进行以下测试：

- [ ] 正常完成测试：不使用CancellationToken，验证功能正常
- [ ] 早期取消测试：在操作开始后立即取消
- [ ] 中途取消测试：在操作进行到50%时取消
- [ ] 异常处理测试：验证OperationCanceledException被正确捕获
- [ ] 资源清理测试：验证取消后没有泄露的资源（数据库连接、文件句柄等）
- [ ] UI响应性测试：验证AutoCAD UI在长操作期间保持响应

---

## 常见错误模式

### ❌ 错误1: 创建方法参数但不使用

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    // ❌ 没有传递给任何地方
    var result = await SomeApiCall();
}
```

**✅ 正确**:
```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    // ✅ 传递给异步调用
    var result = await SomeApiCall(cancellationToken);
}
```

---

### ❌ 错误2: 在循环中忘记检查

```csharp
foreach (var item in items)
{
    // ❌ 如果item处理很耗时，无法及时响应取消请求
    await ProcessItemAsync(item, cancellationToken);
}
```

**✅ 正确**:
```csharp
foreach (var item in items)
{
    cancellationToken.ThrowIfCancellationRequested();  // ✅ 在每次迭代前检查
    await ProcessItemAsync(item, cancellationToken);
}
```

---

### ❌ 错误3: 硬编码CancellationToken.None

```csharp
// ❌ 这实际上禁用了取消功能
var result = await _service.DoWorkAsync(CancellationToken.None);
```

**✅ 正确**:
```csharp
// ✅ 使用调用方提供的CancellationToken
public async Task MyMethodAsync(CancellationToken cancellationToken = default)
{
    var result = await _service.DoWorkAsync(cancellationToken);
}
```

---

### ❌ 错误4: 在try/catch中吞掉OperationCanceledException

```csharp
try
{
    await DoWorkAsync(cancellationToken);
}
catch (Exception ex)  // ❌ 这会捕获OperationCanceledException
{
    // 不再考虑取消...
}
```

**✅ 正确**:
```csharp
try
{
    await DoWorkAsync(cancellationToken);
}
catch (OperationCanceledException)
{
    // ✅ 单独处理取消
    Log.Information("操作被用户取消");
    throw;  // 重新抛出
}
catch (Exception ex)
{
    // 处理其他异常
}
```

---

## 参考资源

- [Microsoft Docs: CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)
- [Microsoft Docs: CancellationTokenSource](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource)
- [Best Practices in C# Async/Await](https://stackoverflow.com/questions/13574158/how-to-cancel-an-async-operation)
- [AutoCAD .NET API Best Practices](https://help.autodesk.com/view/OARX/2025/ENU/)

