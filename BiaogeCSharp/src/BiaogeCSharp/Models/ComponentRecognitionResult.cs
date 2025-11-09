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
    /// 数量
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 体积 (m³)
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// 面积 (m²)
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// 费用 (¥)
    /// </summary>
    public decimal Cost { get; set; }

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
