using Aspose.CAD;
using Aspose.CAD.FileFormats.Cad;
using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace BiaogeCSharp.Services;

/// <summary>
/// Aspose.CAD DWG解析器
/// </summary>
public class AsposeDwgParser
{
    private readonly ILogger<AsposeDwgParser> _logger;

    public AsposeDwgParser(ILogger<AsposeDwgParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析DWG文件
    /// </summary>
    public DwgDocument Parse(string filePath)
    {
        _logger.LogInformation("开始解析DWG文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        try
        {
            // 使用Aspose.CAD加载DWG文件
            var cadImage = (CadImage)Image.Load(filePath);

            var document = new DwgDocument
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                CadImage = cadImage
            };

            // 解析图层信息
            if (cadImage.Layers != null)
            {
                foreach (var layer in cadImage.Layers)
                {
                    document.Layers.Add(new LayerInfo
                    {
                        Name = layer.Name ?? "0",
                        IsVisible = !layer.IsOff,
                        IsLocked = layer.IsLocked
                    });
                }
            }

            // 元数据
            document.Metadata["Version"] = cadImage.DxfVersion?.ToString() ?? "Unknown";
            document.Metadata["Width"] = cadImage.Width;
            document.Metadata["Height"] = cadImage.Height;

            _logger.LogInformation(
                "DWG解析完成: {EntityCount}个实体, {LayerCount}个图层",
                document.EntityCount,
                document.Layers.Count
            );

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DWG解析失败: {FilePath}", filePath);
            throw;
        }
    }
}
