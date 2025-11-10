using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// DWG图纸翻译服务 - 核心业务逻辑
/// 完整流程：加载DWG → 提取文本 → 翻译 → 应用翻译 → 保存
/// </summary>
public class DwgTranslationService
{
    private readonly AsposeDwgParser _dwgParser;
    private readonly TranslationEngine _translationEngine;
    private readonly CacheService _cacheService;
    private readonly ILogger<DwgTranslationService> _logger;

    public DwgTranslationService(
        AsposeDwgParser dwgParser,
        TranslationEngine translationEngine,
        CacheService cacheService,
        ILogger<DwgTranslationService> logger)
    {
        _dwgParser = dwgParser;
        _translationEngine = translationEngine;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// 翻译DWG图纸（完整流程）
    /// </summary>
    /// <param name="inputPath">输入DWG文件路径</param>
    /// <param name="outputPath">输出DWG文件路径</param>
    /// <param name="targetLanguage">目标语言（默认：zh简体中文）</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>翻译统计信息</returns>
    public async Task<TranslationStatistics> TranslateDwgAsync(
        string inputPath,
        string outputPath,
        string targetLanguage = "zh",
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始翻译DWG图纸: {InputPath} → {OutputPath}", inputPath, outputPath);

        var stats = new TranslationStatistics
        {
            InputFile = inputPath,
            OutputFile = outputPath,
            TargetLanguage = targetLanguage,
            StartTime = DateTime.Now
        };

        try
        {
            // 步骤1: 加载DWG文件（10%进度）
            progress?.Report(10);
            _logger.LogInformation("步骤1/5: 加载DWG文件...");
            var document = _dwgParser.Parse(inputPath);
            stats.EntityCount = document.EntityCount;
            stats.LayerCount = document.Layers.Count;

            // 步骤2: 提取所有文本（30%进度）
            progress?.Report(30);
            _logger.LogInformation("步骤2/5: 提取文本...");
            var texts = _dwgParser.ExtractTexts(document);
            stats.TotalTexts = texts.Count;

            if (texts.Count == 0)
            {
                _logger.LogWarning("图纸中未找到任何文本");
                stats.EndTime = DateTime.Now;
                return stats;
            }

            _logger.LogInformation("提取了{Count}条文本", texts.Count);

            // 去重
            var uniqueTexts = texts.Distinct().ToList();
            _logger.LogInformation("去重后: {Count}条唯一文本", uniqueTexts.Count);

            // 步骤3: 翻译文本（60%进度）
            progress?.Report(60);
            _logger.LogInformation("步骤3/5: 翻译文本...");

            var translations = new Dictionary<string, string>();

            // 使用批量翻译或单文本翻译
            var translatedTexts = await _translationEngine.TranslateBatchWithCacheAsync(
                uniqueTexts,
                targetLanguage,
                new Progress<double>(p => progress?.Report(60 + p * 0.25)), // 60%-85%
                cancellationToken
            );

            // 构建翻译映射表
            for (int i = 0; i < uniqueTexts.Count && i < translatedTexts.Count; i++)
            {
                translations[uniqueTexts[i]] = translatedTexts[i];
            }

            stats.TranslatedTexts = translations.Count;
            _logger.LogInformation("翻译完成: {Count}条文本", translations.Count);

            // 步骤4: 应用翻译到DWG（85%进度）
            progress?.Report(85);
            _logger.LogInformation("步骤4/5: 应用翻译到图纸...");

            var modifiedCount = _dwgParser.ApplyTranslations(document, translations);
            stats.ModifiedEntities = modifiedCount;

            _logger.LogInformation("修改了{Count}个实体", modifiedCount);

            // 步骤5: 保存翻译后的DWG（95%进度）
            progress?.Report(95);
            _logger.LogInformation("步骤5/5: 保存翻译后的图纸...");

            _dwgParser.SaveDocument(document, outputPath);

            // 完成（100%进度）
            progress?.Report(100);
            stats.EndTime = DateTime.Now;
            stats.Success = true;

            _logger.LogInformation(
                "翻译完成！耗时: {Duration:F2}秒, 翻译: {Translated}/{Total}条",
                stats.Duration.TotalSeconds,
                stats.TranslatedTexts,
                stats.TotalTexts
            );

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "翻译DWG图纸失败");
            stats.EndTime = DateTime.Now;
            stats.Success = false;
            stats.ErrorMessage = ex.Message;
            throw;
        }
    }

