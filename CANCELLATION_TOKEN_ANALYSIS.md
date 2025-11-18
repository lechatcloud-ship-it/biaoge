# CancellationToken支持分析报告 - 标哥AutoCAD插件

**生成时间**: 2025-11-18  
**分析范围**: Services目录中所有async方法  
**扫描文件数**: 42个  
**发现的async方法总数**: 76+

---

## 优先级分类标准

| 级别 | 条件 | 示例 |
|------|------|------|
| **High** | 用户直接触发，执行时间 > 5秒 | 翻译全图、批量识别、AI对话 |
| **Medium** | 后台任务、可能阻塞UI、执行时间 1-5秒 | 缓存查询、历史记录操作 |
| **Low** | 快速完成 < 1秒、不涉及网络/I/O | 工具方法、转换方法 |

---

## HIGH优先级（需要立即添加CancellationToken支持）

### 1. TranslationController.TranslateCurrentDrawing()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/TranslationController.cs`  
**行号**: Line 45  
**签名**: `public async Task<TranslationStatistics> TranslateCurrentDrawing(string targetLanguage, IProgress<TranslationProgress>? progress = null)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 调用 `_extractor.ExtractAllText()` - DWG遍历（长时间）
- 调用 `_translationEngine.TranslateBatchWithCacheAsync()` - 网络API调用（长时间）
- 调用 `_updater.UpdateTexts()` - DWG更新（长时间）
- 调用 `history.AddRecordsAsync()` - 数据库写入（长时间）

**优先级**: ⭐⭐⭐ **HIGH**  
**理由**: 用户最常用的命令，翻译整个DWG可能需要5-60秒  
**建议修改**:
```csharp
public async Task<TranslationStatistics> TranslateCurrentDrawing(
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    // 传递给内部调用：
    // _translationEngine.TranslateBatchWithCacheAsync(..., cancellationToken: cancellationToken)
    // history.AddRecordsAsync(..., cancellationToken: cancellationToken) - 如果支持的话
    // Task.Delay(delayMs, cancellationToken) - 在延迟操作中使用
}
```

---

### 2. BailianApiClient.TranslateBatchAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/BailianApiClient.cs`  
**行号**: Line 670  
**签名**: `public async Task<List<string>> TranslateBatchAsync(List<string> texts, string targetLanguage, string? model = null, string? sourceLanguage = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)`  

**CancellationToken状态**: ✅ **有** (已有参数)  
**耗时操作**:
- HttpClient.SendAsync() 多次调用（第721行的 `await _httpClient.SendAsync(clonedRequest, cancellationToken)`）
- Task.Run() with Task.WaitAsync() - 并发控制（第751行 `using var semaphore = new SemaphoreSlim(10)`）
- 循环处理长文本列表（700+条）

**优先级**: ✅ **已支持**  
**使用分析**:
- ✅ 正确传递给 `_httpClient.SendAsync(clonedRequest, cancellationToken)`
- ✅ 正确传递给 `semaphore.WaitAsync(cancellationToken)` 
- ✅ 正确传递给 `TranslateAsync()` 递归调用（第731行）
- ✅ 正确传递给 `Task.Delay()` 重试延迟（第561、572、583行）

**评分**: ⭐⭐⭐ **优秀** - 完整支持CancellationToken

---

### 3. BailianApiClient.TranslateAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/BailianApiClient.cs`  
**行号**: Line 940  
**签名**: `public async Task<string> TranslateAsync(string text, string targetLanguage, string? model = null, string? sourceLanguage = null, CancellationToken cancellationToken = default)`  

**CancellationToken状态**: ✅ **有** (已有参数)  
**耗时操作**:
- 调用 `TranslateWithSegmentationAsync()` (Line 983)
- 调用 `_httpClient.SendAsync()` (Line 1060+)

**优先级**: ✅ **已支持**  
**使用分析**:
- ✅ 正确传递给 `TranslateWithSegmentationAsync(text, targetLanguage, model, sourceLanguage, cancellationToken)` (Line 983)
- ❌ **未直接传递给HttpClient调用** - 需要检查SendAsync调用

