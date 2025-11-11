using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiaogPlugin.Services;

/// <summary>
/// 工程量计算器 - 汇总和统计构件识别结果
/// </summary>
public class QuantityCalculator
{
    /// <summary>
    /// 计算工程量汇总
    /// </summary>
    public QuantitySummary CalculateSummary(List<ComponentRecognitionResult> components)
    {
        Log.Information("开始计算工程量汇总，构件数量: {Count}", components.Count);

        var summary = new QuantitySummary
        {
            TotalComponents = components.Count,
            ComponentsByType = GroupByType(components),
            TotalVolume = components.Sum(c => c.Volume),
            TotalArea = components.Sum(c => c.Area),
            TotalCost = components.Sum(c => c.Cost),
            MaterialSummary = CalculateMaterialSummary(components)
        };

        // 计算统计信息
        summary.AverageConfidence = components.Count > 0
            ? components.Average(c => c.Confidence)
            : 0;

        summary.ValidCount = components.Count(c => c.Status == "有效");
        summary.AbnormalCount = components.Count(c => c.Status.Contains("异常"));

        Log.Information("工程量汇总完成: 总数{Total}, 有效{Valid}, 异常{Abnormal}",
            summary.TotalComponents, summary.ValidCount, summary.AbnormalCount);

        return summary;
    }

    /// <summary>
    /// 按类型分组统计
    /// </summary>
    private Dictionary<string, ComponentTypeStats> GroupByType(List<ComponentRecognitionResult> components)
    {
        return components
            .GroupBy(c => c.Type)
            .ToDictionary(
                g => g.Key,
                g => new ComponentTypeStats
                {
                    Count = g.Count(),
                    TotalQuantity = g.Sum(c => c.Quantity),
                    TotalVolume = Math.Round(g.Sum(c => c.Volume), 3),
                    TotalArea = Math.Round(g.Sum(c => c.Area), 3),
                    TotalCost = Math.Round(g.Sum(c => c.Cost), 2),
                    AverageConfidence = g.Average(c => c.Confidence)
                }
            );
    }

    /// <summary>
    /// 计算材料汇总
    /// </summary>
    private List<MaterialSummaryItem> CalculateMaterialSummary(List<ComponentRecognitionResult> components)
    {
        var materials = new List<MaterialSummaryItem>();

        // 混凝土汇总
        var concreteComponents = components.Where(c => c.Type.Contains("混凝土")).ToList();
        if (concreteComponents.Any())
        {
            materials.Add(new MaterialSummaryItem
            {
                MaterialType = "混凝土",
                TotalVolume = Math.Round(concreteComponents.Sum(c => c.Volume), 3),
                Unit = "m³",
                EstimatedCost = concreteComponents.Sum(c => c.Cost),
                Specifications = concreteComponents
                    .GroupBy(c => c.Type)
                    .Select(g => $"{g.Key}: {g.Sum(c => c.Volume):F2}m³")
                    .ToList()
            });
        }

        // 钢筋汇总
        var steelComponents = components.Where(c => c.Type.Contains("钢筋")).ToList();
        if (steelComponents.Any())
        {
            materials.Add(new MaterialSummaryItem
            {
                MaterialType = "钢筋",
                TotalVolume = Math.Round(steelComponents.Sum(c => c.Volume) * 7850, 3), // 钢材密度7850kg/m³
                Unit = "kg",
                EstimatedCost = steelComponents.Sum(c => c.Cost),
                Specifications = steelComponents
                    .GroupBy(c => c.Type)
                    .Select(g => $"{g.Key}: {g.Count()}根")
                    .ToList()
            });
        }

        // 砌体汇总
        var masonryComponents = components.Where(c => c.Type.Contains("砖") || c.Type.Contains("砌块")).ToList();
        if (masonryComponents.Any())
        {
            materials.Add(new MaterialSummaryItem
            {
                MaterialType = "砌体",
                TotalVolume = Math.Round(masonryComponents.Sum(c => c.Volume), 3),
                Unit = "m³",
                EstimatedCost = masonryComponents.Sum(c => c.Cost),
                Specifications = masonryComponents
                    .GroupBy(c => c.Type)
                    .Select(g => $"{g.Key}: {g.Sum(c => c.Volume):F2}m³")
                    .ToList()
            });
        }

        // 门窗汇总
        var doorWindowComponents = components.Where(c => c.Type.Contains("门") || c.Type.Contains("窗")).ToList();
        if (doorWindowComponents.Any())
        {
            materials.Add(new MaterialSummaryItem
            {
                MaterialType = "门窗",
                TotalVolume = doorWindowComponents.Sum(c => c.Quantity),
                Unit = "扇",
                EstimatedCost = doorWindowComponents.Sum(c => c.Cost),
                Specifications = doorWindowComponents
                    .GroupBy(c => c.Type)
                    .Select(g => $"{g.Key}: {g.Sum(c => c.Quantity)}扇")
                    .ToList()
            });
        }

        return materials;
    }

