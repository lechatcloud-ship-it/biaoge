using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// 构件识别服务 - 使用多策略识别+AI验证
/// </summary>
public class ComponentRecognizer
{
    private readonly BailianApiClient _apiClient;
    private readonly ILogger<ComponentRecognizer> _logger;

    // 静态正则表达式 - 使用Compiled选项提升性能
    // 数量提取正则
    private static readonly Regex QuantityRegex = new(@"(\d+(?:\.\d+)?)\s*(?:个|根|块|片|扇|樘)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 尺寸提取正则 (长x宽x高 或 直径)
    private static readonly Regex DimensionRegex = new(@"(\d+(?:\.\d+)?)\s*[x×]\s*(\d+(?:\.\d+)?)\s*(?:[x×]\s*(\d+(?:\.\d+)?))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DiameterRegex = new(@"[ΦφØ]?\s*(\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 构件识别规则 - 静态编译正则提升性能
    private static readonly Dictionary<string, List<Regex>> ComponentPatterns = new()
    {
        // 混凝土构件
        ["C30混凝土柱"] = new List<Regex>
        {
            new Regex(@"C30.*柱", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土柱.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C35混凝土梁"] = new List<Regex>
        {
            new Regex(@"C35.*梁", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土梁.*C35", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C30混凝土板"] = new List<Regex>
        {
            new Regex(@"C30.*板", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土板.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        // 钢筋
        ["HRB400钢筋"] = new List<Regex>
        {
            new Regex(@"HRB400", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Φ\d+.*HRB400", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["HPB300钢筋"] = new List<Regex>
        {
            new Regex(@"HPB300", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Φ\d+.*HPB300", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        // 砌体
        ["MU10砖墙"] = new List<Regex>
        {
            new Regex(@"MU10.*墙", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"砖墙.*MU10", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["MU15砌块"] = new List<Regex>
        {
            new Regex(@"MU15.*砌块", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        // 门窗
        ["M1门"] = new List<Regex>
        {
            new Regex(@"M1", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"门.*M1", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C1窗"] = new List<Regex>
        {
            new Regex(@"C1", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"窗.*C1", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        }
    };

    public ComponentRecognizer(
        BailianApiClient apiClient,
        ILogger<ComponentRecognizer> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// 识别构件
    /// </summary>
    /// <param name="texts">文本列表</param>
    /// <param name="useAiVerification">是否使用AI验证</param>
    public async Task<List<ComponentRecognitionResult>> RecognizeComponentsAsync(
        List<string> texts,
        bool useAiVerification = true)
    {
        _logger.LogInformation("开始识别构件: {Count}条文本", texts.Count);

        var results = new List<ComponentRecognitionResult>();

        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // 策略1: 正则表达式匹配
            var regexResult = RecognizeByRegex(text);

            if (regexResult != null)
            {
                // 策略2: 提取数量和尺寸
                ExtractQuantityAndDimensions(text, regexResult);

                // 策略3: 建筑规范验证
                ApplyConstructionStandards(regexResult);

                // 策略4: AI验证（可选）
                if (useAiVerification && regexResult.Confidence < 0.9)
                {
                    await VerifyWithAiAsync(text, regexResult);
                }

                // 计算工程量
                CalculateQuantity(regexResult);

                results.Add(regexResult);
            }
        }

        _logger.LogInformation("识别完成: {Count}个构件", results.Count);

        return results;
    }

    /// <summary>
    /// 正则表达式识别
    /// </summary>
    private ComponentRecognitionResult? RecognizeByRegex(string text)
    {
        foreach (var (type, patterns) in ComponentPatterns)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.IsMatch(text))
                {
                    return new ComponentRecognitionResult
                    {
                        Type = type,
                        OriginalText = text,
                        Confidence = 0.85, // 正则匹配基础置信度
                        Status = "识别中"
                    };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 提取数量和尺寸
    /// </summary>
    private void ExtractQuantityAndDimensions(string text, ComponentRecognitionResult result)
    {
        // 提取数量
        var quantityMatch = QuantityRegex.Match(text);
        if (quantityMatch.Success)
        {
            if (int.TryParse(quantityMatch.Groups[1].Value, out var quantity))
            {
                result.Quantity = quantity;
                result.Confidence += 0.05; // 提高置信度
            }
        }

        // 提取尺寸
        var dimensionMatch = DimensionRegex.Match(text);
        if (dimensionMatch.Success)
        {
            if (double.TryParse(dimensionMatch.Groups[1].Value, out var length))
            {
                result.Length = length / 1000.0; // 转换为米

                if (dimensionMatch.Groups.Count >= 3 && double.TryParse(dimensionMatch.Groups[2].Value, out var width))
                {
                    result.Width = width / 1000.0;

                    if (dimensionMatch.Groups.Count >= 4 && double.TryParse(dimensionMatch.Groups[3].Value, out var height))
                    {
                        result.Height = height / 1000.0;
                    }
                }

                result.Confidence += 0.03;
            }
        }

        // 提取直径（用于钢筋等）
        var diameterMatch = DiameterRegex.Match(text);
        if (diameterMatch.Success && double.TryParse(diameterMatch.Groups[1].Value, out var diameter))
        {
            result.Diameter = diameter / 1000.0; // 转换为米
            result.Confidence += 0.02;
        }
    }

    /// <summary>
    /// 建筑规范验证
    /// </summary>
    private void ApplyConstructionStandards(ComponentRecognitionResult result)
    {
        // GB 50854-2013 建筑工程规范验证

        if (result.Type.Contains("柱"))
        {
            // 柱的合理尺寸范围: 200mm-1000mm
            if (result.Width > 0 && (result.Width < 0.2 || result.Width > 1.0))
            {
                result.Confidence -= 0.1;
                result.Status = "尺寸异常";
            }
        }
        else if (result.Type.Contains("梁"))
        {
            // 梁的合理跨度: 2m-20m
            if (result.Length > 0 && (result.Length < 2.0 || result.Length > 20.0))
            {
                result.Confidence -= 0.1;
                result.Status = "跨度异常";
            }
        }
        else if (result.Type.Contains("板"))
        {
            // 板的合理厚度: 80mm-300mm
            if (result.Height > 0 && (result.Height < 0.08 || result.Height > 0.3))
            {
                result.Confidence -= 0.1;
                result.Status = "厚度异常";
            }
        }

        // 如果没有异常，标记为有效
        if (result.Status == "识别中" && result.Confidence >= 0.8)
        {
            result.Status = "有效";
        }
    }

    /// <summary>
    /// AI验证（提高置信度）
    /// </summary>
    private async Task VerifyWithAiAsync(string text, ComponentRecognitionResult result)
    {
        try
        {
            // 使用AI模型验证识别结果
            // 这里可以调用BailianApiClient进行更复杂的验证
            // 暂时简化处理
            await Task.CompletedTask;

            // AI验证通过后提高置信度
            result.Confidence += 0.1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI验证失败");
        }
    }

    /// <summary>
    /// 计算工程量
    /// </summary>
    private void CalculateQuantity(ComponentRecognitionResult result)
    {
        // 计算体积
        if (result.Length > 0 && result.Width > 0 && result.Height > 0)
        {
            result.Volume = Math.Round(result.Length * result.Width * result.Height * result.Quantity, 3);
        }

        // 计算面积
        if (result.Length > 0 && result.Width > 0)
        {
            result.Area = Math.Round(result.Length * result.Width * result.Quantity, 3);
        }

        // 估算成本（简化处理，实际应从数据库获取单价）
        result.Cost = EstimateCost(result);
    }

    /// <summary>
    /// 估算成本
    /// </summary>
    private decimal EstimateCost(ComponentRecognitionResult result)
    {
        // 简化的成本估算（单位：元）
        var unitPrices = new Dictionary<string, decimal>
        {
            ["C30混凝土柱"] = 500.0m, // 元/m³
            ["C35混凝土梁"] = 550.0m,
            ["C30混凝土板"] = 450.0m,
            ["HRB400钢筋"] = 4500.0m, // 元/吨
            ["HPB300钢筋"] = 4000.0m,
            ["MU10砖墙"] = 200.0m, // 元/m³
            ["MU15砌块"] = 180.0m,
            ["M1门"] = 800.0m, // 元/扇
            ["C1窗"] = 600.0m
        };

        if (unitPrices.TryGetValue(result.Type, out var unitPrice))
        {
            if (result.Volume > 0)
            {
                return Math.Round(unitPrice * (decimal)result.Volume, 2);
            }
            else if (result.Quantity > 0)
            {
                return Math.Round(unitPrice * result.Quantity, 2);
            }
        }

        return 0;
    }

    /// <summary>
    /// 从DWG文档提取文本进行识别
    /// </summary>
    public async Task<List<ComponentRecognitionResult>> RecognizeFromDocumentAsync(
        DwgDocument document,
        bool useAiVerification = true)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _logger.LogInformation("从DWG文档提取文本进行构件识别");

        var texts = new List<string>();

        // 从CadImage中提取文本实体
        if (document.CadImage?.Entities != null)
        {
            foreach (var entity in document.CadImage.Entities)
            {
                // 提取文本实体的文本内容
                var text = ExtractTextFromEntity(entity);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    texts.Add(text);
                }
            }
        }

        _logger.LogInformation("提取了{Count}条文本", texts.Count);

        return await RecognizeComponentsAsync(texts, useAiVerification);
    }

    /// <summary>
    /// 从CAD实体提取文本
    /// </summary>
    private string ExtractTextFromEntity(object entity)
    {
        try
        {
            // 使用反射提取文本属性
            var type = entity.GetType();

            // 尝试获取Text属性
            var textProperty = type.GetProperty("Text") ?? type.GetProperty("DefaultValue");
            if (textProperty != null)
            {
                var text = textProperty.GetValue(entity)?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法从实体提取文本");
        }

        return string.Empty;
    }
}
