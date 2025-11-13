# 阿里云百炼模型配置指南

基于2025年2月最新的阿里云百炼模型体系，本插件针对不同任务选择最优模型。

## 🎯 核心优势

✅ **一个API密钥调用所有模型** - 阿里云百炼只需配置一次API密钥，即可访问所有模型
✅ **自动选择最优模型** - 插件根据任务类型自动选择性价比最高的模型
✅ **支持手动配置** - 用户可在设置中自定义每个功能使用的模型

---

## 📊 当前模型配置

| 功能 | 默认模型 | 原因 | 定价 |
|------|---------|------|------|
| **CAD文本翻译** | `qwen-mt` | 翻译专用，支持92语言，轻量快速 | 极低成本 |
| **AI对话助手** | `qwen-max` | 能力最强，复杂推理 | ¥0.006/0.024 (输入/输出 千token) |
| **深度思考模式** | `qwq-max-preview` | 显示完整思维链，擅长数学编程 | 按需计费 |
| **构件识别** | `qwen-plus` | 平衡性能和成本，1M上下文 | ¥0.0008/0.002 |
| **Function Calling** | `qwen3-coder-plus` | 擅长工具调用和环境交互 | ¥0.001/0.004 |

---

## 🗂️ 完整模型列表

### 旗舰对话模型

#### **Qwen-Max** (`qwen-max`)
- **适合**: 复杂任务、高质量需求
- **上下文**: 262K tokens
- **定价**: ¥0.006/千token (输入), ¥0.024/千token (输出)
- **特点**: 能力最强，复杂推理，长文本

#### **Qwen-Plus** (`qwen-plus`)
- **适合**: 效果、速度、成本均衡
- **上下文**: 1M tokens
- **定价**: ¥0.0008/千token (输入), ¥0.002/千token (输出)
- **特点**: 超长上下文，性价比高，**推荐用于大多数任务**

#### **Qwen-Flash** (`qwen-flash`)
- **适合**: 简单任务、追求速度
- **上下文**: 1M tokens
- **定价**: ¥0.00015/千token (输入), ¥0.0015/千token (输出)
- **特点**: 极速响应，低成本

### 推理模型（深度思考）

#### **QwQ-Max-Preview** (`qwq-max-preview`)
- **适合**: 复杂推理、数学、编程问题
- **特点**: 显示完整思维链，深度推理
- **使用**: 在AI助手中输入`deep`启用

#### **Qwen3-Max-Thinking** (`qwen3-max-thinking`)
- **适合**: 需要思考过程的复杂任务
- **参数**: `thinking_budget` 控制推理token数量
- **特点**: 带思考模式的Qwen3

### 专用模型

#### **Qwen-MT** (`qwen-mt`)
- **适合**: 文本翻译
- **支持**: 92种语言（中、英、日、韩、法、西、德、泰、印尼、越南、阿拉伯等）
- **特点**: 翻译专用，轻量快速，**性价比极高**
- **使用**: 插件翻译功能默认使用此模型

#### **Qwen3-Coder-Plus** (`qwen3-coder-plus`)
- **适合**: 代码生成、工具调用
- **上下文**: 1M tokens
- **定价**: ¥0.001/千token (输入), ¥0.004/千token (输出)
- **特点**: Function Calling鲁棒性强，代码安全性高
- **使用**: AI助手调用AutoCAD工具时使用

### 视觉模型

#### **Qwen3-VL-Plus** (`qwen3-vl-plus`)
- **适合**: 图纸视觉理解
- **特点**: 视觉编码、空间感知、多模态推理、超长视频理解
- **定价**: 分层计费（基于输入token数量）
- **使用**: 未来支持图纸图像分析

#### **Qwen-VL-OCR** (`qwen-vl-ocr`)
- **适合**: 文档OCR识别
- **支持**: 文档、表格、试卷、手写识别
- **语言**: 英、法、日、韩、德、意
- **使用**: 未来支持扫描图纸识别

#### **Qwen-VL-Max** (`qwen-vl-max`)
- **适合**: 通用图像理解
- **特点**: 视觉理解旗舰模型
- **使用**: 图像问答、图纸分析

### 全模态模型

#### **Qwen3-Omni** (`qwen3-omni`)
- **适合**: 多模态理解（文本+图像+音频+视频）
- **性能**: 32个开源SOTA，22个整体SOTA
- **延迟**: 音频对话低至211ms
- **语言**: 119种文本、19种语音理解、10种语音生成
- **使用**: 未来支持语音交互

#### **Qwen3-Omni-Flash** (`qwen3-omni-flash`)
- **适合**: 轻量级多模态任务
- **特点**: 速度更快，成本更低

---

## ⚙️ 自定义模型配置

### 方法1：通过设置对话框
1. 在AutoCAD中输入 `BIAOGE_SETTINGS`
2. 在"模型配置"选项卡中选择每个功能使用的模型
3. 点击"保存"

### 方法2：手动编辑配置文件
配置文件位置：`%USERPROFILE%\.biaoge\config.json`

```json
{
  "Bailian": {
    "ApiKey": "sk-your-api-key",
    "TextTranslationModel": "qwen-mt",
    "ConversationModel": "qwen-max",
    "DeepThinkingModel": "qwq-max-preview",
    "RecognitionModel": "qwen-plus",
    "ToolCallingModel": "qwen3-coder-plus"
  }
}
```

---

## 💡 使用建议

### 成本优化
- **翻译任务**: 使用`qwen-mt`（专用翻译模型，成本极低）
- **简单对话**: 使用`qwen-flash`（速度快，成本低）
- **复杂任务**: 使用`qwen-plus`（性价比最高）
- **关键任务**: 使用`qwen-max`（质量最高）

### 性能优化
- **启用缓存**: 翻译缓存命中率可达90%+
- **批量处理**: 翻译功能自动批量（50条/次），减少API调用
- **智能选择**: 插件自动为不同任务选择最优模型

### 免费额度
- 每个模型提供**100万token免费额度**
- 有效期为开通百炼后**90天**
- 建议先用免费额度测试各模型效果

---

## 🔧 代码示例

### 使用模型选择器
```csharp
using BiaogPlugin.Services;

// 获取翻译任务的最优模型
var translationModel = BailianModelSelector.GetOptimalModel(
    BailianModelSelector.TaskType.Translation
);

// 获取对话任务的高性能模型
var conversationModel = BailianModelSelector.GetOptimalModel(
    BailianModelSelector.TaskType.Conversation,
    highPerformance: true
);

// 获取模型详细信息
var modelInfo = BailianModelSelector.GetModelInfo(translationModel);
Console.WriteLine(modelInfo.ToString());
```

### 查看所有可用模型
```csharp
var allModels = BailianModelSelector.GetAllModels();
foreach (var model in allModels)
{
    Console.WriteLine(model.ToString());
}
```

---

## 📚 参考资源

- [阿里云百炼官方文档](https://help.aliyun.com/zh/model-studio/)
- [模型列表](https://help.aliyun.com/zh/model-studio/models)
- [定价说明](https://help.aliyun.com/zh/model-studio/pricing)
- [API参考](https://help.aliyun.com/zh/model-studio/developer-reference/api-details)

---

## ⚠️ 注意事项

1. **API密钥安全**: 不要将API密钥提交到Git仓库
2. **成本控制**: 关注API调用量，合理使用缓存
3. **模型更新**: 阿里云百炼持续更新模型，建议关注官方公告
4. **功能支持**: 部分模型功能（如OCR、全模态）将在未来版本支持

---

最后更新: 2025-02-25
