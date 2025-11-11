using OfficeOpenXml;
using OfficeOpenXml.Style;
using Serilog;
using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace BiaogPlugin.Services;

/// <summary>
/// Excel导出器 - 导出工程量清单
/// </summary>
public class ExcelExporter
{
    public ExcelExporter()
    {
        // EPPlus需要设置许可证上下文
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// 导出工程量汇总到Excel
    /// </summary>
    public void ExportSummary(QuantitySummary summary, string outputPath)
    {
        Log.Information("开始导出Excel: {Path}", outputPath);

        try
        {
            using var package = new ExcelPackage();

            // 创建工作表
            CreateSummarySheet(package, summary);
            CreateDetailSheet(package, summary);
            CreateMaterialSheet(package, summary);

            // 保存文件
            var fileInfo = new FileInfo(outputPath);
            package.SaveAs(fileInfo);

            Log.Information("Excel导出成功: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Excel导出失败");
            throw;
        }
    }

    /// <summary>
    /// 创建汇总表
    /// </summary>
    private void CreateSummarySheet(ExcelPackage package, QuantitySummary summary)
    {
        var sheet = package.Workbook.Worksheets.Add("工程量汇总");

        // 设置列宽
        sheet.Column(1).Width = 20;
        sheet.Column(2).Width = 15;
        sheet.Column(3).Width = 25;

        var row = 1;

        // 标题
        sheet.Cells[row, 1, row, 3].Merge = true;
        sheet.Cells[row, 1].Value = "工程量计算汇总表";
        sheet.Cells[row, 1].Style.Font.Size = 16;
        sheet.Cells[row, 1].Style.Font.Bold = true;
        sheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row++;

        // 生成时间
        sheet.Cells[row, 1, row, 3].Merge = true;
        sheet.Cells[row, 1].Value = $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        sheet.Cells[row, 1].Style.Font.Size = 10;
        sheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row++;
        row++; // 空行

        // 总体统计
        sheet.Cells[row, 1].Value = "总体统计";
        sheet.Cells[row, 1].Style.Font.Bold = true;
        SetCellBackground(sheet.Cells[row, 1, row, 3], Color.LightGray);
        row++;

        AddDataRow(sheet, row++, "构件总数", $"{summary.TotalComponents}个");
        AddDataRow(sheet, row++, "有效构件", $"{summary.ValidCount}个");
        AddDataRow(sheet, row++, "异常构件", $"{summary.AbnormalCount}个");
        AddDataRow(sheet, row++, "平均置信度", $"{summary.AverageConfidence:P}");
        AddDataRow(sheet, row++, "总体积", $"{summary.TotalVolume:F2}m³");
        AddDataRow(sheet, row++, "总面积", $"{summary.TotalArea:F2}m²");
        AddDataRow(sheet, row++, "总成本", $"¥{summary.TotalCost:N2}");
        row++; // 空行

        // 分类汇总表头
        sheet.Cells[row, 1].Value = "构件类型";
        sheet.Cells[row, 2].Value = "数量";
        sheet.Cells[row, 3].Value = "成本";
        SetCellBackground(sheet.Cells[row, 1, row, 3], Color.LightBlue);
        sheet.Cells[row, 1, row, 3].Style.Font.Bold = true;
        row++;

        // 分类数据
        foreach (var (type, stats) in summary.ComponentsByType.OrderByDescending(x => x.Value.TotalCost))
        {
            sheet.Cells[row, 1].Value = type;
            sheet.Cells[row, 2].Value = $"{stats.Count}处 ({stats.TotalQuantity}个)";
            sheet.Cells[row, 3].Value = $"¥{stats.TotalCost:N2}";
            row++;
        }

        // 设置边框
        var dataRange = sheet.Cells[1, 1, row - 1, 3];
        dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
    }

    /// <summary>
    /// 创建详细表
    /// </summary>
    private void CreateDetailSheet(ExcelPackage package, QuantitySummary summary)
    {
        var sheet = package.Workbook.Worksheets.Add("构件明细");

        // 表头
        sheet.Cells[1, 1].Value = "构件类型";
        sheet.Cells[1, 2].Value = "数量";
        sheet.Cells[1, 3].Value = "总数";
        sheet.Cells[1, 4].Value = "体积(m³)";
        sheet.Cells[1, 5].Value = "面积(m²)";
        sheet.Cells[1, 6].Value = "成本(元)";
        sheet.Cells[1, 7].Value = "置信度";

        SetCellBackground(sheet.Cells[1, 1, 1, 7], Color.LightBlue);
        sheet.Cells[1, 1, 1, 7].Style.Font.Bold = true;

        // 数据
        var row = 2;
        foreach (var (type, stats) in summary.ComponentsByType.OrderBy(x => x.Key))
        {
            sheet.Cells[row, 1].Value = type;
            sheet.Cells[row, 2].Value = stats.Count;
            sheet.Cells[row, 3].Value = stats.TotalQuantity;
            sheet.Cells[row, 4].Value = stats.TotalVolume;
            sheet.Cells[row, 5].Value = stats.TotalArea;
            sheet.Cells[row, 6].Value = (double)stats.TotalCost;
            sheet.Cells[row, 7].Value = stats.AverageConfidence;

            // 格式化
            sheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
            sheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            sheet.Cells[row, 6].Style.Numberformat.Format = "¥#,##0.00";
            sheet.Cells[row, 7].Style.Numberformat.Format = "0.00%";

            row++;
        }

        // 自动调整列宽
        sheet.Cells.AutoFitColumns();

        // 设置边框
        var range = sheet.Cells[1, 1, row - 1, 7];
        range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
    }

    /// <summary>
    /// 创建材料表
    /// </summary>
    private void CreateMaterialSheet(ExcelPackage package, QuantitySummary summary)
    {
        var sheet = package.Workbook.Worksheets.Add("材料汇总");

        // 标题
        sheet.Cells[1, 1, 1, 4].Merge = true;
        sheet.Cells[1, 1].Value = "材料汇总表";
        sheet.Cells[1, 1].Style.Font.Size = 14;
        sheet.Cells[1, 1].Style.Font.Bold = true;
        sheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // 表头
        sheet.Cells[2, 1].Value = "材料类型";
        sheet.Cells[2, 2].Value = "总量";
        sheet.Cells[2, 3].Value = "单位";
        sheet.Cells[2, 4].Value = "估算成本(元)";

        SetCellBackground(sheet.Cells[2, 1, 2, 4], Color.LightGreen);
        sheet.Cells[2, 1, 2, 4].Style.Font.Bold = true;

        // 数据
        var row = 3;
        foreach (var material in summary.MaterialSummary)
        {
            sheet.Cells[row, 1].Value = material.MaterialType;
            sheet.Cells[row, 2].Value = material.TotalVolume;
            sheet.Cells[row, 3].Value = material.Unit;
            sheet.Cells[row, 4].Value = (double)material.EstimatedCost;
            sheet.Cells[row, 4].Style.Numberformat.Format = "¥#,##0.00";

            row++;

            // 规格明细
            if (material.Specifications.Any())
            {
                foreach (var spec in material.Specifications)
                {
                    sheet.Cells[row, 2, row, 4].Merge = true;
                    sheet.Cells[row, 2].Value = $"  {spec}";
                    sheet.Cells[row, 2].Style.Font.Italic = true;
                    sheet.Cells[row, 2].Style.Font.Color.SetColor(Color.Gray);
                    row++;
                }
            }
        }

        // 自动调整列宽
        sheet.Cells.AutoFitColumns();

        // 设置边框
        var range = sheet.Cells[1, 1, row - 1, 4];
        range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
    }

    /// <summary>
    /// 添加数据行
    /// </summary>
    private void AddDataRow(ExcelWorksheet sheet, int row, string label, string value)
    {
        sheet.Cells[row, 1].Value = label;
        sheet.Cells[row, 2, row, 3].Merge = true;
        sheet.Cells[row, 2].Value = value;
    }

    /// <summary>
    /// 设置单元格背景色
    /// </summary>
    private void SetCellBackground(ExcelRange range, Color color)
    {
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(color);
    }
}
