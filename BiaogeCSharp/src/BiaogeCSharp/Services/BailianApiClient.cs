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
    private readonly HttpClient _httpClient;
    private readonly ILogger<BailianApiClient> _logger;
    private readonly string _apiKey;

    public BailianApiClient(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<BailianApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["Bailian:ApiKey"] ?? "";

        _httpClient.BaseAddress = new Uri("https://dashscope.aliyuncs.com");
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
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

                var response = await _httpClient.PostAsJsonAsync(
                    "/api/v1/services/translation/batch-translate",
                    request,
                    cancellationToken
                );

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

            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/services/translation/translate",
                request,
                cancellationToken
            );

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

public record BailianUsage(
    int InputTokens,
    int OutputTokens
);
