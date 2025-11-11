using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BiaogPlugin.Services;

/// <summary>
/// 配置管理器 - 管理用户配置文件
/// </summary>
public class ConfigManager
{
    private readonly string _configPath;
    private Dictionary<string, object?> _configCache = new();
    private readonly object _lock = new();

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
                catch (Exception ex)
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
            catch (Exception ex)
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
        return GetConfig<string>(key) ?? defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return GetConfig<int>(key, defaultValue);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        return GetConfig<bool>(key, defaultValue);
    }

    public void SetConfig<T>(string key, T value)
    {
        lock (_lock)
        {
            _configCache[key] = value;
            SaveConfig();
        }
    }

    public void SetMultiple(Dictionary<string, object?> values)
    {
        lock (_lock)
        {
            foreach (var kvp in values)
            {
                _configCache[kvp.Key] = kvp.Value;
            }
            SaveConfig();
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
    }

    /// <summary>
    /// 清除所有配置
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _configCache.Clear();
            SaveConfig();
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
}
