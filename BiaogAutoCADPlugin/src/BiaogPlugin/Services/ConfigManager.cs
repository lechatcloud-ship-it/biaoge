using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services;

/// <summary>
/// 配置管理器 - 管理用户配置文件
/// </summary>
public class ConfigManager
{
    private readonly string _configPath;
    private Dictionary<string, object?> _configCache = new();
    private PluginConfig? _typedConfig;
    private readonly object _lock = new();

    /// <summary>
    /// 强类型配置对象
    /// </summary>
    public PluginConfig Config
    {
        get
        {
            if (_typedConfig == null)
            {
                LoadTypedConfig();
            }
            return _typedConfig ?? new PluginConfig();
        }
    }

    public ConfigManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".biaoge"
        );
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, "config.json");

        // 初始化时加载配置
        LoadConfig();
        LoadTypedConfig();
    }

    private void LoadConfig()
    {
        lock (_lock)
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _configCache = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                        ?? new Dictionary<string, object?>();
                    Log.Information("配置文件已加载: {ConfigPath}", _configPath);
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "加载配置文件失败");
                    _configCache = new Dictionary<string, object?>();
                }
            }
            else
            {
                _configCache = new Dictionary<string, object?>();
                Log.Information("配置文件不存在，使用默认配置");
            }
        }
    }

    private void SaveConfig()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_configCache, options);
                File.WriteAllText(_configPath, json);
                Log.Debug("配置文件已保存: {ConfigPath}", _configPath);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "保存配置文件失败");
            }
        }
    }

    public T? GetConfig<T>(string key, T? defaultValue = default)
    {
        lock (_lock)
        {
            if (_configCache.TryGetValue(key, out var value))
            {
                if (value is JsonElement jsonElement)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }
            return defaultValue;
        }
    }

    public string GetString(string key, string defaultValue = "")
    {
        // ✅ 修复：优先从强类型配置读取（嵌套结构）
        var valueFromTypedConfig = GetFromTypedConfig(key);
        if (valueFromTypedConfig != null)
        {
            return valueFromTypedConfig.ToString() ?? defaultValue;
        }

        // 回退到扁平配置（向后兼容）
        return GetConfig<string>(key) ?? defaultValue;
    }

    /// <summary>
    /// 从强类型配置读取值
    /// 例如: "Bailian:ApiKey" → _typedConfig.Bailian.ApiKey
    /// </summary>
    private object? GetFromTypedConfig(string key)
    {
        if (_typedConfig == null)
        {
            LoadTypedConfig();
        }

        if (_typedConfig == null) return null;

        var parts = key.Split(':');
        if (parts.Length != 2) return null;

        var section = parts[0].ToLower();
        var property = parts[1];

        try
        {
            return section switch
            {
                "bailian" => GetBailianConfigValue(property),
                "translation" => GetTranslationConfigValue(property),
                "ui" => GetUIConfigValue(property),
                "inputmethod" => GetInputMethodConfigValue(property),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private object? GetBailianConfigValue(string property)
    {
        if (_typedConfig == null) return null;

        return property switch
        {
            "ApiKey" => _typedConfig.Bailian.ApiKey,
            "BaseUrl" => _typedConfig.Bailian.BaseUrl,
            "TextTranslationModel" => _typedConfig.Bailian.TextTranslationModel,
            "ImageTranslationModel" => _typedConfig.Bailian.ImageTranslationModel,
            "MultimodalDialogModel" => _typedConfig.Bailian.MultimodalDialogModel,
            "AgentCoreModel" => _typedConfig.Bailian.AgentCoreModel,
            "CodeAnalysisModel" => _typedConfig.Bailian.CodeAnalysisModel,
            _ => null
        };
    }

    private object? GetTranslationConfigValue(string property)
    {
        if (_typedConfig == null) return null;

        return property switch
        {
            "UseCache" => _typedConfig.Translation.EnableCache,
            "EnableCache" => _typedConfig.Translation.EnableCache,
            "BatchSize" => _typedConfig.Translation.BatchSize,
            "CacheExpirationDays" => _typedConfig.Translation.CacheExpirationDays,
            _ => null
        };
    }

    private object? GetUIConfigValue(string property)
    {
        if (_typedConfig == null) return null;

        return property switch
        {
            "EnableRibbon" => _typedConfig.UI.EnableRibbon,
            "EnableContextMenu" => _typedConfig.UI.EnableContextMenu,
            "EnableDoubleClickTranslation" => _typedConfig.UI.EnableDoubleClickTranslation,
            _ => null
        };
    }

    private object? GetInputMethodConfigValue(string property)
    {
        if (_typedConfig == null) return null;

        return property switch
        {
            "AutoSwitch" => _typedConfig.InputMethod.AutoSwitch,
            "CommandModeIME" => _typedConfig.InputMethod.CommandModeIME,
            "TextModeIME" => _typedConfig.InputMethod.TextModeIME,
            _ => null
        };
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        // ✅ 修复：优先从强类型配置读取
        var valueFromTypedConfig = GetFromTypedConfig(key);
        if (valueFromTypedConfig != null && valueFromTypedConfig is int intValue)
        {
            return intValue;
        }

        return GetConfig<int>(key, defaultValue);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        // ✅ 修复：优先从强类型配置读取
        var valueFromTypedConfig = GetFromTypedConfig(key);
        if (valueFromTypedConfig != null && valueFromTypedConfig is bool boolValue)
        {
            return boolValue;
        }

        return GetConfig<bool>(key, defaultValue);
    }

    public void SetConfig<T>(string key, T value)
    {
        lock (_lock)
        {
            _configCache[key] = value;

            // ✅ 修复：同时更新强类型配置，确保格式一致
            UpdateTypedConfigFromKey(key, value);

            // 使用强类型配置的保存方法，确保嵌套结构
            SaveTypedConfig();
        }
    }

    /// <summary>
    /// 根据扁平键更新强类型配置
    /// 例如: "Bailian:ApiKey" → _typedConfig.Bailian.ApiKey
    /// </summary>
    private void UpdateTypedConfigFromKey<T>(string key, T value)
    {
        // 初始化 _typedConfig
        if (_typedConfig == null)
        {
            _typedConfig = new PluginConfig();
        }

        var parts = key.Split(':');
        if (parts.Length != 2) return;

        var section = parts[0].ToLower();
        var property = parts[1];

        try
        {
            switch (section)
            {
                case "bailian":
                    UpdateBailianConfig(property, value);
                    break;
                case "translation":
                    UpdateTranslationConfig(property, value);
                    break;
                case "ui":
                    UpdateUIConfig(property, value);
                    break;
                case "inputmethod":
                    UpdateInputMethodConfig(property, value);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, $"更新强类型配置失败: {key}");
        }
    }

    private void UpdateBailianConfig<T>(string property, T value)
    {
        if (_typedConfig == null) return;

        // ✅ 修复：确保Bailian对象已初始化
        if (_typedConfig.Bailian == null)
        {
            _typedConfig.Bailian = new BailianConfig();
            Log.Debug("初始化Bailian配置对象");
        }

        switch (property)
        {
            case "ApiKey":
                _typedConfig.Bailian.ApiKey = value?.ToString() ?? "";
                Log.Debug("更新ApiKey: {Length}字符", _typedConfig.Bailian.ApiKey.Length);
                break;
            case "BaseUrl":
                _typedConfig.Bailian.BaseUrl = value?.ToString() ?? "";
                break;
            case "TextTranslationModel":
                _typedConfig.Bailian.TextTranslationModel = value?.ToString() ?? "";
                break;
            case "ImageTranslationModel":
                _typedConfig.Bailian.ImageTranslationModel = value?.ToString() ?? "";
                break;
            case "MultimodalDialogModel":
                _typedConfig.Bailian.MultimodalDialogModel = value?.ToString() ?? "";
                break;
            case "AgentCoreModel":
                _typedConfig.Bailian.AgentCoreModel = value?.ToString() ?? "";
                break;
            case "CodeAnalysisModel":
                _typedConfig.Bailian.CodeAnalysisModel = value?.ToString() ?? "";
                break;
        }
    }

    private void UpdateTranslationConfig<T>(string property, T value)
    {
        if (_typedConfig == null) return;

        // ✅ 修复：确保Translation对象已初始化
        if (_typedConfig.Translation == null)
        {
            _typedConfig.Translation = new TranslationConfig();
            Log.Debug("初始化Translation配置对象");
        }

        switch (property)
        {
            case "UseCache":
            case "EnableCache":
                if (value is bool boolValue)
                    _typedConfig.Translation.EnableCache = boolValue;
                break;
            case "SkipNumbers":
            case "SkipShortText":
                // 这些属性在新的配置模型中可能不存在，忽略
                break;
            case "BatchSize":
                if (value is int intValue)
                    _typedConfig.Translation.BatchSize = intValue;
                break;
        }
    }

    private void UpdateUIConfig<T>(string property, T value)
    {
        if (_typedConfig == null) return;

        // ✅ 修复：确保UI对象已初始化
        if (_typedConfig.UI == null)
        {
            _typedConfig.UI = new UIConfig();
            Log.Debug("初始化UI配置对象");
        }

        switch (property)
        {
            case "EnableRibbon":
                if (value is bool boolValue)
                    _typedConfig.UI.EnableRibbon = boolValue;
                break;
            case "EnableContextMenu":
                if (value is bool boolValue2)
                    _typedConfig.UI.EnableContextMenu = boolValue2;
                break;
        }
    }

    private void UpdateInputMethodConfig<T>(string property, T value)
    {
        if (_typedConfig == null) return;

        // ✅ 修复：确保InputMethod对象已初始化
        if (_typedConfig.InputMethod == null)
        {
            _typedConfig.InputMethod = new InputMethodConfig();
            Log.Debug("初始化InputMethod配置对象");
        }

        switch (property)
        {
            case "AutoSwitch":
                if (value is bool boolValue)
                    _typedConfig.InputMethod.AutoSwitch = boolValue;
                break;
            case "CommandModeIME":
                _typedConfig.InputMethod.CommandModeIME = value?.ToString() ?? "";
                break;
            case "TextModeIME":
                _typedConfig.InputMethod.TextModeIME = value?.ToString() ?? "";
                break;
        }
    }

    public void SetMultiple(Dictionary<string, object?> values)
    {
        lock (_lock)
        {
            foreach (var kvp in values)
            {
                _configCache[kvp.Key] = kvp.Value;

                // ✅ 修复：同时更新强类型配置
                UpdateTypedConfigFromKey(kvp.Key, kvp.Value);
            }

            // ✅ 修复：使用强类型保存，确保嵌套格式
            SaveTypedConfig();
        }
    }

    // 异步版本（为了兼容性）
    public Task<T?> GetConfigAsync<T>(string key)
    {
        return Task.FromResult(GetConfig<T>(key));
    }

    public Task SetConfigAsync<T>(string key, T value)
    {
        SetConfig(key, value);
        return Task.CompletedTask;
    }

    public Task SetConfigSectionAsync(string section, Dictionary<string, object?> values)
    {
        SetConfig(section, values);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 重新加载配置文件
    /// </summary>
    public void Reload()
    {
        LoadConfig();

        // ✅ 修复：同时重新加载强类型配置
        LoadTypedConfig();
    }

    /// <summary>
    /// 清除所有配置
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _configCache.Clear();

            // ✅ 修复：同时清除强类型配置
            _typedConfig = new PluginConfig();

            // ✅ 修复：保存空的嵌套格式配置
            SaveTypedConfig();
        }
    }

    /// <summary>
    /// 获取所有配置键
    /// </summary>
    public List<string> GetAllKeys()
    {
        lock (_lock)
        {
            return new List<string>(_configCache.Keys);
        }
    }

    /// <summary>
    /// 检查配置键是否存在
    /// </summary>
    public bool HasKey(string key)
    {
        lock (_lock)
        {
            return _configCache.ContainsKey(key);
        }
    }

    /// <summary>
    /// 加载强类型配置
    /// </summary>
    private void LoadTypedConfig()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _typedConfig = JsonSerializer.Deserialize<PluginConfig>(json) ?? new PluginConfig();
                    Log.Debug("强类型配置已加载");
                }
                else
                {
                    _typedConfig = new PluginConfig();
                    Log.Debug("使用默认强类型配置");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "加载强类型配置失败");
                _typedConfig = new PluginConfig();
            }
        }
    }

    /// <summary>
    /// 保存强类型配置
    /// </summary>
    public void SaveTypedConfig()
    {
        lock (_lock)
        {
            try
            {
                // ✅ 确保_typedConfig和所有子对象都已初始化
                if (_typedConfig == null)
                {
                    _typedConfig = new PluginConfig();
                    Log.Warning("SaveTypedConfig时_typedConfig为null，已重新初始化");
                }
                if (_typedConfig.Bailian == null)
                {
                    _typedConfig.Bailian = new BailianConfig();
                    Log.Warning("SaveTypedConfig时Bailian为null，已重新初始化");
                }
                if (_typedConfig.Translation == null)
                {
                    _typedConfig.Translation = new TranslationConfig();
                }
                if (_typedConfig.UI == null)
                {
                    _typedConfig.UI = new UIConfig();
                }
                if (_typedConfig.InputMethod == null)
                {
                    _typedConfig.InputMethod = new InputMethodConfig();
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(_typedConfig, options);

                // ✅ 添加详细日志
                Log.Debug("准备保存配置，JSON长度: {Length}", json.Length);
                Log.Debug("ApiKey长度: {Length}", _typedConfig.Bailian?.ApiKey?.Length ?? 0);

                File.WriteAllText(_configPath, json);
                Log.Information("✅ 强类型配置已成功保存到: {ConfigPath}", _configPath);

                // 验证文件是否真的写入了
                if (File.Exists(_configPath))
                {
                    var fileInfo = new FileInfo(_configPath);
                    Log.Information("✅ 配置文件大小: {Size} 字节", fileInfo.Length);
                }
                else
                {
                    Log.Error("❌ 配置文件保存后不存在！路径: {Path}", _configPath);
                }

                // ✅ 修复：不同步到_configCache，避免键不匹配问题
                // _configCache保留扁平键用于向后兼容
                // GetString/GetInt/GetBool已优先从_typedConfig读取
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "❌ 保存强类型配置失败，路径: {Path}", _configPath);
                // ✅ 向上抛出异常，让调用方知道保存失败了
                throw;
            }
        }
    }
}
