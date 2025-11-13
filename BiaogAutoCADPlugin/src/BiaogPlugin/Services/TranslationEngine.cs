using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogPlugin.Services;

/// <summary>
/// 翻译引擎
/// </summary>
public class TranslationEngine
{
    private readonly BailianApiClient _apiClient;
    private readonly CacheService _cacheService;

    public TranslationEngine(
        BailianApiClient apiClient,
        CacheService cacheService)
    {
        _apiClient = apiClient;
        _cacheService = cacheService;
    }

    /// <summary>
    /// 单文本翻译（带缓存）
    /// </summary>
    public async Task<string> TranslateWithCacheAsync(
        string text,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        // ✅ P0修复: 添加API密钥验证
        if (!_apiClient.HasApiKey)
        {
            var errorMsg = "未配置百炼API密钥，请运行 BIAOGE_SETTINGS 命令配置";
            Log.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrWhiteSpace(text))
            return text;

        // 检查缓存
        var cached = await _cacheService.GetTranslationAsync(text, targetLanguage);
        if (cached != null)
        {
            Log.Debug("缓存命中: {Text}", text);
            return cached;
        }

        // 调用API翻译
        var translated = await _apiClient.TranslateAsync(
            text,
            targetLanguage,
            cancellationToken: cancellationToken
        );

        // 写入缓存
        await _cacheService.SetTranslationAsync(text, targetLanguage, translated);

        return translated;
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
        // ✅ P0修复: 添加API密钥验证
        if (!_apiClient.HasApiKey)
        {
            var errorMsg = "未配置百炼API密钥，请运行 BIAOGE_SETTINGS 命令配置";
            Log.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

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

        Log.Information(
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