**评分**: ⭐⭐ **部分支持** - 需要验证HttpClient调用是否使用了CancellationToken

---

### 4. TranslationEngine.TranslateBatchWithCacheAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/TranslationEngine.cs`  
**行号**: Line 69  
**签名**: `public async Task<List<string>> TranslateBatchWithCacheAsync(List<string> texts, string targetLanguage, IProgress<double>? progress = null, CancellationToken cancellationToken = default)`  

**CancellationToken状态**: ✅ **有** (已有参数)  
**耗时操作**:
- 循环调用 `_cacheService.GetTranslationAsync()` (Line 90) - 多次异步I/O
- 调用 `_apiClient.TranslateBatchAsync()` (Line 113)
- 循环调用 `_cacheService.SetTranslationAsync()` (Line 127)

**优先级**: ✅ **已支持**  
**使用分析**:
- ✅ 正确传递给 `_apiClient.TranslateBatchAsync()` (Line 113-118)
- ❌ **未传递给缓存查询操作** - GetTranslationAsync() 和 SetTranslationAsync() 没有CancellationToken参数
  ```csharp
  var cached = await _cacheService.GetTranslationAsync(texts[i], targetLanguage);
  // 应该改为支持 cancellationToken
  ```

**评分**: ⭐⭐ **部分支持** - API层支持，但缓存层不支持

---

