using System.Collections.Generic;

namespace BiaogeCSharp.Models;

/// <summary>
/// 构件识别结果
/// </summary>
public class ComponentRecognitionResult
{
    /// <summary>
    /// 构件类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 构件类型（别名，用于兼容）
    /// </summary>
    public string ComponentType
    {
        get => Type;
        set => Type = value;
    }

    /// <summary>
    /// 原始文本
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 规格参数
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// 单位
    /// </summary>
    public string Unit { get; set; } = "个";

    /// <summary>
    /// 单价 (¥)
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 体积 (m³)
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// 面积 (m²)
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// 长度 (m)
    /// </summary>
    public double Length { get; set; }

    /// <summary>
    /// 宽度 (m)
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 高度 (m)
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 直径 (m) - 用于钢筋等
    /// </summary>
    public double Diameter { get; set; }

    /// <summary>
    /// 费用 (¥)
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// 材料清单
    /// </summary>
    public Dictionary<string, decimal>? Materials { get; set; }

    /// <summary>
    /// 状态 (有效/警告/错误)
    /// </summary>
    public string Status { get; set; } = "有效";

    /// <summary>
    /// 置信度 (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 置信度百分比显示
    /// </summary>
    public string ConfidenceDisplay => $"{Confidence:P2}";
}
