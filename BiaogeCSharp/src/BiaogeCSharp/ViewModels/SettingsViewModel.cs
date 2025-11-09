using BiaogeCSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

/// <summary>
/// 设置对话框ViewModel
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager;
    private readonly BailianApiClient _bailianApiClient;
    private readonly ILogger<SettingsViewModel> _logger;

    // 阿里云百炼设置
    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _apiEndpoint = "https://dashscope.aliyuncs.com/api/v1";

    [ObservableProperty]
    private int _requestTimeout = 30;

    [ObservableProperty]
    private int _retryCount = 3;

    [ObservableProperty]
    private string _multimodalModel = "qwen-vl-max";

    [ObservableProperty]
    private string _imageTranslationModel = "qwen-vl-max";

    [ObservableProperty]
    private string _textTranslationModel = "qwen-mt-plus";

    [ObservableProperty]
    private bool _useCustomModel;

    [ObservableProperty]
    private string _customModelName = string.Empty;

    // 翻译设置
    [ObservableProperty]
    private int _batchSize = 50;

    [ObservableProperty]
    private int _concurrentThreads = 3;

    [ObservableProperty]
    private bool _cacheEnabled = true;

    [ObservableProperty]
    private bool _qualityControlEnabled = true;

    [ObservableProperty]
    private bool _preserveFormat = true;

    [ObservableProperty]
    private string _defaultSourceLanguage = "zh";

    [ObservableProperty]
    private string _defaultTargetLanguage = "en";

    // 性能优化
    [ObservableProperty]
    private bool _spatialIndexEnabled = true;

    [ObservableProperty]
    private bool _antialiasingEnabled = true;

    [ObservableProperty]
    private int _entityLoadThreshold = 100000;

    [ObservableProperty]
    private int _fpsLimit = 60;

    [ObservableProperty]
    private int _memoryThreshold = 500;

    // 界面设置
    [ObservableProperty]
    private string _theme = "dark";

    [ObservableProperty]
    private string _language = "zh-CN";

    [ObservableProperty]
    private int _fontSize = 14;

    [ObservableProperty]
    private bool _animationsEnabled = true;

    [ObservableProperty]
    private bool _tooltipsEnabled = true;

    [ObservableProperty]
    private bool _showWelcomePage = true;

    // 高级设置
    [ObservableProperty]
    private bool _debugMode;

    [ObservableProperty]
    private string _logLevel = "INFO";

    [ObservableProperty]
    private bool _sendAnonymousStats;

    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    // 状态
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    public SettingsViewModel(
        ConfigManager configManager,
        BailianApiClient bailianApiClient,
        ILogger<SettingsViewModel> logger)
    {
        _configManager = configManager;
        _bailianApiClient = bailianApiClient;
        _logger = logger;

        LoadSettings();
    }

    /// <summary>
    /// 从配置文件加载设置
    /// </summary>
    public void LoadSettings()
    {
        try
        {
            // 阿里云百炼
            ApiKey = _configManager.GetString("Bailian:ApiKey", string.Empty);
            ApiEndpoint = _configManager.GetString("Bailian:ApiEndpoint", "https://dashscope.aliyuncs.com/api/v1");
            RequestTimeout = _configManager.GetInt("Bailian:RequestTimeout", 30);
            RetryCount = _configManager.GetInt("Bailian:RetryCount", 3);
            MultimodalModel = _configManager.GetString("Bailian:MultimodalModel", "qwen-vl-max");
            ImageTranslationModel = _configManager.GetString("Bailian:ImageTranslationModel", "qwen-vl-max");
            TextTranslationModel = _configManager.GetString("Bailian:TextTranslationModel", "qwen-mt-plus");
            UseCustomModel = _configManager.GetBool("Bailian:UseCustomModel", false);
            CustomModelName = _configManager.GetString("Bailian:CustomModelName", string.Empty);

            // 翻译设置
            BatchSize = _configManager.GetInt("Translation:BatchSize", 50);
            ConcurrentThreads = _configManager.GetInt("Translation:ConcurrentThreads", 3);
            CacheEnabled = _configManager.GetBool("Translation:CacheEnabled", true);
            QualityControlEnabled = _configManager.GetBool("Translation:QualityControlEnabled", true);
            PreserveFormat = _configManager.GetBool("Translation:PreserveFormat", true);
            DefaultSourceLanguage = _configManager.GetString("Translation:DefaultSourceLanguage", "zh");
            DefaultTargetLanguage = _configManager.GetString("Translation:DefaultTargetLanguage", "en");

            // 性能优化
            SpatialIndexEnabled = _configManager.GetBool("Performance:SpatialIndexEnabled", true);
            AntialiasingEnabled = _configManager.GetBool("Performance:AntialiasingEnabled", true);
            EntityLoadThreshold = _configManager.GetInt("Performance:EntityLoadThreshold", 100000);
            FpsLimit = _configManager.GetInt("Performance:FpsLimit", 60);
            MemoryThreshold = _configManager.GetInt("Performance:MemoryThreshold", 500);

            // 界面设置
            Theme = _configManager.GetString("UI:Theme", "dark");
            Language = _configManager.GetString("UI:Language", "zh-CN");
            FontSize = _configManager.GetInt("UI:FontSize", 14);
            AnimationsEnabled = _configManager.GetBool("UI:AnimationsEnabled", true);
            TooltipsEnabled = _configManager.GetBool("UI:TooltipsEnabled", true);
            ShowWelcomePage = _configManager.GetBool("UI:ShowWelcomePage", true);

            // 高级设置
            DebugMode = _configManager.GetBool("Advanced:DebugMode", false);
            LogLevel = _configManager.GetString("Advanced:LogLevel", "INFO");
            SendAnonymousStats = _configManager.GetBool("Advanced:SendAnonymousStats", false);
            AutoCheckUpdates = _configManager.GetBool("Advanced:AutoCheckUpdates", true);

            _logger.LogInformation("设置已加载");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载设置失败");
        }
    }

    /// <summary>
    /// 保存设置到配置文件
    /// </summary>
    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            // 阿里云百炼
            _configManager.SetConfig("Bailian:ApiKey", ApiKey);
            _configManager.SetConfig("Bailian:ApiEndpoint", ApiEndpoint);
            _configManager.SetConfig("Bailian:RequestTimeout", RequestTimeout);
            _configManager.SetConfig("Bailian:RetryCount", RetryCount);
            _configManager.SetConfig("Bailian:MultimodalModel", MultimodalModel);
            _configManager.SetConfig("Bailian:ImageTranslationModel", ImageTranslationModel);
            _configManager.SetConfig("Bailian:TextTranslationModel", TextTranslationModel);
            _configManager.SetConfig("Bailian:UseCustomModel", UseCustomModel);
            _configManager.SetConfig("Bailian:CustomModelName", CustomModelName);

            // 翻译设置
            _configManager.SetConfig("Translation:BatchSize", BatchSize);
            _configManager.SetConfig("Translation:ConcurrentThreads", ConcurrentThreads);
            _configManager.SetConfig("Translation:CacheEnabled", CacheEnabled);
            _configManager.SetConfig("Translation:QualityControlEnabled", QualityControlEnabled);
            _configManager.SetConfig("Translation:PreserveFormat", PreserveFormat);
            _configManager.SetConfig("Translation:DefaultSourceLanguage", DefaultSourceLanguage);
            _configManager.SetConfig("Translation:DefaultTargetLanguage", DefaultTargetLanguage);

            // 性能优化
            _configManager.SetConfig("Performance:SpatialIndexEnabled", SpatialIndexEnabled);
            _configManager.SetConfig("Performance:AntialiasingEnabled", AntialiasingEnabled);
            _configManager.SetConfig("Performance:EntityLoadThreshold", EntityLoadThreshold);
            _configManager.SetConfig("Performance:FpsLimit", FpsLimit);
            _configManager.SetConfig("Performance:MemoryThreshold", MemoryThreshold);

            // 界面设置
            _configManager.SetConfig("UI:Theme", Theme);
            _configManager.SetConfig("UI:Language", Language);
            _configManager.SetConfig("UI:FontSize", FontSize);
            _configManager.SetConfig("UI:AnimationsEnabled", AnimationsEnabled);
            _configManager.SetConfig("UI:TooltipsEnabled", TooltipsEnabled);
            _configManager.SetConfig("UI:ShowWelcomePage", ShowWelcomePage);

            // 高级设置
            _configManager.SetConfig("Advanced:DebugMode", DebugMode);
            _configManager.SetConfig("Advanced:LogLevel", LogLevel);
            _configManager.SetConfig("Advanced:SendAnonymousStats", SendAnonymousStats);
            _configManager.SetConfig("Advanced:AutoCheckUpdates", AutoCheckUpdates);

            // 刷新BailianApiClient的API密钥
            _bailianApiClient.RefreshApiKey();

            StatusMessage = "设置已保存";
            _logger.LogInformation("设置已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存设置失败");
            StatusMessage = $"保存失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 测试API连接
    /// </summary>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "请先输入API密钥";
            return;
        }

        IsTesting = true;
        StatusMessage = "正在测试连接...";

        try
        {
            // 先保存API密钥
            _configManager.SetConfig("Bailian:ApiKey", ApiKey);
            _bailianApiClient.RefreshApiKey();

            // 测试连接
            var result = await _bailianApiClient.TestConnectionAsync();

            if (result)
            {
                StatusMessage = "连接成功！API密钥有效";
                _logger.LogInformation("API连接测试成功");
            }
            else
            {
                StatusMessage = "连接失败，请检查API密钥是否正确";
                _logger.LogWarning("API连接测试失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试连接异常");
            StatusMessage = $"连接失败: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 重置所有设置到默认值
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        _configManager.Clear();
        LoadSettings();
        StatusMessage = "已重置到默认设置";
    }
}
