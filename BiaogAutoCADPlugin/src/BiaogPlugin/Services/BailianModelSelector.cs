using System;
using System.Collections.Generic;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 阿里云百炼模型选择器 - 基于2025年最新模型体系
    /// </summary>
    public static class BailianModelSelector
    {
        /// <summary>
        /// 模型配置 - 包含所有可用模型的详细信息
        /// </summary>
        public static class Models
        {
            // ========== 旗舰对话模型 ==========

            /// <summary>
            /// Qwen-Max - 能力最强，适合复杂任务
            /// 上下文: 262K tokens
            /// 定价: ¥0.006/千token (输入), ¥0.024/千token (输出)
            /// </summary>
            public const string QwenMax = "qwen-max";

            /// <summary>
            /// Qwen-Plus - 效果、速度、成本均衡
            /// 上下文: 1M tokens
            /// 定价: ¥0.0008/千token (输入), ¥0.002/千token (输出)
            /// </summary>
            public const string QwenPlus = "qwen-plus";

            /// <summary>
            /// Qwen-Flash - 速度快、成本低
            /// 上下文: 1M tokens
            /// 定价: ¥0.00015/千token (输入), ¥0.0015/千token (输出)
            /// </summary>
            public const string QwenFlash = "qwen-flash";

            // ========== 推理模型（深度思考）==========

            /// <summary>
            /// QwQ-Max-Preview - 深度推理模型，显示完整思维链
            /// 适合: 复杂推理、数学、编程
            /// </summary>
            public const string QwQMaxPreview = "qwq-max-preview";

            /// <summary>
            /// Qwen3-Max-Thinking - 带思考模式的Qwen3
            /// 支持: thinking_budget参数控制推理token
            /// </summary>
            public const string Qwen3MaxThinking = "qwen3-max-thinking";

            // ========== 专用模型 ==========

            /// <summary>
            /// Qwen-MT - 翻译专用模型
            /// 支持: 92种语言（中、英、日、韩、法、西、德、泰、印尼、越南、阿拉伯等）
            /// 特点: 轻量、快速、低成本
            /// </summary>
            public const string QwenMT = "qwen-mt";

            /// <summary>
            /// Qwen-Coder - 代码专用模型
            /// 上下文: 1M tokens
            /// 定价: ¥0.001/千token (输入), ¥0.004/千token (输出)
            /// 擅长: 工具调用（Function Calling）、环境交互、代码生成
            /// </summary>
            public const string QwenCoder = "qwen-coder";

            /// <summary>
            /// Qwen3-Coder-Plus - 增强版代码模型
            /// 特点: 工具调用鲁棒性提升、代码安全性增强
            /// </summary>
            public const string Qwen3CoderPlus = "qwen3-coder-plus";

            // ========== 视觉模型 ==========

            /// <summary>
            /// Qwen3-VL-Plus - 视觉理解增强版
            /// 特点: 视觉编码、空间感知、多模态推理、超长视频理解
            /// 定价: 分层计费（基于输入token数量）
            /// </summary>
            public const string Qwen3VLPlus = "qwen3-vl-plus";

            /// <summary>
            /// Qwen-VL-OCR - 文档OCR专用模型
            /// 支持: 文档、表格、试卷、手写识别
            /// 语言: 英、法、日、韩、德、意
            /// </summary>
            public const string QwenVLOCR = "qwen-vl-ocr";

            /// <summary>
            /// Qwen-VL-Max - 视觉理解旗舰模型
            /// 适合: 通用图像理解、图像问答
            /// </summary>
            public const string QwenVLMax = "qwen-vl-max";

            // ========== 全模态模型 ==========

            /// <summary>
            /// Qwen3-Omni - 全模态模型
            /// 支持: 文本+图像+音频+视频
            /// 性能: 32个开源SOTA，22个整体SOTA
            /// 延迟: 音频对话低至211ms
            /// 语言: 119种文本、19种语音理解、10种语音生成
            /// </summary>
            public const string Qwen3Omni = "qwen3-omni";

            /// <summary>
            /// Qwen3-Omni-Flash - 轻量级全模态模型
            /// 特点: 速度更快，成本更低
            /// </summary>
            public const string Qwen3OmniFlash = "qwen3-omni-flash";
        }

        /// <summary>
        /// 任务类型枚举
        /// </summary>
        public enum TaskType
        {
            /// <summary>文本翻译</summary>
            Translation,
            /// <summary>AI对话助手</summary>
            Conversation,
            /// <summary>构件识别</summary>
            ComponentRecognition,
            /// <summary>深度思考推理</summary>
            DeepThinking,
            /// <summary>代码/工具调用</summary>
            CodeToolCalling,
            /// <summary>图纸OCR识别</summary>
            OCR,
            /// <summary>视觉理解</summary>
            VisionUnderstanding,
            /// <summary>全模态分析</summary>
            MultimodalAnalysis
        }

        /// <summary>
        /// 根据任务类型选择最优模型
        /// </summary>
        public static string GetOptimalModel(TaskType taskType, bool highPerformance = false)
        {
            return taskType switch
            {
                // 翻译 - 使用专用翻译模型（轻量快速）
                TaskType.Translation => Models.QwenMT,

                // AI对话 - 高性能用Max，平衡用Plus，快速用Flash
                TaskType.Conversation when highPerformance => Models.QwenMax,
                TaskType.Conversation => Models.QwenPlus,

                // 构件识别 - 平衡性能和成本
                TaskType.ComponentRecognition => Models.QwenPlus,

                // 深度思考 - 使用推理模型
                TaskType.DeepThinking => Models.QwQMaxPreview,

                // 代码/工具调用 - 使用Coder模型
                TaskType.CodeToolCalling => Models.Qwen3CoderPlus,

                // OCR - 使用专用OCR模型
                TaskType.OCR => Models.QwenVLOCR,

                // 视觉理解 - 使用VL模型
                TaskType.VisionUnderstanding => Models.Qwen3VLPlus,

                // 全模态 - 使用Omni模型
                TaskType.MultimodalAnalysis when highPerformance => Models.Qwen3Omni,
                TaskType.MultimodalAnalysis => Models.Qwen3OmniFlash,

                _ => Models.QwenPlus // 默认
            };
        }

        /// <summary>
        /// 获取模型的详细信息
        /// </summary>
        public static ModelInfo GetModelInfo(string modelName)
        {
            return modelName switch
            {
                Models.QwenMax => new ModelInfo
                {
                    Name = "通义千问Max",
                    Model = Models.QwenMax,
                    Description = "能力最强，适合复杂任务",
                    ContextLength = 262144,
                    InputCost = 0.006m,
                    OutputCost = 0.024m,
                    Features = new[] { "复杂推理", "长文本", "高质量" }
                },

                Models.QwenPlus => new ModelInfo
                {
                    Name = "通义千问Plus",
                    Model = Models.QwenPlus,
                    Description = "效果、速度、成本均衡",
                    ContextLength = 1000000,
                    InputCost = 0.0008m,
                    OutputCost = 0.002m,
                    Features = new[] { "均衡", "超长上下文", "性价比高" }
                },

                Models.QwenFlash => new ModelInfo
                {
                    Name = "通义千问Flash",
                    Model = Models.QwenFlash,
                    Description = "速度快、成本低",
                    ContextLength = 1000000,
                    InputCost = 0.00015m,
                    OutputCost = 0.0015m,
                    Features = new[] { "极速", "低成本", "简单任务" }
                },

                Models.QwQMaxPreview => new ModelInfo
                {
                    Name = "QwQ深度思考",
                    Model = Models.QwQMaxPreview,
                    Description = "深度推理模型，显示完整思维链",
                    ContextLength = 32768,
                    Features = new[] { "深度推理", "思维链", "数学编程" }
                },

                Models.QwenMT => new ModelInfo
                {
                    Name = "通义千问翻译",
                    Model = Models.QwenMT,
                    Description = "翻译专用模型，支持92种语言",
                    Features = new[] { "92语言", "轻量快速", "低成本" }
                },

                Models.Qwen3CoderPlus => new ModelInfo
                {
                    Name = "通义千问Coder Plus",
                    Model = Models.Qwen3CoderPlus,
                    Description = "增强版代码模型，擅长工具调用",
                    ContextLength = 1000000,
                    InputCost = 0.001m,
                    OutputCost = 0.004m,
                    Features = new[] { "Function Calling", "代码生成", "环境交互" }
                },

                Models.Qwen3VLPlus => new ModelInfo
                {
                    Name = "通义千问VL Plus",
                    Model = Models.Qwen3VLPlus,
                    Description = "视觉理解增强版",
                    Features = new[] { "视觉编码", "空间感知", "多模态推理", "超长视频" }
                },

                Models.QwenVLOCR => new ModelInfo
                {
                    Name = "通义千问VL-OCR",
                    Model = Models.QwenVLOCR,
                    Description = "文档OCR专用模型",
                    Features = new[] { "文档识别", "表格提取", "手写识别", "6语言" }
                },

                Models.Qwen3Omni => new ModelInfo
                {
                    Name = "通义千问Omni",
                    Model = Models.Qwen3Omni,
                    Description = "全模态模型（文本+图像+音频+视频）",
                    Features = new[] { "全模态", "32 SOTA", "低延迟", "119语言" }
                },

                _ => new ModelInfo
                {
                    Name = "未知模型",
                    Model = modelName,
                    Description = "自定义模型"
                }
            };
        }

        /// <summary>
        /// 获取所有可用模型列表
        /// </summary>
        public static List<ModelInfo> GetAllModels()
        {
            return new List<ModelInfo>
            {
                GetModelInfo(Models.QwenMax),
                GetModelInfo(Models.QwenPlus),
                GetModelInfo(Models.QwenFlash),
                GetModelInfo(Models.QwQMaxPreview),
                GetModelInfo(Models.QwenMT),
                GetModelInfo(Models.Qwen3CoderPlus),
                GetModelInfo(Models.Qwen3VLPlus),
                GetModelInfo(Models.QwenVLOCR),
                GetModelInfo(Models.Qwen3Omni)
            };
        }
    }

    /// <summary>
    /// 模型信息
    /// </summary>
    public class ModelInfo
    {
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public string Description { get; set; } = "";
        public int ContextLength { get; set; }
        public decimal InputCost { get; set; }  // 元/千token
        public decimal OutputCost { get; set; } // 元/千token
        public string[] Features { get; set; } = Array.Empty<string>();

        public override string ToString()
        {
            var cost = InputCost > 0
                ? $"¥{InputCost:F4}/¥{OutputCost:F4} (输入/输出 千token)"
                : "按需计费";
            return $"{Name} - {Description}\n特性: {string.Join(", ", Features)}\n成本: {cost}";
        }
    }
}
