using Aspose.CAD;
using Aspose.CAD.FileFormats.Cad;
using Aspose.CAD.ImageOptions;
using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// DWG/DXF导出服务
/// </summary>
public class DwgExporter
{
    private readonly ILogger<DwgExporter> _logger;

    public DwgExporter(ILogger<DwgExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 导出为DWG/DXF格式
    /// </summary>
    /// <param name="document">DWG文档</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="format">输出格式（dwg或dxf）</param>
    /// <param name="version">DWG版本（R2010, R2013, R2018, R2024等）</param>
    public async Task ExportAsync(
        DwgDocument document,
        string outputPath,
        string format = "dwg",
        string version = "R2018")
    {
        _logger.LogInformation("开始导出: {Format} {Version}", format, version);

        try
        {
            await Task.Run(() =>
            {
                // 确保输出目录存在
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 配置导出选项
                var cadImage = document.CadImage;

                if (format.ToLower() == "dwg")
                {
                    // 导出为DWG
                    var options = new CadRasterizationOptions
                    {
                        PageWidth = cadImage.Width,
                        PageHeight = cadImage.Height,
                        DrawType = CadDrawTypeMode.UseObjectColor,
                        Layouts = new[] { "Model" }
                    };

                    var dwgOptions = new DwgOptions
                    {
                        VectorRasterizationOptions = options
                    };

                    cadImage.Save(outputPath, dwgOptions);
                }
                else
                {
                    // 导出为DXF
                    var options = new CadRasterizationOptions
                    {
                        PageWidth = cadImage.Width,
                        PageHeight = cadImage.Height,
                        DrawType = CadDrawTypeMode.UseObjectColor,
                        Layouts = new[] { "Model" }
                    };

                    var dxfOptions = new DxfOptions
                    {
                        VectorRasterizationOptions = options
                    };

                    cadImage.Save(outputPath, dxfOptions);
                }

                _logger.LogInformation("导出完成: {OutputPath}", outputPath);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出失败");
            throw new Exception($"导出失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 导出为DWG（简化方法）
    /// </summary>
    public Task ExportDwgAsync(DwgDocument document, string outputPath, string version = "R2018")
    {
        return ExportAsync(document, outputPath, "dwg", version);
    }

    /// <summary>
    /// 导出为DXF（简化方法）
    /// </summary>
    public Task ExportDxfAsync(DwgDocument document, string outputPath, string version = "R2018")
    {
        return ExportAsync(document, outputPath, "dxf", version);
    }
}
