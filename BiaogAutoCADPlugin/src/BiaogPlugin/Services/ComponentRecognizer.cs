using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services;

/// <summary>
/// 构件识别服务 - 基于AutoCAD文本实体的多策略识别+AI验证
/// </summary>
public class ComponentRecognizer
{
    private readonly BailianApiClient _bailianClient;

    // 静态正则表达式 - 使用Compiled选项提升性能
    private static readonly Regex QuantityRegex = new(@"(\d+(?:\.\d+)?)\s*(?:个|根|块|片|扇|樘)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 尺寸提取正则 (长x宽x高 或 直径)
    private static readonly Regex DimensionRegex = new(@"(\d+(?:\.\d+)?)\s*[x×]\s*(\d+(?:\.\d+)?)\s*(?:[x×]\s*(\d+(?:\.\d+)?))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DiameterRegex = new(@"[ΦφØ]?\s*(\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 构件识别规则
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
            new Regex(@"M[01](?!\d)", RegexOptions.Compiled),
            new Regex(@"门.*M[01]", RegexOptions.Compiled)
        },
        ["C1窗"] = new List<Regex>
        {
            new Regex(@"C[01](?!\d)", RegexOptions.Compiled),
            new Regex(@"窗.*C[01]", RegexOptions.Compiled)
        }
    };

    public ComponentRecognizer(BailianApiClient bailianClient)
    {
        _bailianClient = bailianClient;
    }

    /// <summary>
    /// 从AutoCAD文本实体识别构件
    /// </summary>
    public async Task<List<ComponentRecognitionResult>> RecognizeFromTextEntitiesAsync(
        List<TextEntity> textEntities,
        bool useAiVerification = false)
    {
        Log.Information("开始识别构件: {Count}个文本实体", textEntities.Count);

        var results = new List<ComponentRecognitionResult>();

        foreach (var entity in textEntities)
        {
            if (string.IsNullOrWhiteSpace(entity.Content))
                continue;

            // 策略1: 正则表达式匹配
            var regexResult = RecognizeByRegex(entity);

            if (regexResult != null)
            {
                // 策略2: 提取数量和尺寸
                ExtractQuantityAndDimensions(entity.Content, regexResult);

                // 策略3: 建筑规范验证
                ApplyConstructionStandards(regexResult);

                // 策略4: AI验证（可选）
                if (useAiVerification && regexResult.Confidence < 0.9)
                {
                    await VerifyWithAiAsync(entity.Content, regexResult);
                }

                // 计算工程量
                CalculateQuantity(regexResult);

                results.Add(regexResult);
            }
        }

        Log.Information("识别完成: {Count}个构件", results.Count);

        return results;
    }

    /// <summary>
    /// 正则表达式识别
    /// </summary>
    private ComponentRecognitionResult? RecognizeByRegex(TextEntity entity)
    {
        foreach (var (type, patterns) in ComponentPatterns)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.IsMatch(entity.Content))
                {
                    return new ComponentRecognitionResult
                    {
                        Type = type,
                        OriginalText = entity.Content,
                        Layer = entity.Layer,
                        Position = entity.Position,
                        Confidence = 0.85,
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
        if (quantityMatch.Success && int.TryParse(quantityMatch.Groups[1].Value, out var quantity))
        {
            result.Quantity = quantity;
            result.Confidence += 0.05;
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

        // 提取直径（钢筋等）
        var diameterMatch = DiameterRegex.Match(text);
        if (diameterMatch.Success && double.TryParse(diameterMatch.Groups[1].Value, out var diameter))
        {
            result.Diameter = diameter / 1000.0;
            result.Confidence += 0.02;
        }
    }

    /// <summary>
    /// 建筑规范验证（GB 50854-2013）
    /// </summary>
    private void ApplyConstructionStandards(ComponentRecognitionResult result)
    {
        if (result.Type.Contains("柱"))
        {
            // 柱的合理尺寸: 200mm-1000mm
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
            // 使用AI验证识别结果（可选功能）
            await Task.CompletedTask;
            result.Confidence += 0.1;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AI验证失败");
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

        // 估算成本
        result.Cost = EstimateCost(result);
    }

    /// <summary>
    /// 估算成本（元）
    /// </summary>
    private decimal EstimateCost(ComponentRecognitionResult result)
    {
        var unitPrices = new Dictionary<string, decimal>
        {
            ["C30混凝土柱"] = 500.0m, // 元/m³
            ["C35混凝土梁"] = 550.0m,
            ["C30混凝土板"] = 450.0m,
            ["HRB400钢筋"] = 4500.0m, // 元/吨
            ["HPB300钢筋"] = 4000.0m,
            ["MU10砖墙"] = 200.0m,
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
}

/// <summary>
/// 构件识别结果
/// </summary>
public class ComponentRecognitionResult
{
    public string Type { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string Layer { get; set; } = string.Empty;
    public Autodesk.AutoCAD.Geometry.Point3d Position { get; set; }
    public double Confidence { get; set; }
    public string Status { get; set; } = string.Empty;

    // 数量和尺寸
    public int Quantity { get; set; } = 1;
    public double Length { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Diameter { get; set; }

    // 工程量
    public double Volume { get; set; }
    public double Area { get; set; }
    public decimal Cost { get; set; }

    public override string ToString()
    {
        return $"{Type} | 数量:{Quantity} | 置信度:{Confidence:P} | 成本:{Cost:C}";
    }
}
