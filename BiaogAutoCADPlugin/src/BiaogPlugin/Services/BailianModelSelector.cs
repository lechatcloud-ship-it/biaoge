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
        /// 用户只需一个API密钥即可调用所有模型
        /// </summary>
        public static class Models
        {
            // ========== Flash系列（2025推荐，一个密钥调用所有）==========

            /// <summary>
            /// qwen-mt-flash - 文本翻译专用模型
            /// 支持: 92种语言，稳定术语定制、格式还原度、领域提示能力
            /// 特点: 轻量、快速、低成本
            /// </summary>
            public const string QwenMTFlash = "qwen-mt-flash";

            /// <summary>
            /// qwen3-omni-flash - 全模态模型
            /// 支持: 文本+图像+音频+视频统一处理
            /// 特点: 速度快、成本低、多模态融合
            /// </summary>
            public const string Qwen3OmniFlash = "qwen3-omni-flash";

            /// <summary>
            /// qwen3-vl-flash - 视觉理解模型
            /// 特点: 空间感知与万物识别，具备视觉2D/3D定位能力
            /// 适合: CAD图纸分析、构件识别
            /// </summary>
            public const string Qwen3VLFlash = "qwen3-vl-flash";

            /// <summary>
            /// qwen3-flash - 思考模式融合模型（2025最新推荐）
            /// 上下文: 1M tokens (997K输入 + 32K输出)
            /// 特点: 思考/非思考模式融合，复杂推理能力显著提高
            /// 适合: 复杂翻译、专业术语理解、工程规范文本
            /// 计费: 按上下文阶梯计费，性价比极高
            /// ✅ 推荐用于翻译任务 - 替代qwen3-max-preview
            /// </summary>
            public const string Qwen3Flash = "qwen3-flash";

            /// <summary>
            /// qwen3-plus - 思考模式融合模型（2025最强推理）
            /// 上下文: 1M tokens (997K输入 + 32K输出)
            /// 特点: 推理能力超过QwQ，通用能力超过Qwen2.5-Plus
            /// 适合: 最复杂的推理任务、AI Agent核心
            /// 计费: 按上下文阶梯计费
            /// ✅ 业界SOTA水平
            /// </summary>
            public const string Qwen3Plus = "qwen3-plus";

            /// <summary>
            /// qwen3-max-preview - 思考模式融合模型（旧版）
            /// 上下文: 256K tokens
            /// 特点: 实现思考模式和非思考模式的有效融合
            /// 适合: 复杂推理、深度分析、问题求解
            /// ⚠️ 建议升级到qwen3-flash或qwen3-plus（1M上下文）
            /// </summary>
            public const string Qwen3MaxPreview = "qwen3-max-preview";

            /// <summary>
            /// qwen3-asr-flash - 语音识别模型
            /// 特点: 快速准确的语音转文字
            /// </summary>
            public const string Qwen3ASRFlash = "qwen3-asr-flash";

            /// <summary>
            /// qwen3-coder-flash - Agent工具调用代码模型
            /// 特点: 优化了仓库级别理解能力
            /// 擅长: Function Calling、代码生成、环境交互
            /// </summary>
            public const string Qwen3CoderFlash = "qwen3-coder-flash";

            // ========== 备用高级模型（旧版，建议使用qwen3系列）==========

            /// <summary>
            /// qwen-max - 旗舰模型，最强能力（旧版）
            /// 上下文: 262K tokens
            /// ⚠️ 建议升级到qwen3-plus（1M上下文）
            /// </summary>
            public const string QwenMax = "qwen-max";

            /// <summary>
            /// qwen-plus - 高性价比模型（旧版）
            /// 上下文: 1M tokens
            /// ⚠️ 建议升级到qwen3-flash或qwen3-plus（思考模式融合）
            /// </summary>
            public const string QwenPlus = "qwen-plus";

            /// <summary>
            /// qwq-max-preview - 深度思考模型，显示完整思维链
            /// 适合: 复杂推理、数学、编程
            /// </summary>
            public const string QwQMaxPreview = "qwq-max-preview";
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
        /// 根据任务类型选择最优模型（2025 Flash系列推荐）
        /// 用户只需一个API密钥即可调用所有模型
        /// </summary>
        public static string GetOptimalModel(TaskType taskType, bool highPerformance = false)
        {
            return taskType switch
            {
                // ✅ 翻译 - 使用Qwen3-Flash（1M上下文，思考模式融合，专业术语理解强）
                // 替代qwen-mt-flash: Qwen3-Flash推理能力更强，更好理解复杂工程规范
                TaskType.Translation => Models.Qwen3Flash,

                // AI对话 - 使用思考模式融合模型
                TaskType.Conversation when highPerformance => Models.Qwen3Plus,  // ✅ 升级到Qwen3-Plus
                TaskType.Conversation => Models.Qwen3Flash,  // ✅ 升级到Qwen3-Flash

                // 构件识别 - 使用视觉Flash模型（空间感知+2D/3D定位）
                TaskType.ComponentRecognition => Models.Qwen3VLFlash,

                // 深度思考 - 使用最强推理模型
                TaskType.DeepThinking => Models.Qwen3Plus,  // ✅ 升级到Qwen3-Plus

                // 代码/工具调用 - 使用Coder Flash模型（仓库级别理解）
                TaskType.CodeToolCalling => Models.Qwen3CoderFlash,

                // OCR - 使用视觉Flash模型
                TaskType.OCR => Models.Qwen3VLFlash,

                // 视觉理解 - 使用VL Flash模型
                TaskType.VisionUnderstanding => Models.Qwen3VLFlash,

                // 全模态 - 使用Omni Flash模型
                TaskType.MultimodalAnalysis => Models.Qwen3OmniFlash,

                _ => Models.Qwen3Flash // ✅ 默认使用Qwen3-Flash（1M上下文）
            };
        }

        /// <summary>
        /// 获取模型的详细信息
        /// </summary>
        public static ModelInfo GetModelInfo(string modelName)
        {
            return modelName switch
            {
                // Flash系列（2025推荐）
                Models.QwenMTFlash => new ModelInfo
                {
                    Name = "翻译Flash",
                    Model = Models.QwenMTFlash,
                    Description = "文本翻译专用（92语言，稳定术语定制、格式还原）",
                    Features = new[] { "92语言", "术语定制", "格式还原", "领域提示" }
                },

                Models.Qwen3OmniFlash => new ModelInfo
                {
                    Name = "全模态Flash",
                    Model = Models.Qwen3OmniFlash,
                    Description = "全模态统一处理（文本+图像+音频+视频）",
                    Features = new[] { "全模态", "速度快", "成本低", "多模态融合" }
                },

                Models.Qwen3VLFlash => new ModelInfo
                {
                    Name = "视觉理解Flash",
                    Model = Models.Qwen3VLFlash,
                    Description = "空间感知与万物识别，具备视觉2D/3D定位能力",
                    Features = new[] { "空间感知", "2D/3D定位", "CAD图纸分析", "构件识别" }
                },

                Models.Qwen3Flash => new ModelInfo
                {
                    Name = "Qwen3-Flash（2025推荐）",
                    Model = Models.Qwen3Flash,
                    Description = "思考模式融合，1M上下文，复杂推理能力显著提高",
                    ContextLength = 1048576,  // 1M tokens
                    Features = new[] { "1M上下文", "思考模式融合", "复杂翻译", "专业术语理解", "阶梯计费" }
                },

                Models.Qwen3Plus => new ModelInfo
                {
                    Name = "Qwen3-Plus（2025最强）",
                    Model = Models.Qwen3Plus,
                    Description = "推理能力超过QwQ，通用能力超过Qwen2.5-Plus，业界SOTA",
                    ContextLength = 1048576,  // 1M tokens
                    Features = new[] { "1M上下文", "最强推理", "业界SOTA", "思考模式融合", "阶梯计费" }
                },

                Models.Qwen3MaxPreview => new ModelInfo
                {
                    Name = "思考融合Preview（旧版）",
                    Model = Models.Qwen3MaxPreview,
                    Description = "思考模式和非思考模式有效融合（建议升级到Qwen3-Flash）",
                    ContextLength = 262144,  // 256K tokens
                    Features = new[] { "思考融合", "复杂推理", "深度分析", "问题求解" }
                },

                Models.Qwen3ASRFlash => new ModelInfo
                {
                    Name = "语音识别Flash",
                    Model = Models.Qwen3ASRFlash,
                    Description = "快速准确的语音转文字",
                    Features = new[] { "语音识别", "快速", "准确" }
                },

                Models.Qwen3CoderFlash => new ModelInfo
                {
                    Name = "代码工具Flash",
                    Model = Models.Qwen3CoderFlash,
                    Description = "Agent工具调用，优化仓库级别理解",
                    Features = new[] { "Function Calling", "仓库理解", "代码生成", "环境交互" }
                },

                // 备用高级模型
                Models.QwenMax => new ModelInfo
                {
                    Name = "通义千问Max",
                    Model = Models.QwenMax,
                    Description = "旗舰模型，最强能力",
                    ContextLength = 262144,
                    Features = new[] { "最强能力", "复杂推理", "高质量" }
                },

                Models.QwenPlus => new ModelInfo
                {
                    Name = "通义千问Plus",
                    Model = Models.QwenPlus,
                    Description = "高性价比，效果速度成本均衡",
                    ContextLength = 1000000,
                    Features = new[] { "均衡", "超长上下文", "性价比高" }
                },

                Models.QwQMaxPreview => new ModelInfo
                {
                    Name = "QwQ深度思考",
                    Model = Models.QwQMaxPreview,
                    Description = "深度推理模型，显示完整思维链",
                    ContextLength = 32768,
                    Features = new[] { "深度推理", "完整思维链", "数学编程" }
                },

                _ => new ModelInfo
                {
                    Name = "自定义模型",
                    Model = modelName,
                    Description = "用户自定义模型"
                }
            };
        }

        /// <summary>
        /// 获取所有可用模型列表（2025 Flash系列推荐）
        /// </summary>
        public static List<ModelInfo> GetAllModels()
        {
            return new List<ModelInfo>
            {
                // Flash系列（推荐）
                GetModelInfo(Models.QwenMTFlash),
                GetModelInfo(Models.Qwen3OmniFlash),
                GetModelInfo(Models.Qwen3VLFlash),
                GetModelInfo(Models.Qwen3MaxPreview),
                GetModelInfo(Models.Qwen3ASRFlash),
                GetModelInfo(Models.Qwen3CoderFlash),

                // 备用高级模型
                GetModelInfo(Models.QwenMax),
                GetModelInfo(Models.QwenPlus),
                GetModelInfo(Models.QwQMaxPreview)
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
