using System.Text.Json.Serialization;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// 插件配置模型
    /// </summary>
    public class PluginConfig
    {
        /// <summary>
        /// 百炼API配置
        /// </summary>
        [JsonPropertyName("bailian")]
        public BailianConfig Bailian { get; set; } = new BailianConfig();

        /// <summary>
        /// 翻译配置
        /// </summary>
        [JsonPropertyName("translation")]
        public TranslationConfig Translation { get; set; } = new TranslationConfig();

        /// <summary>
        /// UI配置
        /// </summary>
        [JsonPropertyName("ui")]
        public UIConfig UI { get; set; } = new UIConfig();

        /// <summary>
        /// 输入法配置
        /// </summary>
        [JsonPropertyName("inputMethod")]
        public InputMethodConfig InputMethod { get; set; } = new InputMethodConfig();
    }

    /// <summary>
    /// 百炼API配置
    /// </summary>
    public class BailianConfig
    {
        /// <summary>
        /// API密钥
        /// </summary>
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// API基础URL
        /// </summary>
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

        /// <summary>
        /// 文本翻译模型
        /// </summary>
        [JsonPropertyName("textTranslationModel")]
        public string TextTranslationModel { get; set; } = "qwen-mt-flash";

        /// <summary>
        /// 图像翻译模型
        /// </summary>
        [JsonPropertyName("imageTranslationModel")]
        public string ImageTranslationModel { get; set; } = "qwen-vl-max";

        /// <summary>
        /// 多模态对话模型
        /// </summary>
        [JsonPropertyName("multimodalDialogModel")]
        public string MultimodalDialogModel { get; set; } = "qwen-vl-max";

        /// <summary>
        /// Agent核心模型
        /// </summary>
        [JsonPropertyName("agentCoreModel")]
        public string AgentCoreModel { get; set; } = "qwen3-max-preview";

        /// <summary>
        /// 代码分析模型
        /// </summary>
        [JsonPropertyName("codeAnalysisModel")]
        public string CodeAnalysisModel { get; set; } = "qwen3-coder-flash";
    }

    /// <summary>
    /// 翻译配置
    /// </summary>
    public class TranslationConfig
    {
        /// <summary>
        /// 批处理大小
        /// </summary>
        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// 启用缓存
        /// </summary>
        [JsonPropertyName("enableCache")]
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// 缓存过期天数
        /// </summary>
        [JsonPropertyName("cacheExpirationDays")]
        public int CacheExpirationDays { get; set; } = 30;

        /// <summary>
        /// 默认目标语言
        /// </summary>
        [JsonPropertyName("defaultTargetLanguage")]
        public string DefaultTargetLanguage { get; set; } = "zh";

        /// <summary>
        /// 启用双击文本快速翻译
        /// </summary>
        [JsonPropertyName("enableDoubleClickTranslation")]
        public bool EnableDoubleClickTranslation { get; set; } = true;

        /// <summary>
        /// 双击翻译默认语言
        /// </summary>
        [JsonPropertyName("doubleClickTargetLanguage")]
        public string DoubleClickTargetLanguage { get; set; } = "zh";

        /// <summary>
        /// 显示翻译预览（不直接应用）
        /// </summary>
        [JsonPropertyName("showTranslationPreview")]
        public bool ShowTranslationPreview { get; set; } = false;

        /// <summary>
        /// 启用翻译历史记录
        /// </summary>
        [JsonPropertyName("enableHistory")]
        public bool EnableHistory { get; set; } = true;

        /// <summary>
        /// 历史记录最大条目数
        /// </summary>
        [JsonPropertyName("historyMaxSize")]
        public int HistoryMaxSize { get; set; } = 1000;

        /// <summary>
        /// 启用质量评估
        /// </summary>
        [JsonPropertyName("enableQualityAssessment")]
        public bool EnableQualityAssessment { get; set; } = false;
    }

    /// <summary>
    /// UI配置
    /// </summary>
    public class UIConfig
    {
        /// <summary>
        /// 启用Ribbon工具栏
        /// </summary>
        [JsonPropertyName("enableRibbon")]
        public bool EnableRibbon { get; set; } = true;

        /// <summary>
        /// 启用右键上下文菜单
        /// </summary>
        [JsonPropertyName("enableContextMenu")]
        public bool EnableContextMenu { get; set; } = true;

        /// <summary>
        /// 启用双击翻译
        /// </summary>
        [JsonPropertyName("enableDoubleClickTranslation")]
        public bool EnableDoubleClickTranslation { get; set; } = true;

        /// <summary>
        /// 显示翻译预览
        /// </summary>
        [JsonPropertyName("showTranslationPreview")]
        public bool ShowTranslationPreview { get; set; } = false;
    }

    /// <summary>
    /// 输入法配置
    /// </summary>
    public class InputMethodConfig
    {
        /// <summary>
        /// 自动切换输入法
        /// </summary>
        [JsonPropertyName("autoSwitch")]
        public bool AutoSwitch { get; set; } = true;

        /// <summary>
        /// 命令模式输入法
        /// </summary>
        [JsonPropertyName("commandModeIME")]
        public string CommandModeIME { get; set; } = "英文";

        /// <summary>
        /// 文本模式输入法
        /// </summary>
        [JsonPropertyName("textModeIME")]
        public string TextModeIME { get; set; } = "中文";
    }
}
