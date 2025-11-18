using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Serilog;
using BiaogPlugin.Models;
using System.Drawing;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// ✅ 专业算量Excel导出器 - 符合GB 50854-2013工程量清单计价规范
    ///
    /// 用户反馈："导出来的表格简单至极，这根本就不是一个合格的算量工具"
    ///
    /// 导出内容：
    /// 1. 工作表1：分部分项工程量清单（按GB 50854-2013格式）
    /// 2. 工作表2：钢筋明细表（直径、长度、根数、重量）
    /// 3. 工作表3：材料汇总表（混凝土、钢筋、模板等）
    ///
    /// 参考专业软件：广联达、鲁班算量
    /// </summary>
    public class QuantityExcelExporter
    {
        public QuantityExcelExporter()
        {
            // EPPlus 7.x 需要设置LicenseContext
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// 导出完整的工程量报表到Excel（3个工作表）
        /// </summary>
        public void ExportToExcel(List<ComponentRecognitionResult> components, string filePath)
        {
            try
            {
                Log.Information($"开始导出Excel报表: {filePath}");
                Log.Information($"构件数量: {components.Count}");

                using (var package = new ExcelPackage())
                {
                    // 工作表1：分部分项工程量清单
                    CreateQuantityListSheet(package, components);

                    // 工作表2：钢筋明细表
                    CreateSteelDetailSheet(package, components);

                    // 工作表3：材料汇总表
                    CreateMaterialSummarySheet(package, components);

                    // 保存文件
                    FileInfo file = new FileInfo(filePath);
                    package.SaveAs(file);

                    Log.Information($"✅ Excel报表导出成功: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导出Excel报表失败");
                throw;
            }
        }

        /// <summary>
        /// 工作表1：分部分项工程量清单（按GB 50854-2013格式）
        /// </summary>
        private void CreateQuantityListSheet(ExcelPackage package, List<ComponentRecognitionResult> components)
        {
            var worksheet = package.Workbook.Worksheets.Add("工程量清单");

            // ===== 标题行 =====
            worksheet.Cells["A1"].Value = "分部分项工程量清单（GB 50854-2013）";
            worksheet.Cells["A1:M1"].Merge = true;
            worksheet.Cells["A1"].Style.Font.Size = 16;
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Row(1).Height = 30;

            // ===== 表头行 =====
            int row = 3;
            // ✅ GB 50854-2013标准表头：项目编码、项目名称、计量单位、工程量（5要素）
            var headers = new[] { "序号", "项目编码", "项目名称", "计量单位", "工程量", "长(m)", "宽(m)", "高(m)", "模板(m²)", "图层", "数量", "置信度" };
            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = worksheet.Cells[row, col];
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // ===== 数据行 =====
            row++;
            int index = 1;

            // 按构件类型分组
            var groupedComponents = components
                .GroupBy(c => c.Type)
                .OrderBy(g => g.Key);

            foreach (var group in groupedComponents)
            {
                // 小计行标题
                var subtitleCell = worksheet.Cells[row, 1, row, headers.Length];
                subtitleCell.Merge = true;
                subtitleCell.Value = $"【{group.Key}】";
                subtitleCell.Style.Font.Bold = true;
                subtitleCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                subtitleCell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                row++;

                double subtotalArea = 0;
                double subtotalVolume = 0;
                double subtotalFormwork = 0;

                foreach (var component in group)
                {
                    // ✅ GB 50854-2013五要素
                    string projectCode = GBProjectCodeGenerator.GetProjectCode(component.Type);
                    string measurementUnit = GBProjectCodeGenerator.GetMeasurementUnit(component.Type);
                    double quantity = measurementUnit == "m³" ? component.Volume : component.Area;

                    worksheet.Cells[row, 1].Value = index++;                                    // 序号
                    worksheet.Cells[row, 2].Value = projectCode;                                // 项目编码
                    worksheet.Cells[row, 3].Value = component.Type;                             // 项目名称
                    worksheet.Cells[row, 4].Value = measurementUnit;                            // 计量单位
                    worksheet.Cells[row, 5].Value = quantity > 0 ? quantity : (double?)null;   // 工程量
                    worksheet.Cells[row, 6].Value = component.Length > 0 ? component.Length : (double?)null;
                    worksheet.Cells[row, 7].Value = component.Width > 0 ? component.Width : (double?)null;
                    worksheet.Cells[row, 8].Value = component.Height > 0 ? component.Height : (double?)null;
                    worksheet.Cells[row, 9].Value = component.FormworkArea > 0 ? component.FormworkArea : (double?)null;
                    worksheet.Cells[row, 10].Value = component.Layer;                           // 图层
                    worksheet.Cells[row, 11].Value = component.Quantity;                        // 数量
                    worksheet.Cells[row, 12].Value = $"{component.Confidence:P0}";             // 置信度

                    // 小计累加
                    subtotalArea += component.Area;
                    subtotalVolume += component.Volume;
                    subtotalFormwork += component.FormworkArea;

                    // 设置数字格式
                    worksheet.Cells[row, 5, row, 9].Style.Numberformat.Format = "0.00";

                    // 边框
                    worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                    row++;
                }

                // 小计行
                var subtotalRow = row;
                worksheet.Cells[subtotalRow, 1, subtotalRow, 4].Merge = true;
                worksheet.Cells[subtotalRow, 1].Value = $"小计（{group.Key}）";
                worksheet.Cells[subtotalRow, 1].Style.Font.Bold = true;
                worksheet.Cells[subtotalRow, 5].Value = subtotalArea + subtotalVolume;  // 工程量小计（面积+体积）
                worksheet.Cells[subtotalRow, 9].Value = subtotalFormwork;               // 模板小计
                worksheet.Cells[subtotalRow, 5].Style.Numberformat.Format = "0.00";
                worksheet.Cells[subtotalRow, 9].Style.Numberformat.Format = "0.00";
                worksheet.Cells[subtotalRow, 1, subtotalRow, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[subtotalRow, 1, subtotalRow, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                row++;
            }

            // ===== 总计行 =====
            var totalRow = row;
            worksheet.Cells[totalRow, 1, totalRow, 4].Merge = true;
            worksheet.Cells[totalRow, 1].Value = "总计";
            worksheet.Cells[totalRow, 1].Style.Font.Bold = true;
            worksheet.Cells[totalRow, 1].Style.Font.Size = 12;
            double totalQuantity = components.Sum(c => c.Area) + components.Sum(c => c.Volume);
            worksheet.Cells[totalRow, 5].Value = totalQuantity;                         // 总工程量
            worksheet.Cells[totalRow, 9].Value = components.Sum(c => c.FormworkArea);   // 总模板
            worksheet.Cells[totalRow, 5].Style.Numberformat.Format = "0.00";
            worksheet.Cells[totalRow, 9].Style.Numberformat.Format = "0.00";
            worksheet.Cells[totalRow, 1, totalRow, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[totalRow, 1, totalRow, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.Orange);

            // 自动调整列宽
            worksheet.Cells.AutoFitColumns();

            Log.Information($"✅ 工作表1【工程量清单】创建完成，共{index - 1}条记录");
        }

        /// <summary>
        /// 工作表2：钢筋明细表
        /// </summary>
        private void CreateSteelDetailSheet(ExcelPackage package, List<ComponentRecognitionResult> components)
        {
            var worksheet = package.Workbook.Worksheets.Add("钢筋明细表");

            // 筛选钢筋构件
            var steelComponents = components.Where(c => c.Type.Contains("钢筋") && c.SteelWeight > 0).ToList();

            // ===== 标题行 =====
            worksheet.Cells["A1"].Value = "钢筋明细表";
            worksheet.Cells["A1:I1"].Merge = true;
            worksheet.Cells["A1"].Style.Font.Size = 16;
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Row(1).Height = 30;

            // ===== 表头行 =====
            int row = 3;
            var headers = new[] { "序号", "钢筋类型", "直径(mm)", "强度等级", "长度(m)", "根数", "总长(m)", "单重(kg/m)", "总重(kg)" };
            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = worksheet.Cells[row, col];
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // ===== 数据行 =====
            row++;
            int index = 1;

            // 按钢筋类型和直径分组
            var groupedSteel = steelComponents
                .GroupBy(c => new { c.Type, Diameter = Math.Round(c.Diameter * 1000) })  // 转为mm
                .OrderBy(g => g.Key.Type)
                .ThenBy(g => g.Key.Diameter);

            double totalWeight = 0;
            double totalLength = 0;

            foreach (var group in groupedSteel)
            {
                double diameterMm = group.Key.Diameter;
                double weightPerMeter = 0.617 * diameterMm * diameterMm / 100;  // kg/m
                int totalCount = group.Sum(c => c.Quantity);
                double groupLength = group.Sum(c => c.Length * c.Quantity);
                double groupWeight = group.Sum(c => c.SteelWeight);

                worksheet.Cells[row, 1].Value = index++;
                worksheet.Cells[row, 2].Value = group.Key.Type;
                worksheet.Cells[row, 3].Value = $"Φ{diameterMm}";
                worksheet.Cells[row, 4].Value = group.Key.Type.Contains("HRB400") ? "HRB400" :
                                                group.Key.Type.Contains("HRB500") ? "HRB500" :
                                                group.Key.Type.Contains("HPB300") ? "HPB300" : "HRB335";
                worksheet.Cells[row, 5].Value = group.First().Length;
                worksheet.Cells[row, 6].Value = totalCount;
                worksheet.Cells[row, 7].Value = groupLength;
                worksheet.Cells[row, 8].Value = weightPerMeter;
                worksheet.Cells[row, 9].Value = groupWeight;

                // 格式化
                worksheet.Cells[row, 5].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 7, row, 9].Style.Numberformat.Format = "0.00";

                // 边框
                worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                totalWeight += groupWeight;
                totalLength += groupLength;

                row++;
            }

            // ===== 总计行 =====
            if (steelComponents.Count > 0)
            {
                var totalRow = row;
                worksheet.Cells[totalRow, 1, totalRow, 6].Merge = true;
                worksheet.Cells[totalRow, 1].Value = "总计";
                worksheet.Cells[totalRow, 1].Style.Font.Bold = true;
                worksheet.Cells[totalRow, 1].Style.Font.Size = 12;
                worksheet.Cells[totalRow, 7].Value = totalLength;
                worksheet.Cells[totalRow, 9].Value = totalWeight;
                worksheet.Cells[totalRow, 7].Style.Numberformat.Format = "0.00";
                worksheet.Cells[totalRow, 9].Style.Numberformat.Format = "0.00";
                worksheet.Cells[totalRow, 1, totalRow, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[totalRow, 1, totalRow, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.Orange);
            }

            // 自动调整列宽
            worksheet.Cells.AutoFitColumns();

            Log.Information($"✅ 工作表2【钢筋明细表】创建完成，共{index - 1}条记录，总重{totalWeight:F2}kg");
        }

        /// <summary>
        /// 工作表3：材料汇总表
        /// </summary>
        private void CreateMaterialSummarySheet(ExcelPackage package, List<ComponentRecognitionResult> components)
        {
            var worksheet = package.Workbook.Worksheets.Add("材料汇总表");

            // ===== 标题行 =====
            worksheet.Cells["A1"].Value = "材料汇总表";
            worksheet.Cells["A1:E1"].Merge = true;
            worksheet.Cells["A1"].Style.Font.Size = 16;
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Row(1).Height = 30;

            // ===== 表头行 =====
            int row = 3;
            var headers = new[] { "材料名称", "规格", "单位", "数量", "备注" };
            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = worksheet.Cells[row, col];
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // ===== 数据行 =====
            row++;

            // 1. 混凝土汇总（按强度等级）
            var concreteGroups = components
                .Where(c => c.Type.Contains("混凝土") && c.Volume > 0)
                .GroupBy(c =>
                {
                    if (c.Type.Contains("C20")) return "C20";
                    if (c.Type.Contains("C25")) return "C25";
                    if (c.Type.Contains("C30")) return "C30";
                    if (c.Type.Contains("C35")) return "C35";
                    if (c.Type.Contains("C40")) return "C40";
                    return "其他";
                })
                .OrderBy(g => g.Key);

            foreach (var group in concreteGroups)
            {
                double totalVolume = group.Sum(c => c.Volume);
                worksheet.Cells[row, 1].Value = "混凝土";
                worksheet.Cells[row, 2].Value = group.Key;
                worksheet.Cells[row, 3].Value = "m³";
                worksheet.Cells[row, 4].Value = totalVolume;
                worksheet.Cells[row, 4].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 5].Value = $"包含{group.Count()}个构件";
                worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;
            }

            // 2. 钢筋汇总（按强度等级）
            var steelGroups = components
                .Where(c => c.Type.Contains("钢筋") && c.SteelWeight > 0)
                .GroupBy(c =>
                {
                    if (c.Type.Contains("HRB400")) return "HRB400";
                    if (c.Type.Contains("HRB500")) return "HRB500";
                    if (c.Type.Contains("HPB300")) return "HPB300";
                    if (c.Type.Contains("HRB335")) return "HRB335";
                    return "其他";
                })
                .OrderBy(g => g.Key);

            foreach (var group in steelGroups)
            {
                double totalWeight = group.Sum(c => c.SteelWeight);
                worksheet.Cells[row, 1].Value = "钢筋";
                worksheet.Cells[row, 2].Value = group.Key;
                worksheet.Cells[row, 3].Value = "t";
                worksheet.Cells[row, 4].Value = totalWeight / 1000;  // kg 转 t
                worksheet.Cells[row, 4].Style.Numberformat.Format = "0.000";
                worksheet.Cells[row, 5].Value = $"包含{group.Count()}个规格";
                worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;
            }

            // 3. 模板汇总
            double totalFormwork = components.Sum(c => c.FormworkArea);
            if (totalFormwork > 0)
            {
                worksheet.Cells[row, 1].Value = "模板";
                worksheet.Cells[row, 2].Value = "竹胶板";
                worksheet.Cells[row, 3].Value = "m²";
                worksheet.Cells[row, 4].Value = totalFormwork;
                worksheet.Cells[row, 4].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 5].Value = "混凝土构件模板面积";
                worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;
            }

            // 4. 砌体汇总
            var masonryGroups = components
                .Where(c => (c.Type.Contains("砖墙") || c.Type.Contains("砌块") || c.Type.Contains("加气")) && c.Volume > 0)
                .GroupBy(c => c.Type)
                .OrderBy(g => g.Key);

            foreach (var group in masonryGroups)
            {
                double totalVolume = group.Sum(c => c.Volume);
                worksheet.Cells[row, 1].Value = "砌体";
                worksheet.Cells[row, 2].Value = group.Key;
                worksheet.Cells[row, 3].Value = "m³";
                worksheet.Cells[row, 4].Value = totalVolume;
                worksheet.Cells[row, 4].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 5].Value = $"包含{group.Count()}个构件";
                worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;
            }

            // 5. 门窗汇总
            var doorWindowGroups = components
                .Where(c => (c.Type.Contains("门") || c.Type.Contains("窗")) && c.Area > 0)
                .GroupBy(c => c.Type)
                .OrderBy(g => g.Key);

            foreach (var group in doorWindowGroups)
            {
                int totalCount = group.Sum(c => c.Quantity);
                double totalArea = group.Sum(c => c.Area);
                worksheet.Cells[row, 1].Value = "门窗";
                worksheet.Cells[row, 2].Value = group.Key;
                worksheet.Cells[row, 3].Value = "樘";
                worksheet.Cells[row, 4].Value = totalCount;
                worksheet.Cells[row, 5].Value = $"总面积{totalArea:F2}m²";
                worksheet.Cells[row, 1, row, headers.Length].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;
            }

            // 自动调整列宽
            worksheet.Cells.AutoFitColumns();

            Log.Information($"✅ 工作表3【材料汇总表】创建完成");
        }
    }
}