### 5. LayerTranslationService.TranslateLayerTexts()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/LayerTranslationService.cs`  
**行号**: Line 235  
**签名**: `public static async Task<TranslationStatistics> TranslateLayerTexts(List<string> layerNames, string targetLanguage, IProgress<TranslationProgress>? progress = null)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 调用 `ExtractTextFromLayers()` - DWG遍历
- 调用 `engine.TranslateBatchWithCacheAsync()` (Line 276) - **网络操作**（重要！）
- 调用 `history.AddRecordsAsync()` (Line 330) - 数据库操作
- 调用 `updater.UpdateTexts()` (Line 300) - DWG更新

**优先级**: ⭐⭐⭐ **HIGH**  
**理由**: 用户通过UI选择图层后执行，可能需要10-30秒  
**问题分析**:
- Line 280: **已经传递 `System.Threading.CancellationToken.None`** - 这是一个硬编码的"无取消"令牌！
  ```csharp
  var translations = await engine.TranslateBatchWithCacheAsync(
      textEntities.Select(t => t.Content).ToList(),
      targetLanguage,
      apiProgress,
      System.Threading.CancellationToken.None  // ❌ 硬编码为不可取消！
  );
  ```

**建议修改**:
```csharp
public static async Task<TranslationStatistics> TranslateLayerTexts(
    List<string> layerNames,
    string targetLanguage,
    IProgress<TranslationProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    // 修改第280行：
    var translations = await engine.TranslateBatchWithCacheAsync(
        textEntities.Select(t => t.Content).ToList(),
        targetLanguage,
        apiProgress,
        cancellationToken  // ✅ 使用方法参数而不是CancellationToken.None
    );
}
```

---

### 6. AIAssistantService.ChatStreamAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/AIAssistantService.cs`  
**行号**: Line 74  
**签名**: `public async Task<AssistantResponse> ChatStreamAsync(string userMessage, Action<string>? onContentChunk = null)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 调用 `_openAIClient.CompleteStreamingAsync()` (Line 121) - **AI API调用**（可能 5-30秒）
- 调用 `ExecuteTool()` 多次 (Line 261+) - 工具执行，可能触发翻译、修改DWG等

**优先级**: ⭐⭐⭐ **HIGH**  
**理由**: AI对话可能是最耗时的操作，用户需要在对话过程中中止  
**建议修改**:
```csharp
public async Task<AssistantResponse> ChatStreamAsync(
    string userMessage,
    Action<string>? onContentChunk = null,
    CancellationToken cancellationToken = default)
{
    // 传递给：
    // _openAIClient.CompleteStreamingAsync(..., cancellationToken)
    // ExecuteTool(..., onStreamChunk, cancellationToken) - 如果ExecuteTool支持的话
}
```

---

### 7. AIComponentRecognizer.RecognizeAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/AIComponentRecognizer.cs`  
**行号**: Line 55  
**签名**: `public async Task<List<ComponentRecognitionResult>> RecognizeAsync(List<TextEntity> textEntities, List<string>? layerNames = null, CalculationPrecision precision = CalculationPrecision.Budget)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 调用 `_ruleRecognizer.RecognizeFromTextEntitiesAsync()` (Line 84) - 循环处理可能有 50-1000个文本实体
- 调用 `VerifyWithVLModelAsync()` (Line 109+) - **AI视觉模型调用**（付费操作）
- 调用 `CrossValidateWithGeometry()` (Line 125+) - DWG几何验证

**优先级**: ⭐⭐⭐ **HIGH**  
**理由**: AI模型调用可能耗时 10-60秒，成本也较高（付费API）  
**建议修改**:
```csharp
public async Task<List<ComponentRecognitionResult>> RecognizeAsync(
    List<TextEntity> textEntities,
    List<string>? layerNames = null,
    CalculationPrecision precision = CalculationPrecision.Budget,
    CancellationToken cancellationToken = default)
{
    // 传递给内部调用
}
```

---

### 8. DrawingVisionAnalyzer.AnalyzeDrawingAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/DrawingVisionAnalyzer.cs`  
**行号**: Line 56  
**签名**: `public async Task<List<VisionRecognizedComponent>> AnalyzeDrawingAsync(string? exportImagePath = null, VisionAnalysisLevel analysisLevel = VisionAnalysisLevel.Standard)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 调用 `ExportCurrentViewToImage()` (Line 67) - AutoCAD导出，可能 5-10秒
- 调用 `CallVisionModelAsync()` (Line 84) - **AI视觉分析**（主要耗时 10-30秒）
- 调用 `CrossValidateWithGeometry()` (Line 89) - 几何验证

**优先级**: ⭐⭐⭐ **HIGH**  
**理由**: 完整的视觉分析流程可能需要 15-60秒  
**建议修改**:
```csharp
public async Task<List<VisionRecognizedComponent>> AnalyzeDrawingAsync(
    string? exportImagePath = null,
    VisionAnalysisLevel analysisLevel = VisionAnalysisLevel.Standard,
    CancellationToken cancellationToken = default)
```

---

## MEDIUM优先级（后台任务和可能阻塞UI的操作）

### 9. ComponentRecognizer.RecognizeFromTextEntitiesAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/ComponentRecognizer.cs`  
**行号**: Line 406  
**签名**: `public async Task<List<ComponentRecognitionResult>> RecognizeFromTextEntitiesAsync(List<TextEntity> textEntities, bool useAiVerification = false)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 循环处理文本实体（50-500个），每个实体：
  - `RecognizeByRegex()` - 正则匹配（快）
  - `VerifyWithAiAsync()` (Line 490) - **AI验证**（可选，耗时3-5秒/个）

**优先级**: ⭐⭐ **MEDIUM**  
**理由**: 基础识别快速（< 2秒），但AI验证会很慢（可选）  
**建议**: 至少在调用 `VerifyWithAiAsync()` 时支持取消

---

### 10. CacheService.GetTranslationAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/CacheService.cs`  
**行号**: Line 92  
**签名**: `public async Task<string?> GetTranslationAsync(string sourceText, string targetLanguage, int expirationDays = 30)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- SQLite 数据库查询 (Line 110-112)
  ```csharp
  using (var reader = await command.ExecuteReaderAsync())
  ```

**优先级**: ⭐⭐ **MEDIUM**  
**理由**: 缓存查询通常快速（< 100ms），但在批量查询时累积可达几秒  
**建议修改**:
```csharp
public async Task<string?> GetTranslationAsync(
    string sourceText, 
    string targetLanguage, 
    int expirationDays = 30,
    CancellationToken cancellationToken = default)
{
    // 传递给：
    // await command.ExecuteReaderAsync(cancellationToken)
    // await connection.OpenAsync(cancellationToken)
}
```

---

### 11. CacheService.SetTranslationAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/CacheService.cs`  
**行号**: Line 137  
**签名**: `public async Task SetTranslationAsync(string sourceText, string targetLanguage, string translatedText)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- SQLite 数据库写入 (INSERT操作)

**优先级**: ⭐⭐ **MEDIUM**  
**建议修改**:
```csharp
public async Task SetTranslationAsync(
    string sourceText, 
    string targetLanguage, 
    string translatedText,
    CancellationToken cancellationToken = default)
