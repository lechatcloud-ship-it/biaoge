using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// Excel导出服务（工程量清单）
/// </summary>
public class ExcelExporter
{
    private readonly ILogger<ExcelExporter> _logger;

    public ExcelExporter(ILogger<ExcelExporter> logger)
    {
        _logger = logger;

        // 设置EPPlus授权（非商业用途）
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// 导出工程量清单到Excel
    /// </summary>
    /// <param name="results">构件识别结果列表</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="includeDetails">包含构件详细信息</param>
    /// <param name="includeConfidence">包含置信度评分</param>
    /// <param name="includeMaterials">包含材料清单</param>
    /// <param name="includeCost">包含成本估算</param>
    public async Task ExportAsync(
        List<ComponentRecognitionResult> results,
        string outputPath,
        bool includeDetails = true,
        bool includeConfidence = true,
        bool includeMaterials = true,
        bool includeCost = false)
    {
        _logger.LogInformation("开始导出Excel工程量清单");

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

                using var package = new ExcelPackage();

                // 创建工作表
                var worksheet = package.Workbook.Worksheets.Add("工程量清单");

                // 设置标题行
                int col = 1;
                worksheet.Cells[1, col++].Value = "序号";
                worksheet.Cells[1, col++].Value = "构件类型";
                worksheet.Cells[1, col++].Value = "数量";

                if (includeDetails)
                {
                    worksheet.Cells[1, col++].Value = "体积 (m³)";
                    worksheet.Cells[1, col++].Value = "面积 (m²)";
                }

                if (includeCost)
                {
                    worksheet.Cells[1, col++].Value = "单价 (¥)";
                    worksheet.Cells[1, col++].Value = "费用 (¥)";
                }

                if (includeConfidence)
                {
                    worksheet.Cells[1, col++].Value = "置信度";
                }

                worksheet.Cells[1, col++].Value = "状态";

                // 设置标题行样式
                using (var range = worksheet.Cells[1, 1, 1, col - 1])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // 填充数据
                int row = 2;
                foreach (var result in results)
                {
                    col = 1;
                    worksheet.Cells[row, col++].Value = row - 1;
                    worksheet.Cells[row, col++].Value = result.Type;
                    worksheet.Cells[row, col++].Value = result.Quantity;

                    if (includeDetails)
                    {
                        worksheet.Cells[row, col++].Value = result.Volume;
                        worksheet.Cells[row, col++].Value = result.Area;
                    }

                    if (includeCost)
                    {
                        worksheet.Cells[row, col++].Value = 0; // 单价需要从数据库获取
                        worksheet.Cells[row, col++].Value = result.Cost;
                    }

                    if (includeConfidence)
                    {
                        worksheet.Cells[row, col++].Value = result.ConfidenceDisplay;
                    }

                    worksheet.Cells[row, col++].Value = result.Status;

                    row++;
                }

                // 自动调整列宽
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // 添加汇总信息
                if (includeMaterials)
                {
                    var summarySheet = package.Workbook.Worksheets.Add("材料汇总");
                    CreateMaterialSummary(summarySheet, results);
                }

                // 保存文件
                var fileInfo = new FileInfo(outputPath);
                package.SaveAs(fileInfo);

                _logger.LogInformation("Excel导出完成: {OutputPath}", outputPath);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel导出失败");
            throw new Exception($"Excel导出失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 创建材料汇总表
    /// </summary>
    private void CreateMaterialSummary(ExcelWorksheet worksheet, List<ComponentRecognitionResult> results)
    {
        // 标题
        worksheet.Cells[1, 1].Value = "材料类型";
        worksheet.Cells[1, 2].Value = "总数量";
        worksheet.Cells[1, 3].Value = "总体积 (m³)";
        worksheet.Cells[1, 4].Value = "总面积 (m²)";
        worksheet.Cells[1, 5].Value = "总费用 (¥)";

        // 设置标题样式
        using (var range = worksheet.Cells[1, 1, 1, 5])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // 按类型分组汇总
        var summary = results.GroupBy(r => r.Type)
            .Select(g => new
            {
                Type = g.Key,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalVolume = g.Sum(x => x.Volume),
                TotalArea = g.Sum(x => x.Area),
                TotalCost = g.Sum(x => x.Cost)
            })
            .ToList();

        // 填充数据
        int row = 2;
        foreach (var item in summary)
        {
            worksheet.Cells[row, 1].Value = item.Type;
            worksheet.Cells[row, 2].Value = item.TotalQuantity;
            worksheet.Cells[row, 3].Value = item.TotalVolume;
            worksheet.Cells[row, 4].Value = item.TotalArea;
            worksheet.Cells[row, 5].Value = item.TotalCost;
            row++;
        }

        // 添加总计行
        worksheet.Cells[row, 1].Value = "总计";
        worksheet.Cells[row, 2].Value = summary.Sum(x => x.TotalQuantity);
        worksheet.Cells[row, 3].Value = summary.Sum(x => x.TotalVolume);
        worksheet.Cells[row, 4].Value = summary.Sum(x => x.TotalArea);
        worksheet.Cells[row, 5].Value = summary.Sum(x => x.TotalCost);

        // 总计行样式
        using (var range = worksheet.Cells[row, 1, row, 5])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
        }

        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
    }
}
