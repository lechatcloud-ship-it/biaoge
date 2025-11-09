using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// 配置管理器
/// </summary>
public class ConfigManager
{
    private readonly string _configPath;

    public ConfigManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".biaoge"
        );
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, "config.json");
    }

    public async Task<T?> GetConfigAsync<T>(string key)
    {
        if (!File.Exists(_configPath))
            return default;

        var json = await File.ReadAllTextAsync(_configPath);
        var config = JsonSerializer.Deserialize<JsonElement>(json);

        if (config.TryGetProperty(key, out var value))
        {
            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        return default;
    }

    public async Task SetConfigAsync<T>(string key, T value)
    {
        JsonElement config;
        if (File.Exists(_configPath))
        {
            var json = await File.ReadAllTextAsync(_configPath);
            config = JsonSerializer.Deserialize<JsonElement>(json);
        }
        else
        {
            config = JsonSerializer.Deserialize<JsonElement>("{}");
        }

        // 简化实现：直接覆盖整个文件
        var newConfig = new Dictionary<string, object?>
        {
            [key] = value
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var newJson = JsonSerializer.Serialize(newConfig, options);
        await File.WriteAllTextAsync(_configPath, newJson);
    }
}
