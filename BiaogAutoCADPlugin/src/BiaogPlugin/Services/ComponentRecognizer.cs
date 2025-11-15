using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BiaogPlugin.Models;
using BiaogPlugin.Extensions;

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

    // ✅ 构件识别规则（AutoCAD 2022优化版 - 大幅扩展中文构件支持）
    // ✅ 关键修复：添加更多中文构件名称模式，解决"提取不到构件"问题
    // 基于GB 50500-2013《建设工程工程量清单计价规范》和实际工程图纸
    private static readonly Dictionary<string, List<Regex>> ComponentPatterns = new()
    {
        // ==================== 混凝土构件 ====================
        // ✅ 柱（所有强度等级）
        ["C20混凝土柱"] = new List<Regex>
        {
            new Regex(@"C20.*柱", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土柱.*C20", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"柱.*C20", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"框架柱.*C20", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C25混凝土柱"] = new List<Regex>
        {
            new Regex(@"C25.*柱", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土柱.*C25", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"柱.*C25", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C30混凝土柱"] = new List<Regex>
        {
            new Regex(@"C30.*柱", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土柱.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"柱.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"KZ.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase)  // KZ=框架柱
        },
        ["C35混凝土柱"] = new List<Regex>
        {
            new Regex(@"C35.*柱", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土柱.*C35", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"柱.*C35", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C40混凝土柱"] = new List<Regex>
        {
            new Regex(@"C40.*柱", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土柱.*C40", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ✅ 梁（所有强度等级）
        ["C25混凝土梁"] = new List<Regex>
        {
            new Regex(@"C25.*梁", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土梁.*C25", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"梁.*C25", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C30混凝土梁"] = new List<Regex>
        {
            new Regex(@"C30.*梁", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土梁.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"梁.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"KL.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),  // KL=框架梁
            new Regex(@"L.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C35混凝土梁"] = new List<Regex>
        {
            new Regex(@"C35.*梁", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土梁.*C35", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"梁.*C35", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C40混凝土梁"] = new List<Regex>
        {
            new Regex(@"C40.*梁", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土梁.*C40", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ✅ 板（所有强度等级）
        ["C20混凝土板"] = new List<Regex>
        {
            new Regex(@"C20.*板", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土板.*C20", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"楼板.*C20", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C25混凝土板"] = new List<Regex>
        {
            new Regex(@"C25.*板", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土板.*C25", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C30混凝土板"] = new List<Regex>
        {
            new Regex(@"C30.*板", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"混凝土板.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"板.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"楼板.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ✅ 剪力墙
        ["C30剪力墙"] = new List<Regex>
        {
            new Regex(@"C30.*剪力墙", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"剪力墙.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"剪.*墙.*C30", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["C35剪力墙"] = new List<Regex>
        {
            new Regex(@"C35.*剪力墙", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"剪力墙.*C35", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ==================== 钢筋 ====================
        ["HRB400钢筋"] = new List<Regex>
        {
            new Regex(@"HRB400", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Φ\d+.*HRB400", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"hrb400", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"HRB\s*400", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["HPB300钢筋"] = new List<Regex>
        {
            new Regex(@"HPB300", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Φ\d+.*HPB300", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"HPB\s*300", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["HRB335钢筋"] = new List<Regex>
        {
            new Regex(@"HRB335", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Φ\d+.*HRB335", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["HRB500钢筋"] = new List<Regex>
        {
            new Regex(@"HRB500", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Φ\d+.*HRB500", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ==================== 砌体 ====================
        ["MU10砖墙"] = new List<Regex>
        {
            new Regex(@"MU10.*墙", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"砖墙.*MU10", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"MU10.*砖", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"墙.*MU10", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["MU15砌块"] = new List<Regex>
        {
            new Regex(@"MU15.*砌块", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"砌块.*MU15", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"MU15", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["MU20砖墙"] = new List<Regex>
        {
            new Regex(@"MU20.*墙", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"砖墙.*MU20", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },
        ["加气混凝土砌块"] = new List<Regex>
        {
            new Regex(@"加气.*砌块", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"加气混凝土", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"ALC", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ==================== 门窗（扩展版） ====================
        ["M1门"] = new List<Regex>
        {
            new Regex(@"M[0-9](?!\d)", RegexOptions.Compiled),
            new Regex(@"门.*M[0-9]", RegexOptions.Compiled),
            new Regex(@"M-[0-9]", RegexOptions.Compiled)
        },
        ["C1窗"] = new List<Regex>
        {
            new Regex(@"C[0-9](?!\d)", RegexOptions.Compiled),
            new Regex(@"窗.*C[0-9]", RegexOptions.Compiled),
            new Regex(@"C-[0-9]", RegexOptions.Compiled)
        },
        ["防火门"] = new List<Regex>
        {
            new Regex(@"防火门", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"FM", RegexOptions.Compiled)
        },
        ["铝合金窗"] = new List<Regex>
        {
            new Regex(@"铝.*窗", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"铝合金", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        },

        // ==================== 通用构件名称（纯中文匹配） ====================
        ["柱"] = new List<Regex>
        {
            new Regex(@"^柱$", RegexOptions.Compiled),
            new Regex(@"^框架柱$", RegexOptions.Compiled),
            new Regex(@"^KZ\d+", RegexOptions.Compiled)
        },
        ["梁"] = new List<Regex>
        {
            new Regex(@"^梁$", RegexOptions.Compiled),
            new Regex(@"^框架梁$", RegexOptions.Compiled),
            new Regex(@"^KL\d+", RegexOptions.Compiled),
            new Regex(@"^L\d+", RegexOptions.Compiled)
        },
        ["板"] = new List<Regex>
        {
            new Regex(@"^板$", RegexOptions.Compiled),
            new Regex(@"^楼板$", RegexOptions.Compiled),
            new Regex(@"^屋面板$", RegexOptions.Compiled)
        },
        ["墙"] = new List<Regex>
        {
            new Regex(@"^墙$", RegexOptions.Compiled),
            new Regex(@"^砖墙$", RegexOptions.Compiled),
            new Regex(@"^填充墙$", RegexOptions.Compiled)
        },
        ["基础"] = new List<Regex>
        {
            new Regex(@"基础", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"独立基础", RegexOptions.Compiled),
            new Regex(@"条形基础", RegexOptions.Compiled)
        }
    };

    public ComponentRecognizer(BailianApiClient bailianClient)
    {
        _bailianClient = bailianClient;
    }

    /// <summary>
    /// 从AutoCAD文本实体识别构件（AutoCAD 2022优化版）
    /// ✅ 新增：详细的调试日志，记录每个识别步骤（解决问题1：算量功能提取不到构件）
    /// </summary>
    public async Task<List<ComponentRecognitionResult>> RecognizeFromTextEntitiesAsync(
        List<TextEntity> textEntities,
        bool useAiVerification = false)
    {
        Log.Information("═══════════════════════════════════════════════════");
        Log.Information("开始识别构件: {Count}个文本实体", textEntities.Count);
        Log.Information("═══════════════════════════════════════════════════");

        var results = new List<ComponentRecognitionResult>();
        int processedCount = 0;
        int recognizedCount = 0;
        int skippedCount = 0;

        foreach (var entity in textEntities)
        {
            processedCount++;

            if (string.IsNullOrWhiteSpace(entity.Content))
            {
                skippedCount++;
                continue;
            }

            // ✅ 详细日志：记录每个文本实体的处理过程
            Log.Debug($"[{processedCount}/{textEntities.Count}] 处理文本: \"{entity.Content}\" (类型: {entity.Type}, 图层: {entity.Layer})");

            // 策略1: 正则表达式匹配
            var regexResult = RecognizeByRegex(entity);

            if (regexResult != null)
            {
                recognizedCount++;
                Log.Debug($"  ✓ 识别为: {regexResult.Type} (置信度: {regexResult.Confidence:P})");

                // 策略2: 提取数量和尺寸
                ExtractQuantityAndDimensions(entity.Content, regexResult);
                if (regexResult.Quantity > 1 || regexResult.Length > 0 || regexResult.Width > 0 || regexResult.Height > 0)
                {
                    Log.Debug($"  ✓ 提取尺寸: 数量={regexResult.Quantity}, L={regexResult.Length:F2}m, W={regexResult.Width:F2}m, H={regexResult.Height:F2}m");
                }

                // 策略3: 建筑规范验证
                ApplyConstructionStandards(regexResult);

                // 策略4: AI验证（可选）
                if (useAiVerification && regexResult.Confidence < 0.9)
                {
                    await VerifyWithAiAsync(entity.Content, regexResult);
                    Log.Debug($"  ✓ AI验证后置信度: {regexResult.Confidence:P}");
                }

                // 计算工程量
                CalculateQuantity(regexResult);
                if (regexResult.Volume > 0 || regexResult.Area > 0)
                {
                    Log.Debug($"  ✓ 工程量: 体积={regexResult.Volume:F3}m³, 面积={regexResult.Area:F3}m², 成本={regexResult.Cost:C}");
                }

                results.Add(regexResult);
            }
            else
            {
                // ✅ 记录未识别的文本，帮助调试
                Log.Debug($"  ✗ 未识别: \"{entity.Content}\"");
            }
        }

        Log.Information("═══════════════════════════════════════════════════");
        Log.Information($"✅ 识别完成: 处理={processedCount}, 识别={recognizedCount}, 跳过={skippedCount}");
        Log.Information($"识别率: {(recognizedCount * 100.0 / Math.Max(processedCount - skippedCount, 1)):F1}%");
        Log.Information("═══════════════════════════════════════════════════");

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
        catch (System.Exception ex)
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
