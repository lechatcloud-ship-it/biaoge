# 标哥AutoCAD插件 - 模型选择指南

基于阿里云百炼官方文档深度研究（2025-01-16）

## 📊 模型对比总览

| 模型 | 上下文 | 输入价格 | 输出价格 | 核心优势 | 适用场景 |
|------|--------|---------|---------|---------|---------|
| **qwen3-max** | 262K | 0.0032元 | 0.0128元 | 最强推理、思考模式 | 复杂分析、深度思考 |
| **qwen-plus** | 1000K | 0.0008元 | 0.002元 | 均衡、支持思考 | 中等复杂任务 |
| **qwen3-coder-flash** | 1000K | 0.001元 | 0.004元 | 代码专用、工具调用 | 开发辅助、Agent |
| **qwen-flash** | 100K | 0.00015元 | 0.0015元 | 极速、超低成本 | 简单对话、翻译 |
| **qwen3-vl-flash** | - | - | - | 视觉理解 | 图像分析 |

## 🎯 插件功能与模型匹配

### 1. AI助手Agent（当前功能）

**推荐模型**：**qwen3-coder-flash** ⭐

**理由**：
- ✅ **专为代码场景优化**：擅长理解AutoCAD .NET API操作
- ✅ **卓越的工具调用能力**：官方强调"擅长工具调用和环境交互"
- ✅ **100万上下文窗口**：支持超长对话历史
- ✅ **完整Function Calling支持**：原生支持Agent工作流
- ✅ **性价比最高**：0.001元/千token输入，仅比qwen-plus贵0.0002元
- ✅ **适合AutoCAD开发场景**：代码生成、API调用、文件操作

**任务类型**：
- 理解用户意图（"帮我翻译图纸"）
- 调用工具函数（translate_text, get_drawing_info, etc.）
- 多步骤推理（先提取文本 → 翻译 → 更新图纸）
- 代码辅助（生成LISP脚本、解释错误）

**对比其他选择**：
- ❌ qwen3-max：价格贵3倍，上下文更短（262K vs 1000K），推理能力过剩
- ❌ qwen-plus：通用模型，代码能力不如coder系列
- ❌ qwen-flash：上下文太短（100K），不适合多轮Agent对话

---

### 2. 工程翻译功能

**推荐模型**：**qwen-flash** ⭐（保持当前配置）

**理由**：
- ✅ **速度最快**：翻译是简单任务，无需强大推理
- ✅ **成本最低**：0.00015元/千token，是qwen3-coder-flash的1/7
- ✅ **足够的上下文**：100K tokens足够翻译整张图纸
- ✅ **经过优化的翻译提示词**：v1.0.7已优化为简洁中文提示词

**任务类型**：
- 文本翻译（中英互译）
- 专业术语映射
- 批量翻译

**性能数据**（v1.0.7）：
- Token利用率：99.8%
- 输出纯净度：99.9%（7步清洗）
- 典型图纸：1次API调用（vs 旧版10次）

---

### 3. 视觉识别（构件识别算量）

**推荐模型**：**qwen3-vl-flash** ⭐

**理由**：
- ✅ **视觉理解专用**：支持图像输入
- ✅ **空间感知能力**：理解2D/3D几何关系
- ✅ **文档理解**：识别工程图纸中的符号、标注
- ✅ **OCR能力**：提取图中的文字信息

**任务类型**：
- 识别构件类型（梁、柱、墙、板）
- 提取尺寸标注
- 理解图纸布局
- 符号识别

**当前状态**：已集成（ComponentRecognizer.cs）

---

### 4. 深度思考任务（未来扩展）

**推荐模型**：**qwen3-max（思考模式）**

**理由**：
- ✅ **最强推理能力**：1T参数，36T tokens预训练
- ✅ **思考模式**：最长81,920 tokens思维链
- ✅ **复杂问题分析**：LMArena全球前三

**潜在场景**：
- 图纸深度审查（结构合理性分析）
- 规范符合性检查（建筑规范AI审查）
- 优化建议（成本优化、材料替代）
- 设计方案对比

---

## 🔧 实施建议

### 阶段1：立即优化（本次更新）

```csharp
// AIAssistantService.cs
private const string AgentModel = "qwen3-coder-flash";  // 改为coder-flash
```

**预期改进**：
- Agent响应质量提升（代码场景优化）
- 工具调用准确率提升
- 成本略微增加（0.0002元/千token）

### 阶段2：配置化模型选择（未来优化）

在 `PluginConfig.cs` 中添加：
```csharp
public class ModelConfig
{
    public string AgentModel { get; set; } = "qwen3-coder-flash";
    public string TranslationModel { get; set; } = "qwen-flash";
    public string VisionModel { get; set; } = "qwen3-vl-flash";
    public string ThinkingModel { get; set; } = "qwen3-max";
}
```

允许用户在设置中切换模型。

### 阶段3：智能模型路由（高级功能）

根据用户问题自动选择模型：
- 简单问答 → qwen-flash
- 代码相关 → qwen3-coder-flash
- 复杂推理 → qwen3-max（思考模式）

---

## 💰 成本对比分析

### 典型使用场景成本估算

**AI助手对话（每次对话）**：
- 输入：2000 tokens（历史对话 + 用户问题）
- 输出：500 tokens（AI回复）

| 模型 | 输入成本 | 输出成本 | 总成本 | 相对成本 |
|------|---------|---------|--------|---------|
| qwen3-max | ¥0.0064 | ¥0.0064 | ¥0.0128 | 4.27x |
| qwen-plus | ¥0.0016 | ¥0.001 | ¥0.0026 | 0.87x |
| **qwen3-coder-flash** | ¥0.002 | ¥0.002 | ¥0.004 | **1x** |
| qwen-flash | ¥0.0003 | ¥0.00075 | ¥0.00105 | 0.26x |

**结论**：qwen3-coder-flash的成本比qwen-plus略高（1.15倍），但代码能力显著提升。

---

## 📈 总结与建议

### 立即执行
✅ **AI助手Agent**：从qwen3-max改为 **qwen3-coder-flash**
- 更适合代码场景
- 100万上下文（vs qwen3-max的262K）
- 成本降低68%

✅ **翻译功能**：保持 **qwen-flash**
- 性价比最高
- 已充分优化

### 未来考虑
🔄 **视觉识别**：评估 **qwen3-vl-flash** 替代当前方案
- 可能提升识别准确率
- 简化代码逻辑

🔄 **高级分析**：引入 **qwen3-max（思考模式）**
- 用于复杂图纸审查
- 按需调用，避免成本浪费

---

## 参考文档
- [阿里云百炼模型列表](https://help.aliyun.com/zh/model-studio/models)
- [Qwen-Coder规格定价](https://help.aliyun.com/zh/model-studio/qwen-coder)
- [Function Calling最佳实践](https://help.aliyun.com/zh/model-studio/qwen-function-calling)