```

---

### 12. CacheService.CleanExpiredCacheAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/CacheService.cs`  
**行号**: Line 185  
**签名**: `public async Task<int> CleanExpiredCacheAsync(int expirationDays = 30)`  

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- 数据库删除操作（可能涉及百万级记录）

**优先级**: ⭐⭐ **MEDIUM**  
**理由**: 后台清理任务，建议支持取消以避免长时间阻塞

---

### 13. TranslationHistory.AddRecordAsync() 和 AddRecordsAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/TranslationHistory.cs`  
**行号**: Line 115、164  
**签名**: 
- `public async Task AddRecordAsync(HistoryRecord record)`
- `public async Task AddRecordsAsync(List<HistoryRecord> records)`

**CancellationToken状态**: ❌ **无**  
**耗时操作**:
- SQLite 批量INSERT操作

**优先级**: ⭐⭐ **MEDIUM**  
**理由**: 批量添加 50-500条记录可能耗时 1-3秒  
**建议修改**:
```csharp
public async Task AddRecordsAsync(List<HistoryRecord> records, CancellationToken cancellationToken = default)
{
    // 在数据库操作中使用 cancellationToken
}
```

---

### 14. TranslationHistory.GetRecentRecordsAsync() 等查询方法
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/TranslationHistory.cs`  
**行号**: Line 235、283、361、422  
**签名**: 
- `public async Task<List<HistoryRecord>> GetRecentRecordsAsync(int limit = 100)`
- `public async Task<List<HistoryRecord>> GetRecordsByObjectIdAsync(string objectIdHandle)`
- `public async Task<Dictionary<string, object>> GetStatisticsAsync()`
- `public async Task ClearAllAsync()`

**CancellationToken状态**: ❌ **无**  
**优先级**: ⭐⭐ **MEDIUM**

---

### 15. DiagnosticTool 的所有异步检查方法
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/DiagnosticTool.cs`  
**行号**: Line 34、66、129、175、214、276、326  
**方法**:
- `RunFullDiagnosticAsync()` (Line 34)
- `CheckConfigurationAsync()` (Line 66)
- `CheckApiConnectionAsync()` (Line 129)
- `CheckCacheHealthAsync()` (Line 175)
- `CheckFileSystemPermissionsAsync()` (Line 214)
- `CheckDiskSpaceAsync()` (Line 276)
- `CheckNetworkConnectivityAsync()` (Line 326)

**CancellationToken状态**: ❌ **无**  
**优先级**: ⭐⭐ **MEDIUM**  
**理由**: 诊断操作通常 2-10秒，支持取消可改进用户体验

---

## LOW优先级（快速完成的操作，< 1秒）

### 16. BailianApiClient.SendWithRetryAsync() 和 CloneHttpRequestAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/BailianApiClient.cs`  
**行号**: Line 521、634  

**CancellationToken状态**: ✅ **已有参数**  
**评分**: ✅ **优秀** - 已正确支持

---

### 17. BailianOpenAIClient.CompleteAsync() 和 CompleteStreamingAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/BailianOpenAIClient.cs`  
**行号**: Line 97、165  

