# 严重Bug报告 - 深度审查发现

**审查日期**: 2025-11-18
**审查范围**: 翻译、算量、AI助手全功能
**严重程度**: 🚨 P0 CRITICAL

---

## 🚨 P0严重错误 - 翻译API端点和格式完全错误

### 问题1: 使用了错误的API端点

**官方文档** (FLASH_MODELS_SPEC.md 第256-268行):
```json
POST https://dashscope.aliyuncs.com/api/v1/services/translation/translate
Authorization: Bearer sk-your-api-key

{
  "model": "qwen-mt-flash",
  "input": {
    "source_language": "zh",
    "target_language": "en",
    "source_text": "建筑外墙"
  }
}
```

**实际代码** (BailianApiClient.cs:1067):
```csharp
// ❌ 错误：使用了对话API端点，而不是翻译专用端点！
var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)

// ChatCompletionEndpoint = "/compatible-mode/v1/chat/completions"  ❌ 错误！
// 应该使用: "/api/v1/services/translation/translate"
```

### 问题2: 请求体格式完全错误

**代码中的格式** (BailianApiClient.cs:1012-1037):
```csharp
requestBody = new
{
    model = model,  // ✓ 正确
    messages = new[]  // ❌ 错误！应该是 "input" 对象，不是 "messages" 数组
    {
        new
        {
            role = "user",  // ❌ 错误！翻译API不使用role/content格式
            content = text
        }
    },
    translation_options = new  // ❌ 位置错误！应该在 input 对象内部
    {
        source_lang = sourceLang,
        target_lang = targetLang,
        domains = EngineeringTranslationConfig.DomainPrompt,
        terms = EngineeringTranslationConfig.GetApiTerms(sourceLang, targetLang),
        tm_list = EngineeringTranslationConfig.GetApiTranslationMemory(sourceLang, targetLang)
    },
    temperature = 0.3  // ❌ 翻译API不支持temperature参数
};
```

**正确格式** (根据官方文档):
```csharp
requestBody = new
{
    model = "qwen-mt-flash",
    input = new  // ✓ 应该使用 input 对象
    {
        source_language = "zh",  // ✓ 直接在 input 内部
        target_language = "en",
        source_text = "建筑外墙",  // ✓ 不是 messages 数组！

        // 可选参数（如果API支持）
        domains = "...",  // 领域提示
        terms = new[] { ... },  // 术语表
        tm_list = new[] { ... }  // 翻译记忆
    }
};
```

### 影响范围

- ✅ **所有翻译功能可能无法正常工作**
- BailianApiClient.TranslateAsync() (第951行)
- BailianApiClient.TranslateBatchAsync() (第681行)
- TranslationEngine.TranslateWithCacheAsync()
- 所有使用翻译的命令:
  - BIAOGE_TRANSLATE_ZH
  - BIAOGE_TRANSLATE_EN
  - BIAOGE_TRANSLATE_SELECTED
  - 图层翻译

### 根本原因

代码注释说"统一使用 OpenAI 兼容模式"，但这对于**翻译API**是错误的！

- **对话API**: 使用 `/compatible-mode/v1/chat/completions` + `messages` 数组 ✓
- **翻译API**: 使用 `/api/v1/services/translation/translate` + `input` 对象 ✓

这是两个**完全不同**的API端点和格式！

---

## 🚨 P0严重错误 - Token限制与官方文档严重不符

### 问题: qwen-mt-flash上下文长度配置错误

**官方文档** (FLASH_MODELS_SPEC.md 第28-30行):
```
qwen-mt-flash:
- 上下文长度: 32,768 tokens（输入+输出）
- 最大输入: 30,000 tokens
- 最大输出: 2,768 tokens
```

**实际代码** (EngineeringTranslationConfig.cs:15-16):
```csharp
// ❌ 错误：Token限制与官方文档不符！
public const int MaxInputTokens = 8192;   // 应该是 30,000
public const int MaxOutputTokens = 8192;  // 应该是 2,768

// ❌ 错误：基于错误的限制计算批次大小
public const int MaxCharsPerBatch = 7400;  // 应该是 ~27,000
```

### 影响

- **严重限制了翻译能力**: 用户无法翻译超过7400字符的文本
- **浪费了API能力**: qwen-mt-flash可以处理30,000 tokens，但我们只用了8K
- **可能导致不必要的分段**: 本可以一次翻译的内容被分成多次

