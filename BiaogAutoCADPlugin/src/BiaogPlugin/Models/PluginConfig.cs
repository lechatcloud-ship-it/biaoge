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

        /// <summary>
        /// ✅ 成本管理配置
        /// </summary>
        [JsonPropertyName("cost")]
        public CostConfig Cost { get; set; } = new CostConfig();
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
        /// 推荐: qwen3-max-preview (256K上下文，更好的专业术语翻译)
        /// 备选: qwen-mt-flash (专用翻译模型，低成本)
        /// </summary>
        [JsonPropertyName("textTranslationModel")]
        public string TextTranslationModel { get; set; } = "qwen3-max-preview";

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

    /// <summary>
    /// ✅ 成本管理配置
    /// ⚠️ 重要说明：
    /// 1. 中国无公开工程造价API，所有价格数据需用户自行维护
    /// 2. 地区差异巨大（一线城市比西部地区高30-50%）
    /// 3. 价格随材料市场波动，需定期更新
    /// 4. 默认禁用自动成本估算，避免误导
    /// </summary>
    public class CostConfig
    {
        /// <summary>
        /// ✅ 启用成本估算（默认关闭，避免误导用户）
        /// </summary>
        [JsonPropertyName("enableCostEstimation")]
        public bool EnableCostEstimation { get; set; } = false;

        /// <summary>
        /// 当前地区（华北/华东/华南/西南/西北/东北/华中）
        /// </summary>
        [JsonPropertyName("currentRegion")]
        public string CurrentRegion { get; set; } = "华东";

        /// <summary>
        /// 自定义价格数据库路径（可覆盖默认配置）
        /// </summary>
        [JsonPropertyName("customPriceDatabasePath")]
        public string CustomPriceDatabasePath { get; set; } = "";

        /// <summary>
        /// 价格数据来源说明（用户自填）
        /// </summary>
        [JsonPropertyName("priceDataSource")]
        public string PriceDataSource { get; set; } = "用户自定义";

        /// <summary>
        /// 价格数据最后更新日期
        /// </summary>
        [JsonPropertyName("lastPriceUpdate")]
        public string LastPriceUpdate { get; set; } = "";

        /// <summary>
        /// 显示成本估算警告（提醒用户价格仅供参考）
        /// </summary>
        [JsonPropertyName("showCostWarning")]
        public bool ShowCostWarning { get; set; } = true;
    }
}