**CancellationToken状态**: ✅ **已有参数**  
**评分**: ✅ **优秀** - 已正确支持

---

### 18. BailianOpenAIClient.CallVisionAsync()
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/BailianOpenAIClient.cs`  
**行号**: Line 359  

**CancellationToken状态**: ❓ **需要检查**  
**优先级**: ⭐⭐⭐ **HIGH** (如果是AI API调用)

---

### 19. AutoCADToolExecutor 的工具方法
**文件**: `/home/user/biaoge/BiaogAutoCADPlugin/src/BiaogPlugin/Services/AutoCADToolExecutor.cs`  
**行号**: Line 34、90、149、207、266 等  

**示例**:
- `DrawLine()` (Line 34)
- `DrawCircle()` (Line 90)
- `DrawRectangle()` (Line 149)
- 等共31个工具方法

**CancellationToken状态**: ❌ **无**  
**耗时操作**: 大多数是快速的AutoCAD命令  
**优先级**: ⭐ **LOW**  
**理由**: 这些都是快速的工具方法，通常 < 1秒完成

---

## 总结表格

| # | 文件 | 方法 | 行号 | 当前支持 | 优先级 | 建议 |
|---|------|------|------|---------|--------|------|
| 1 | TranslationController.cs | TranslateCurrentDrawing | 45 | ❌ | HIGH | 立即添加 |
| 2 | BailianApiClient.cs | TranslateBatchAsync | 670 | ✅ | - | 已支持 |
| 3 | BailianApiClient.cs | TranslateAsync | 940 | ✅ | - | 已支持 |
| 4 | TranslationEngine.cs | TranslateBatchWithCacheAsync | 69 | ⚠️ | HIGH | 添加到缓存操作 |
| 5 | LayerTranslationService.cs | TranslateLayerTexts | 235 | ❌ | HIGH | **立即修复** |
| 6 | AIAssistantService.cs | ChatStreamAsync | 74 | ❌ | HIGH | 立即添加 |
| 7 | AIComponentRecognizer.cs | RecognizeAsync | 55 | ❌ | HIGH | 立即添加 |
| 8 | DrawingVisionAnalyzer.cs | AnalyzeDrawingAsync | 56 | ❌ | HIGH | 立即添加 |
| 9 | ComponentRecognizer.cs | RecognizeFromTextEntitiesAsync | 406 | ❌ | MEDIUM | 添加（特别是AI验证) |
| 10 | CacheService.cs | GetTranslationAsync | 92 | ❌ | MEDIUM | 添加 |
| 11 | CacheService.cs | SetTranslationAsync | 137 | ❌ | MEDIUM | 添加 |
| 12 | CacheService.cs | CleanExpiredCacheAsync | 185 | ❌ | MEDIUM | 添加 |
| 13 | TranslationHistory.cs | AddRecordAsync | 115 | ❌ | MEDIUM | 添加 |
| 14 | TranslationHistory.cs | AddRecordsAsync | 164 | ❌ | MEDIUM | 添加 |
| 15 | TranslationHistory.cs | GetRecentRecordsAsync等 | 235+ | ❌ | MEDIUM | 添加 |
| 16 | DiagnosticTool.cs | RunFullDiagnosticAsync等 | 34+ | ❌ | MEDIUM | 添加 |
| 17 | BailianOpenAIClient.cs | CompleteAsync | 97 | ✅ | - | 已支持 |
| 18 | BailianOpenAIClient.cs | CompleteStreamingAsync | 165 | ✅ | - | 已支持 |
| 19 | BailianOpenAIClient.cs | CallVisionAsync | 359 | ❓ | HIGH | 需确认 |
| 20 | AutoCADToolExecutor.cs | DrawXXX等31个 | 34+ | ❌ | LOW | 可选 |

---

## 关键发现

### 🔴 最严重的问题

**LayerTranslationService.TranslateLayerTexts() Line 280**
```csharp
var translations = await engine.TranslateBatchWithCacheAsync(
    textEntities.Select(t => t.Content).ToList(),
    targetLanguage,
    apiProgress,
    System.Threading.CancellationToken.None  // ❌ 硬编码为"不可取消"
);
```

**影响**: 即使TranslateBatchWithCacheAsync()支持CancellationToken，这里也永远无法取消。这个方法可能执行 10-30秒。

---

## 实施计划

### Phase 1: 紧急修复 (1-2天)
1. **LayerTranslationService.TranslateLayerTexts()** - 移除CancellationToken.None，添加方法参数
2. **TranslationController.TranslateCurrentDrawing()** - 添加CancellationToken，传递给内部调用
3. **AIAssistantService.ChatStreamAsync()** - 添加CancellationToken

### Phase 2: 主要功能 (2-3天)
4. **AIComponentRecognizer.RecognizeAsync()** - 添加CancellationToken
5. **DrawingVisionAnalyzer.AnalyzeDrawingAsync()** - 添加CancellationToken
6. **ComponentRecognizer.RecognizeFromTextEntitiesAsync()** - 添加CancellationToken

### Phase 3: 数据层支持 (1-2天)
7. **CacheService** 所有异步方法 - 添加CancellationToken支持
8. **TranslationHistory** 所有异步方法 - 添加CancellationToken支持

### Phase 4: 诊断和工具 (可选)
9. **DiagnosticTool** 异步方法 - 添加CancellationToken
10. **AutoCADToolExecutor** 工具方法 - 可选添加（优先级低)

---

## 传播链路分析

```
用户命令 (BIAOGE_TRANSLATE_ZH等)
    ↓
