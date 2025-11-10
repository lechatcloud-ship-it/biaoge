using Aspose.CAD.FileFormats.Cad;
using System;
using System.Collections.Generic;

namespace BiaogeCSharp.Models;

/// <summary>
/// DWG文档模型 - 实现IDisposable以正确释放CadImage资源
/// </summary>
public class DwgDocument : IDisposable
{
    private bool _disposed = false;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public CadImage CadImage { get; set; } = null!;
    public List<LayerInfo> Layers { get; set; } = new();
    public int EntityCount => CadImage?.Entities?.Count ?? 0;
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 释放CadImage资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 释放托管资源
                CadImage?.Dispose();
            }

            _disposed = true;
        }
    }

    ~DwgDocument()
    {
        Dispose(false);
    }
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