    /// <summary>
    /// 预览翻译（不保存文件，只返回翻译结果）
    /// </summary>
    /// <param name="inputPath">输入DWG文件路径</param>
    /// <param name="targetLanguage">目标语言</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>翻译映射表（原文→译文）</returns>
    public async Task<Dictionary<string, string>> PreviewTranslationAsync(
        string inputPath,
        string targetLanguage = "zh",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("预览翻译: {InputPath}", inputPath);

        try
        {
            // 加载和提取文本
            var document = _dwgParser.Parse(inputPath);
            var texts = _dwgParser.ExtractTexts(document);
            var uniqueTexts = texts.Distinct().ToList();

            _logger.LogInformation("提取了{Count}条唯一文本", uniqueTexts.Count);

            // 翻译
            var translatedTexts = await _translationEngine.TranslateBatchWithCacheAsync(
                uniqueTexts,
                targetLanguage,
                cancellationToken: cancellationToken
            );

            // 构建映射表
            var translations = new Dictionary<string, string>();
            for (int i = 0; i < uniqueTexts.Count && i < translatedTexts.Count; i++)
            {
                translations[uniqueTexts[i]] = translatedTexts[i];
            }

            _logger.LogInformation("预览翻译完成: {Count}条", translations.Count);

            return translations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览翻译失败");
            throw;
        }
    }

    /// <summary>
    /// 获取图纸中的所有文本（用于预览）
    /// </summary>
    /// <param name="filePath">DWG文件路径</param>
    /// <returns>文本列表</returns>
    public List<string> GetTextsFromDwg(string filePath)
    {
        _logger.LogInformation("提取文本: {FilePath}", filePath);

        try
        {
            var document = _dwgParser.Parse(filePath);
            var texts = _dwgParser.ExtractTexts(document);

            _logger.LogInformation("提取了{Count}条文本", texts.Count);

            return texts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取文本失败");
            throw;
        }
    }

    /// <summary>
    /// 按图层获取文本（用于高级预览）
    /// </summary>
    /// <param name="filePath">DWG文件路径</param>
    /// <returns>按图层分组的文本</returns>
    public Dictionary<string, List<string>> GetTextsByLayer(string filePath)
    {
        _logger.LogInformation("按图层提取文本: {FilePath}", filePath);

        try
        {
            var document = _dwgParser.Parse(filePath);
            var textsByLayer = _dwgParser.ExtractTextsByLayer(document);

            _logger.LogInformation("提取了{Count}个图层的文本", textsByLayer.Count);

            return textsByLayer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按图层提取文本失败");
            throw;
        }
    }

    /// <summary>
    /// 清空翻译缓存
    /// </summary>
    public async Task ClearCacheAsync()
    {
        _logger.LogInformation("清空翻译缓存");
        await _cacheService.ClearCacheAsync();
    }
}

/// <summary>
/// 翻译统计信息
/// </summary>
public class TranslationStatistics
{
    public string InputFile { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;

    public int EntityCount { get; set; }
    public int LayerCount { get; set; }
    public int TotalTexts { get; set; }
    public int TranslatedTexts { get; set; }
    public int ModifiedEntities { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public double SuccessRate => TotalTexts > 0 ? (double)TranslatedTexts / TotalTexts * 100 : 0;

    public override string ToString()
    {
        return $"翻译统计: {TranslatedTexts}/{TotalTexts}条文本 ({SuccessRate:F1}%), " +
               $"修改{ModifiedEntities}个实体, 耗时{Duration.TotalSeconds:F2}秒";
    }
}