Commands.cs: async void 命令方法
    ↓
TranslationController.TranslateCurrentDrawing() ⭐ [HIGH]
    ├─→ TranslationEngine.TranslateBatchWithCacheAsync() ✅ [有CancellationToken]
    │   ├─→ CacheService.GetTranslationAsync() ❌ [无]
    │   └─→ BailianApiClient.TranslateBatchAsync() ✅ [有]
    │       ├─→ BailianApiClient.SendWithRetryAsync() ✅
    │       └─→ Task.Delay(retryDelay, cancellationToken) ✅
    └─→ TranslationHistory.AddRecordsAsync() ❌ [无]

或

LayerTranslationService.TranslateLayerTexts() ⭐ [HIGH]
    └─→ TranslationEngine.TranslateBatchWithCacheAsync(CancellationToken.None) ❌ [硬编码!]
```

---

## 命令界面集成建议

在Commands.cs中，为每个长时间运行的命令添加取消支持：

```csharp
private static CancellationTokenSource? _currentCommandCts;

[CommandMethod("BIAOGE_TRANSLATE_ZH", CommandFlags.Modal)]
public async void QuickTranslateToChinese()
{
    // 创建新的CancellationTokenSource
    _currentCommandCts = new CancellationTokenSource();
    
    try
    {
        var result = await _translationController.TranslateCurrentDrawing(
            "zh",
            progress: new Progress<TranslationProgress>(p => 
            {
                ed.WriteMessage($"\n进度: {p.Percentage}% - {p.ProcessedCount}/{p.TotalCount}");
            }),
            cancellationToken: _currentCommandCts.Token
        );
    }
    catch (OperationCanceledException)
    {
        ed.WriteMessage("\n翻译已取消");
    }
    finally
    {
        _currentCommandCts?.Dispose();
        _currentCommandCts = null;
    }
}

// 添加一个取消命令
[CommandMethod("BIAOGE_CANCEL", CommandFlags.Modal)]
public void CancelCurrentOperation()
{
    if (_currentCommandCts != null && !_currentCommandCts.IsCancellationRequested)
    {
        _currentCommandCts.Cancel();
        ed.WriteMessage("\n已请求取消当前操作...");
    }
    else
    {
        ed.WriteMessage("\n没有正在进行的可取消操作");
    }
}
```

