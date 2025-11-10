using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BiaogeCSharp.Services;

/// <summary>
/// AI上下文管理器 - 管理图纸、翻译、算量的全局状态
/// </summary>
public class AIContextManager
{
    private readonly ILogger<AIContextManager> _logger;

    // 上下文数据
    private DwgDocument? _currentDocument;
    private List<ComponentRecognitionResult>? _recognitionResults;
    private Dictionary<string, string>? _translationData;
    private Dictionary<string, object> _metadata = new();

    public AIContextManager(ILogger<AIContextManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 设置当前DWG文档
    /// </summary>
    public void SetCurrentDocument(DwgDocument document)
    {
        _currentDocument = document;
        _logger.LogInformation("上下文更新: DWG文档 - {FileName}", document.FileName);
    }

    /// <summary>
    /// 设置识别结果
    /// </summary>
    public void SetRecognitionResults(List<ComponentRecognitionResult> results)
    {
        _recognitionResults = results;
        _logger.LogInformation("上下文更新: 识别结果 - {Count}个构件", results.Count);
    }

    /// <summary>
    /// 设置翻译数据
    /// </summary>
    public void SetTranslationData(Dictionary<string, string> translations)
    {
        _translationData = translations;
        _logger.LogInformation("上下文更新: 翻译数据 - {Count}条", translations.Count);
    }

    /// <summary>
    /// 设置元数据
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    /// <summary>
    /// 构建上下文信息字符串
    /// </summary>
    public string BuildContext()
    {
        var context = new StringBuilder();

        // 图纸上下文
        if (_currentDocument != null)
        {
            context.AppendLine("## 当前图纸");
            context.AppendLine($"文件名: {_currentDocument.FileName}");
            context.AppendLine($"实体数量: {_currentDocument.EntityCount}");
            context.AppendLine($"图层数量: {_currentDocument.Layers.Count}");

            if (_currentDocument.Metadata.Any())
            {
                context.AppendLine("### 元数据");
                foreach (var (key, value) in _currentDocument.Metadata)
                {
                    context.AppendLine($"- {key}: {value}");
                }
            }

            context.AppendLine();
        }

        // 识别结果上下文
        if (_recognitionResults != null && _recognitionResults.Any())
        {
            context.AppendLine("## 构件识别结果");
            context.AppendLine($"总数: {_recognitionResults.Count}个构件");

            // 按类型汇总
            var summary = _recognitionResults
                .GroupBy(r => r.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    TotalVolume = g.Sum(x => x.Volume),
                    TotalCost = g.Sum(x => x.Cost),
                    AvgConfidence = g.Average(x => x.Confidence)
                })
                .ToList();

            context.AppendLine("### 构件汇总");
            foreach (var item in summary)
            {
                context.AppendLine($"- {item.Type}: {item.Count}个, 体积{item.TotalVolume:F2}m³, 费用{item.TotalCost:F2}元, 平均置信度{item.AvgConfidence:P2}");
            }

            // 统计信息
            var validCount = _recognitionResults.Count(r => r.Status == "有效");
            var totalCost = _recognitionResults.Sum(r => r.Cost);

            context.AppendLine("### 统计");
            context.AppendLine($"- 有效构件: {validCount}/{_recognitionResults.Count}");
            context.AppendLine($"- 总费用: {totalCost:F2}元");

            context.AppendLine();
        }

        // 翻译数据上下文
        if (_translationData != null && _translationData.Any())
        {
            context.AppendLine("## 翻译数据");
            context.AppendLine($"已翻译: {_translationData.Count}条文本");
            context.AppendLine();
        }

        // 附加元数据
        if (_metadata.Any())
        {
            context.AppendLine("## 其他信息");
            foreach (var (key, value) in _metadata)
            {
                context.AppendLine($"- {key}: {value}");
            }
            context.AppendLine();
        }

        // 如果没有任何上下文
        if (context.Length == 0)
        {
            context.AppendLine("当前没有加载图纸或数据。");
        }

        return context.ToString();
    }

    /// <summary>
    /// 获取当前文档
    /// </summary>
    public DwgDocument? GetCurrentDocument() => _currentDocument;

    /// <summary>
    /// 获取识别结果
    /// </summary>
    public List<ComponentRecognitionResult>? GetRecognitionResults() => _recognitionResults;

    /// <summary>
    /// 获取翻译数据
    /// </summary>
    public Dictionary<string, string>? GetTranslationData() => _translationData;

    /// <summary>
    /// 清空上下文
    /// </summary>
    public void Clear()
    {
        _currentDocument = null;
        _recognitionResults = null;
        _translationData = null;
        _metadata.Clear();
        _logger.LogInformation("上下文已清空");
    }
}
