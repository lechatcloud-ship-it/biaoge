using Aspose.CAD;
using Aspose.CAD.ImageOptions;
using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// PDF导出服务
/// </summary>
public class PdfExporter
{
    private readonly ILogger<PdfExporter> _logger;

    public PdfExporter(ILogger<PdfExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 导出为PDF
    /// </summary>
    /// <param name="document">DWG文档</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="pageSize">页面大小（A0, A1, A2, A3, A4等）</param>
    /// <param name="dpi">分辨率（72, 150, 300等）</param>
    /// <param name="embedFonts">是否嵌入字体</param>
    public async Task ExportAsync(
        DwgDocument document,
        string outputPath,
        string pageSize = "A3",
        int dpi = 150,
        bool embedFonts = true)
    {
        _logger.LogInformation("开始导出PDF: {PageSize}, {DPI} DPI", pageSize, dpi);

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

                var cadImage = document.CadImage;

                // 获取页面尺寸
                var (width, height) = GetPageSize(pageSize);

                // 配置光栅化选项
                var rasterizationOptions = new CadRasterizationOptions
                {
                    PageWidth = width,
                    PageHeight = height,
                    DrawType = CadDrawTypeMode.UseObjectColor,
                    ScaleMethod = ScaleType.ShrinkToFit,
                    Layouts = new[] { "Model" },
                    BackgroundColor = Color.White,
                    DrawColor = Color.Black
                };

                // 配置PDF选项
                var pdfOptions = new PdfOptions
                {
                    VectorRasterizationOptions = rasterizationOptions
                };

                // 导出
                cadImage.Save(outputPath, pdfOptions);

                _logger.LogInformation("PDF导出完成: {OutputPath}", outputPath);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF导出失败");
            throw new Exception($"PDF导出失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取页面尺寸（像素）
    /// </summary>
    private (int width, int height) GetPageSize(string pageSize)
    {
        // 基于150 DPI的标准纸张尺寸
        return pageSize.ToUpper() switch
        {
            "A0" => (4967, 7022),   // 841 × 1189 mm
            "A1" => (3508, 4967),   // 594 × 841 mm
            "A2" => (2480, 3508),   // 420 × 594 mm
            "A3" => (1754, 2480),   // 297 × 420 mm
            "A4" => (1240, 1754),   // 210 × 297 mm
            _ => (1754, 2480)       // 默认 A3
        };
    }
}
