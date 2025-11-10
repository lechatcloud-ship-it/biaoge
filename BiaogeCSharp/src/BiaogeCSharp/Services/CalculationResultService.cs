using BiaogeCSharp.Models;
using System.Collections.Generic;
using System.Linq;

namespace BiaogeCSharp.Services;

/// <summary>
/// 算量结果服务 - 在ViewModels之间共享计算结果
/// </summary>
public class CalculationResultService
{
    private List<ComponentRecognitionResult> _latestResults = new();

    /// <summary>
    /// 获取最新的计算结果
    /// </summary>
    public IReadOnlyList<ComponentRecognitionResult> LatestResults => _latestResults.AsReadOnly();

    /// <summary>
    /// 是否有计算结果
    /// </summary>
    public bool HasResults => _latestResults.Any();

    /// <summary>
    /// 更新计算结果
    /// </summary>
    public void UpdateResults(IEnumerable<ComponentRecognitionResult> results)
    {
        _latestResults = new List<ComponentRecognitionResult>(results);
    }

    /// <summary>
    /// 清空计算结果
    /// </summary>
    public void ClearResults()
    {
        _latestResults.Clear();
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public CalculationStatistics GetStatistics()
    {
        return new CalculationStatistics
        {
            TotalComponents = _latestResults.Count,
            ValidComponents = _latestResults.Count(r => r.Status == "有效"),
            TotalQuantity = _latestResults.Sum(r => r.Quantity),
            TotalCost = _latestResults.Sum(r => r.Cost),
            AverageConfidence = _latestResults.Any() ? _latestResults.Average(r => r.Confidence) : 0
        };
    }

    /// <summary>
    /// 按类型分组
    /// </summary>
    public Dictionary<string, List<ComponentRecognitionResult>> GroupByType()
    {
        return _latestResults
            .GroupBy(r => r.ComponentType)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 获取材料汇总
    /// </summary>
    public Dictionary<string, decimal> GetMaterialSummary()
    {
        var summary = new Dictionary<string, decimal>();

        foreach (var result in _latestResults)
        {
            if (result.Materials != null)
            {
                foreach (var material in result.Materials)
                {
                    if (summary.ContainsKey(material.Key))
                    {
                        summary[material.Key] += material.Value;
                    }
                    else
                    {
                        summary[material.Key] = material.Value;
                    }
                }
            }
        }

        return summary;
    }
}

/// <summary>
/// 计算统计信息
/// </summary>
public class CalculationStatistics
{
    public int TotalComponents { get; set; }
    public int ValidComponents { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public double AverageConfidence { get; set; }
}
