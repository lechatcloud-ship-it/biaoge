using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace BiaogeCSharp.Services;

/// <summary>
/// 阿里云百炼API客户端
/// </summary>
public class BailianApiClient
{
    private readonly object _apiKeyLock = new();
    private readonly HttpClient _httpClient;
    private readonly ILogger<BailianApiClient> _logger;
    private readonly ConfigManager _configManager;
    private readonly IConfiguration _configuration;
    private string? _apiKey;

    public BailianApiClient(
        HttpClient httpClient,
        ConfigManager configManager,
        IConfiguration configuration,
        ILogger<BailianApiClient> logger)
    {
        _httpClient = httpClient;
        _configManager = configManager;
        _configuration = configuration;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://dashscope.aliyuncs.com");

        // 初始化API密钥
        RefreshApiKey();
    }

    /// <summary>
    /// 刷新API密钥 - 从ConfigManager或IConfiguration读取
    /// </summary>
    public void RefreshApiKey()
    {
        lock (_apiKeyLock)
        {
            // 优先从ConfigManager读取（用户通过设置对话框保存的）
            _apiKey = _configManager.GetString("Bailian:ApiKey");

            // 如果ConfigManager中没有，尝试从IConfiguration读取（appsettings.json）
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = _configuration["Bailian:ApiKey"];
            }

            // 如果还是没有，尝试从环境变量读取
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
            }

            if (!string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogInformation("API密钥已加载");
            }
            else
            {
                _logger.LogWarning("未找到API密钥，请在设置中配置");
            }
        }
    }

    /// <summary>
    /// 获取当前API密钥（线程安全）
    /// </summary>
    private string? GetApiKey()
    {
        lock (_apiKeyLock)
        {
            return _apiKey;
        }
    }

    /// <summary>
    /// 检查API密钥是否已配置
    /// </summary>
    public bool HasApiKey
    {
        get
        {
            lock (_apiKeyLock)
            {
                return !string.IsNullOrEmpty(_apiKey);
            }
        }
    }

    /// <summary>
    /// 批量翻译
    /// </summary>
    public async Task<List<string>> TranslateBatchAsync(
        List<string> texts,
        string targetLanguage,
        string model = "qwen-mt-plus",
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        const int batchSize = 50;

        var batches = texts.Chunk(batchSize).ToList();

        for (int i = 0; i < batches.Length; i++)
        {
            var batch = batches[i].ToList();

            try
            {
                var request = new
                {
                    model = model,
                    input = new
                    {
                        source_language = "zh",
                        target_language = targetLanguage,
                        source_texts = batch
                    }
                };

                // 创建带Authorization头的请求（线程安全）
                var apiKey = GetApiKey();
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/services/translation/batch-translate")
                {
                    Content = JsonContent.Create(request)
                };
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                }

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BailianBatchResponse>(
                        cancellationToken: cancellationToken
                    );

                    if (result?.Output?.Translations != null)
                    {
                        results.AddRange(result.Output.Translations);
                    }
                }
                else
                {
                    _logger.LogWarning("批量翻译失败: {StatusCode}", response.StatusCode);
                    results.AddRange(batch); // 失败时返回原文
                }

                progress?.Report((i + 1.0) / batches.Length * 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量翻译异常");
                results.AddRange(batch);
            }
        }

        return results;
    }

    /// <summary>
    /// 单文本翻译
    /// </summary>
    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string model = "qwen-mt-plus",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                model = model,
                input = new
                {
                    source_language = "zh",
                    target_language = targetLanguage,
                    source_text = text
                }
            };

            // 创建带Authorization头的请求（线程安全）
            var apiKey = GetApiKey();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/services/translation/translate")
            {
                Content = JsonContent.Create(request)
            };
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BailianTranslateResponse>(
                    cancellationToken: cancellationToken
                );

                if (result?.Output?.Translation != null)
                {
                    return result.Output.Translation;
                }
            }
            else
            {
                _logger.LogWarning("翻译失败: {StatusCode}", response.StatusCode);
            }

            return text; // 失败时返回原文
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "翻译异常");
            return text;
        }
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                model = "qwen-mt-plus",
                input = new
                {
                    source_language = "zh",
                    target_language = "en",
                    source_text = "测试"
                }
            };

            // 创建带Authorization头的请求（线程安全）
            var apiKey = GetApiKey();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/services/translation/translate")
            {
                Content = JsonContent.Create(request)
            };
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public record BailianBatchResponse(
    BailianOutput Output,
    BailianUsage Usage
);

public record BailianOutput(
    List<string> Translations
);

public record BailianTranslateResponse(
    BailianTranslateOutput Output,
    BailianUsage Usage
);

public record BailianTranslateOutput(
    string Translation
);

public record BailianUsage(
    int InputTokens,
    int OutputTokens
);