### 代码注释中的矛盾

EngineeringTranslationConfig.cs 第21-27行说：
```csharp
/// ✅ P0修复：修正为qwen-mt-flash实际限制（8K上下文，NOT 1M）
/// qwen-mt-flash性能参数（官方文档）：
/// - 最大输入长度: 8192 tokens
/// - 最大输出长度: 8192 tokens
/// - 总上下文: 16384 tokens
```

但官方文档明确说的是 **32,768 tokens**，而不是 16,384！

---

## ⚠️ P1中等问题 - DomainPrompt格式不确定

### 问题: domains参数格式不明确

**代码** (EngineeringTranslationConfig.cs:72-81):
```csharp
public static readonly string DomainPrompt =
    "This text is from construction and civil engineering drawings, including structural " +
    "design specifications, architectural plans, MEP (mechanical, electrical, plumbing) " +
    "systems, and building material specifications. The content involves professional " +
    "engineering terminology following international standards (GB, ACI, AISC, ASHRAE, IBC). " +
    "Pay attention to technical identifiers such as drawing numbers, material strength " +
    "grades (e.g., C30 concrete, Q235 steel, HRB400 reinforcement), measurement units, " +
    "axis references, and standard codes. Translate in a professional technical documentation " +
    "style suitable for engineers and construction professionals, preserving all technical " +
    "identifiers and formatting.";
```

**不确定性**:
- 官方文档没有明确说明 `domains` 参数应该是长段落还是短关键词
- 代码注释说之前的指令式命令会导致"提示词泄漏"
- 当前使用的是长描述性段落（约85 tokens），占用较多上下文

### 建议

需要验证 `domains` 参数的正确格式：
1. 是否应该是简短的领域关键词（如 "construction, engineering"）？
2. 还是确实应该是长段落描述？
3. 是否有官方示例可以参考？

---

## ✅ Vision API 实现正确

**检查结果**: Vision API (CallVisionModelAsync) 使用了正确的格式：

```csharp
// ✓ 正确：使用 OpenAI 兼容端点
POST /compatible-mode/v1/chat/completions

// ✓ 正确：使用 messages 数组 + image_url格式
{
    "model": "qwen3-vl-flash",
    "messages": [
        {
            "role": "user",
            "content": [
                { "type": "text", "text": "..." },
                { "type": "image_url", "image_url": { "url": "data:image/png;base64,..." } }
            ]
        }
    ],
    "max_tokens": 8000,
    "temperature": 0.1
}
```

这符合OpenAI Vision API的格式规范。✅

---

## 🔧 修复优先级

### P0 - 立即修复（严重影响功能）

1. **修复翻译API端点和格式**
   - 创建专用的翻译API端点常量
   - 重写TranslateAsync()使用正确的input格式
   - 重写TranslateBatchAsync()使用正确的格式

2. **修复Token限制配置**
   - 将MaxInputTokens改为30000
   - 将MaxOutputTokens改为2768
   - 重新计算MaxCharsPerBatch（约27000字符）

### P1 - 短期修复（优化改进）

3. **验证DomainPrompt格式**
   - 查阅阿里云百炼官方文档
   - 如果需要，简化为关键词格式

### P2 - 长期优化

4. **添加端到端测试**
   - 测试翻译API调用
   - 测试Vision API调用
   - 测试AI助手功能

---

## 📋 修复检查清单

- [ ] 创建 `/api/v1/services/translation/translate` 端点常量
- [ ] 重写 TranslateAsync() 使用 input 格式
- [ ] 重写 TranslateBatchAsync() 使用 input 格式
- [ ] 更新 MaxInputTokens = 30000
- [ ] 更新 MaxOutputTokens = 2768
- [ ] 重新计算 MaxCharsPerBatch
- [ ] 测试翻译功能
- [ ] 测试批量翻译
- [ ] 更新文档
- [ ] 提交修复

---

## 🚨 紧急程度

**CRITICAL** - 这些错误可能导致：
- 翻译功能完全无法工作（API格式错误）
- 翻译能力被严重限制（Token限制错误）
- 用户体验极差（不必要的分段、超时）
- API调用失败率高（格式不匹配）

**建议立即修复！**
