# 阿里云百炼 Flash系列模型规格说明

**一个API密钥调用所有模型** - 用户只需配置一次密钥即可使用所有功能

---

## API端点配置（硬编码）

### 统一API基础地址
```
BaseURL: https://dashscope.aliyuncs.com
```

### 端点列表
- **翻译API**: `/api/v1/services/translation/translate`
- **批量翻译API**: `/api/v1/services/translation/batch-translate`
- **对话API（兼容OpenAI）**: `/compatible-mode/v1/chat/completions`

---

## Flash系列模型详细参数（2025推荐）

### 1. qwen-mt-flash - 文本翻译专用

**模型标识**: `qwen-mt-flash`

**核心参数**:
- **上下文长度**: 16,384 tokens（输入+输出，实际API规格）
- **最大输入**: 8,192 tokens
- **最大输出**: 8,192 tokens
- **支持语言**: 92种（中、英、日、韩、法、西、德、泰、印尼、越南、阿拉伯等）

**特殊能力**:
- ✅ 稳定术语定制
- ✅ 格式还原度优化
- ✅ 领域提示能力（建筑、工程、法律等）
- ✅ 轻量快速、低成本

**使用场景**:
- CAD图纸文本翻译
- 建筑工程术语翻译
- 批量文档翻译（支持50条/批次）

**定价**: 极低成本（具体价格请参考阿里云官网）

---

### 2. qwen3-max-preview - 思考模式融合

**模型标识**: `qwen3-max-preview`

**核心参数**:
- **上下文长度**: 256,000 tokens（256K）
- **最大输入**: 254,000 tokens
- **最大输出**: 32,768 tokens
- **参数规模**: 1万亿参数（1T）

**特殊能力**:
- ✅ 思考模式和非思考模式有效融合
- ✅ 复杂推理能力强
- ✅ 数学、科学、编程问题求解
- ✅ 多语言支持（100+种语言）
- ⚠️ 非推理模型架构（不同于QwQ-Max-Preview）

**使用场景**:
- AI对话助手（BIAOGE_AI命令）
- 图纸问答（复杂问题分析）
- 深度推理任务

**定价**: 中等成本

**特殊参数**:
- `thinking_budget`: 控制思考token数量（可选）
- `temperature`: 0.7（推荐）
- `top_p`: 0.9（推荐）

---

### 3. qwen3-vl-flash - 视觉理解专用

**模型标识**: `qwen3-vl-flash`

**核心参数**:
- **上下文长度**: 128,000 tokens（128K）
- **最大输入**: 126,000 tokens
- **最大输出**: 32,768 tokens
- **图像Token计算**: 每28x28像素 = 1 token

**特殊能力**:
- ✅ 空间感知与万物识别
- ✅ 视觉2D/3D定位能力
- ✅ CAD图纸分析优化
- ✅ 构件识别（墙、柱、梁、板等）

**使用场景**:
- CAD图纸构件识别（BIAOGE_CALCULATE命令）
- 图纸OCR识别
- 视觉定位分析

**定价**: 分层计费（基于输入token）

**图像处理规则**:
- 最少需要 4个 Token/图
- 最多需要 1,280个 Token/图
- 建议图像分辨率：1024x1024像素

---

### 4. qwen3-coder-flash - Agent工具调用专用

**模型标识**: `qwen3-coder-flash`

**核心参数**:
- **上下文长度**: 256,000 tokens（原生256K，YaRN可扩展至1M）
- **最大输入**: 254,000 tokens
- **最大输出**: 32,768 tokens

**特殊能力**:
- ✅ 优化了仓库级别理解能力
- ✅ Function Calling鲁棒性提升
- ✅ 代码生成、环境交互
- ✅ 上下文缓存支持（命中率20%/10%成本）

**使用场景**:
- Agent工具调用（修改图纸、查询图层等）
- Function Calling（modify_text, query_layers等）
- 代码生成和执行

**定价**: 低成本，支持缓存优化

**缓存机制**:
- 隐式缓存命中：单价的 20% 计费
- 显式缓存命中：单价的 10% 计费

---

### 5. qwen3-omni-flash - 全模态统一

**模型标识**: `qwen3-omni-flash`

**核心参数**:
- **上下文长度**: 128,000 tokens（128K）
- **最大输入**: 126,000 tokens
- **最大输出**: 32,768 tokens

**特殊能力**:
- ✅ 文本+图像+音频+视频统一处理
- ✅ 全模态融合
- ✅ 速度快、成本低
- ✅ 混合思考模型（enable_thinking参数）

**使用场景**:
- 多模态分析（图纸+语音说明）
- 综合信息处理
- 跨模态理解

**定价**: 极低成本

**音频Token计算**:
```
总Tokens数 = 音频时长（秒） × 12.5
```

**特殊参数**:
- `enable_thinking`: 控制是否开启思考模式（可选）

