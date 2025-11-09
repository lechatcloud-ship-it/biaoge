using Aspose.CAD.FileFormats.Cad;
using System.Collections.Generic;

namespace BiaogeCSharp.Models;

/// <summary>
/// DWG文档模型
/// </summary>
public class DwgDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public CadImage CadImage { get; set; } = null!;
    public List<LayerInfo> Layers { get; set; } = new();
    public int EntityCount => CadImage?.Entities?.Count ?? 0;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 图层信息
/// </summary>
public class LayerInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public int EntityCount { get; set; }
}