    /// <summary>
    /// 生成工程量报告
    /// </summary>
    public string GenerateReport(QuantitySummary summary)
    {
        var report = "╔═══════════════════════════════════════════════════════╗\n";
        report += "║              工程量计算报告                          ║\n";
        report += "╚═══════════════════════════════════════════════════════╝\n\n";

        // 总体统计
        report += "【总体统计】\n";
        report += $"  构件总数: {summary.TotalComponents}个\n";
        report += $"  有效构件: {summary.ValidCount}个\n";
        report += $"  异常构件: {summary.AbnormalCount}个\n";
        report += $"  平均置信度: {summary.AverageConfidence:P}\n";
        report += $"  总体积: {summary.TotalVolume:F2}m³\n";
        report += $"  总面积: {summary.TotalArea:F2}m²\n";
        report += $"  总成本: ¥{summary.TotalCost:N2}\n\n";

        // 分类统计
        if (summary.ComponentsByType.Any())
        {
            report += "【分类统计】\n";
            foreach (var (type, stats) in summary.ComponentsByType.OrderByDescending(x => x.Value.TotalCost))
            {
                report += $"\n  {type}:\n";
                report += $"    数量: {stats.Count}处 | 总数: {stats.TotalQuantity}个\n";
                if (stats.TotalVolume > 0)
                    report += $"    体积: {stats.TotalVolume:F2}m³\n";
                if (stats.TotalArea > 0)
                    report += $"    面积: {stats.TotalArea:F2}m²\n";
                report += $"    成本: ¥{stats.TotalCost:N2}\n";
                report += $"    置信度: {stats.AverageConfidence:P}\n";
            }
            report += "\n";
        }

        // 材料汇总
        if (summary.MaterialSummary.Any())
        {
            report += "【材料汇总】\n";
            foreach (var material in summary.MaterialSummary)
            {
                report += $"\n  {material.MaterialType}:\n";
                report += $"    总量: {material.TotalVolume:F2}{material.Unit}\n";
                report += $"    成本: ¥{material.EstimatedCost:N2}\n";
                if (material.Specifications.Any())
                {
                    report += "    规格明细:\n";
                    foreach (var spec in material.Specifications)
                    {
                        report += $"      - {spec}\n";
                    }
                }
            }
        }

        report += $"\n报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";

        return report;
    }
}

/// <summary>
/// 工程量汇总
/// </summary>
public class QuantitySummary
{
    public int TotalComponents { get; set; }
    public int ValidCount { get; set; }
    public int AbnormalCount { get; set; }
    public double AverageConfidence { get; set; }
    public double TotalVolume { get; set; }
    public double TotalArea { get; set; }
    public decimal TotalCost { get; set; }
    public Dictionary<string, ComponentTypeStats> ComponentsByType { get; set; } = new();
    public List<MaterialSummaryItem> MaterialSummary { get; set; } = new();
}

/// <summary>
/// 构件类型统计
/// </summary>
public class ComponentTypeStats
{
    public int Count { get; set; }
    public int TotalQuantity { get; set; }
    public double TotalVolume { get; set; }
    public double TotalArea { get; set; }
    public decimal TotalCost { get; set; }
    public double AverageConfidence { get; set; }
}

/// <summary>
/// 材料汇总项
/// </summary>
public class MaterialSummaryItem
{
    public string MaterialType { get; set; } = string.Empty;
    public double TotalVolume { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public List<string> Specifications { get; set; } = new();
}