---

### 6. qwen3-asr-flash - 语音识别专用

**模型标识**: `qwen3-asr-flash`

**核心参数**:
- **上下文长度**: 32,768 tokens
- **最大音频时长**: 待确认
- **支持语言**: 多种

**特殊能力**:
- ✅ 快速准确的语音转文字
- ✅ 低延迟
- ✅ 支持多语言识别

**使用场景**:
- 语音输入指令
- 语音问答
- 实时语音转文字

**定价**: 极低成本

---

## 备用高级模型

### qwen-max - 旗舰模型

**模型标识**: `qwen-max`

**核心参数**:
- **上下文长度**: 262,144 tokens（262K）
- **最大输入**: 260,000 tokens
- **最大输出**: 32,768 tokens

**特殊能力**:
- 最强能力，适合极端复杂任务
- 高质量输出

**使用场景**:
- 备用选项（当Flash系列不满足需求时）

**定价**: ¥0.006/千token（输入），¥0.024/千token（输出）

---

### qwen-plus - 高性价比模型

**模型标识**: `qwen-plus`

**核心参数**:
- **上下文长度**: 1,000,000 tokens（1M）
- **最大输入**: 997,952 tokens
- **最大输出**: 32,768 tokens

**特殊能力**:
- 效果、速度、成本均衡
- 超长上下文

**使用场景**:
- 备用选项

**定价**: ¥0.0008/千token（输入），¥0.002/千token（输出）

---

### qwq-max-preview - 深度思考模型

**模型标识**: `qwq-max-preview`

**核心参数**:
- **上下文长度**: 32,768 tokens（32K）
- **最大输入**: 30,000 tokens
- **最大输出**: 32,768 tokens

**特殊能力**:
- 显示完整思维链
- 深度推理、数学、编程

**使用场景**:
- 需要查看完整推理过程的任务

---

## API调用示例

### 1. 翻译API调用（qwen-mt-flash）

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

### 2. 对话API调用（qwen3-max-preview）

```json
POST https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions
Authorization: Bearer sk-your-api-key

{
  "model": "qwen3-max-preview",
  "messages": [
    {"role": "system", "content": "你是一个CAD图纸分析专家"},
    {"role": "user", "content": "这张图纸有哪些图层？"}
  ],
  "stream": true,
  "temperature": 0.7,
  "top_p": 0.9
}
```

### 3. Function Calling调用（qwen3-coder-flash）

```json
POST https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions
Authorization: Bearer sk-your-api-key

{
  "model": "qwen3-coder-flash",
  "messages": [
    {"role": "user", "content": "修改图纸中的文字"外墙"为"建筑外墙""}
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "modify_text",
        "description": "修改CAD图纸中的文本内容",
        "parameters": {
          "type": "object",
          "properties": {
            "original_text": {"type": "string"},
            "new_text": {"type": "string"}
          },
          "required": ["original_text", "new_text"]
        }
      }
    }
  ],
  "stream": false
}
```

---

## 配置文件示例（%USERPROFILE%\.biaoge\config.json）

```json
{
  "Bailian": {
    "ApiKey": "sk-your-api-key-here",
    "TextTranslationModel": "qwen-mt-flash",
    "ConversationModel": "qwen3-max-preview",
    "VisionModel": "qwen3-vl-flash",
    "ToolCallingModel": "qwen3-coder-flash",
    "MultimodalModel": "qwen3-omni-flash"
  },
  "Translation": {
    "UseCache": true,
    "SkipNumbers": true,
    "SkipShortText": true
  }
}
```

---

## 成本优化建议

1. **翻译任务**: 使用 `qwen-mt-flash`（极低成本）
2. **对话任务**: 使用 `qwen3-max-preview`（中等成本）
3. **视觉识别**: 使用 `qwen3-vl-flash`（分层计费）
4. **工具调用**: 使用 `qwen3-coder-flash` + 缓存优化（最低20%成本）
5. **多模态**: 使用 `qwen3-omni-flash`（极低成本）

**免费额度**: 每个模型提供100万token免费额度，有效期90天

---

## 注意事项

1. **一个密钥全调用**: 所有模型共享同一个API密钥，无需分别配置
2. **硬编码端点**: API端点已硬编码在插件中，用户无需配置
3. **自动模型选择**: 插件会根据任务类型自动选择最优模型
4. **用户可自定义**: 通过设置界面（BIAOGE_SETTINGS）可手动调整模型
5. **上下文限制**: 注意各模型的上下文长度限制，避免超限

---

## 参考文档

- 阿里云百炼官方文档: https://help.aliyun.com/zh/model-studio/models
- API文档: https://help.aliyun.com/zh/model-studio/developer-reference/
- 控制台: https://dashscope.console.aliyun.com/

---

**更新时间**: 2025-01-11
**插件版本**: v1.1.0 - Flash Series Edition
