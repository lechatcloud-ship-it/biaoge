using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// 翻译引擎
/// </summary>
public class TranslationEngine
{
    private readonly BailianApiClient _apiClient;
    private readonly CacheService _cacheService;
    private readonly ILogger<TranslationEngine> _logger;

    public TranslationEngine(
        BailianApiClient apiClient,
        CacheService cacheService,
        ILogger<TranslationEngine> logger)
    {
        _apiClient = apiClient;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// 批量翻译（带缓存）
    /// </summary>
    public async Task<List<string>> TranslateBatchWithCacheAsync(
        List<string> texts,
        string targetLanguage,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        var uncachedTexts = new List<string>();
        var uncachedIndices = new List<int>();

        // 检查缓存
        for (int i = 0; i < texts.Count; i++)
        {
            var cached = await _cacheService.GetTranslationAsync(texts[i], targetLanguage);
            if (cached != null)
            {
                results.Add(cached);
            }
            else
            {
                results.Add(""); // 占位
                uncachedTexts.Add(texts[i]);
                uncachedIndices.Add(i);
            }
        }

        _logger.LogInformation(
            "缓存命中: {CachedCount}/{TotalCount} ({HitRate:P})",
            texts.Count - uncachedTexts.Count,
            texts.Count,
            (texts.Count - uncachedTexts.Count) / (double)texts.Count
        );

        // 翻译未缓存的文本
        if (uncachedTexts.Any())
        {
            var translated = await _apiClient.TranslateBatchAsync(
                uncachedTexts,
                targetLanguage,
                progress: progress,
                cancellationToken: cancellationToken
            );

            // 更新结果并写入缓存
            for (int i = 0; i < translated.Count; i++)
            {
                var index = uncachedIndices[i];
                results[index] = translated[i];

                // 写入缓存
                await _cacheService.SetTranslationAsync(
                    uncachedTexts[i],
                    targetLanguage,
                    translated[i]
                );
            }
        }

        return results;
    }
}
