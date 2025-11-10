using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// 算量报告生成器 - 生成专业的工程量清单PDF报告
/// 使用MigraDoc创建结构化文档
/// </summary>
public class CalculationReportGenerator
{
    private readonly ILogger<CalculationReportGenerator> _logger;

    public CalculationReportGenerator(ILogger<CalculationReportGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成完整的算量报告
    /// </summary>
    /// <param name="results">识别结果列表</param>
    /// <param name="outputPath">输出PDF路径</param>
    /// <param name="projectName">项目名称</param>
    public async Task GenerateReportAsync(
        IEnumerable<ComponentRecognitionResult> results,
        string outputPath,
        string projectName = "工程项目")
    {
        _logger.LogInformation("开始生成算量报告: {ProjectName}", projectName);

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

                // 创建MigraDoc文档
                var document = CreateDocument(results.ToList(), projectName);

                // 渲染为PDF
                var renderer = new PdfDocumentRenderer(unicode: true)
                {
                    Document = document
                };
                renderer.RenderDocument();
                renderer.PdfDocument.Save(outputPath);

                _logger.LogInformation("报告生成成功: {OutputPath}", outputPath);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成报告失败");
            throw new Exception($"报告生成失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 创建MigraDoc文档
    /// </summary>
    private Document CreateDocument(List<ComponentRecognitionResult> results, string projectName)
    {
        var document = new Document();
        DefineStyles(document);

        // 添加封面
        AddCoverPage(document, projectName, results);

        // 添加摘要统计
        AddSummarySection(document, results);

        // 添加详细构件清单
        AddDetailedComponentList(document, results);

        // 添加材料汇总
        AddMaterialSummary(document, results);

        // 添加成本分析
        AddCostAnalysis(document, results);

        return document;
    }

    /// <summary>
    /// 定义文档样式
    /// </summary>
    private void DefineStyles(Document document)
    {
        // 正常段落样式
        var normal = document.Styles["Normal"];
        normal!.Font.Name = "Microsoft YaHei";
        normal.Font.Size = 10;

        // 标题1样式
        var heading1 = document.Styles["Heading1"];
        heading1!.Font.Name = "Microsoft YaHei";
        heading1.Font.Size = 18;
        heading1.Font.Bold = true;
        heading1.Font.Color = Colors.DarkBlue;
        heading1.ParagraphFormat.SpaceBefore = 20;
        heading1.ParagraphFormat.SpaceAfter = 10;

        // 标题2样式
        var heading2 = document.Styles["Heading2"];
        heading2!.Font.Name = "Microsoft YaHei";
        heading2.Font.Size = 14;
        heading2.Font.Bold = true;
        heading2.Font.Color = Colors.Blue;
        heading2.ParagraphFormat.SpaceBefore = 15;
        heading2.ParagraphFormat.SpaceAfter = 8;

        // 表格样式
        var tableStyle = document.Styles.AddStyle("Table", "Normal");
        tableStyle.Font.Name = "Microsoft YaHei";
        tableStyle.Font.Size = 9;
    }

    /// <summary>
    /// 添加封面页
    /// </summary>
    private void AddCoverPage(Document document, string projectName, List<ComponentRecognitionResult> results)
    {
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
        section.PageSetup.TopMargin = "2cm";
        section.PageSetup.BottomMargin = "2cm";

        // 标题
        var titleParagraph = section.AddParagraph();
        titleParagraph.Format.Font.Size = 24;
        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.Font.Color = Colors.DarkBlue;
        titleParagraph.Format.Alignment = ParagraphAlignment.Center;
        titleParagraph.Format.SpaceBefore = "5cm";
        titleParagraph.AddText("建筑工程量清单报告");

        // 项目名称
        var projectParagraph = section.AddParagraph();
        projectParagraph.Format.Font.Size = 16;
        projectParagraph.Format.Alignment = ParagraphAlignment.Center;
        projectParagraph.Format.SpaceBefore = "1cm";
        projectParagraph.AddText(projectName);

        // 生成日期
        var dateParagraph = section.AddParagraph();
        dateParagraph.Format.Font.Size = 12;
        dateParagraph.Format.Alignment = ParagraphAlignment.Center;
        dateParagraph.Format.SpaceBefore = "2cm";
        dateParagraph.AddText($"生成日期: {DateTime.Now:yyyy年MM月dd日}");

        // 基本信息
        var infoParagraph = section.AddParagraph();
        infoParagraph.Format.Font.Size = 11;
        infoParagraph.Format.Alignment = ParagraphAlignment.Center;
        infoParagraph.Format.SpaceBefore = "1cm";
        infoParagraph.AddText($"构件总数: {results.Count}");
        infoParagraph.AddLineBreak();
        infoParagraph.AddText($"有效构件: {results.Count(r => r.Status == "有效")}");
        infoParagraph.AddLineBreak();
        infoParagraph.AddText($"总造价: ¥{results.Sum(r => r.Cost):N2}");

        // 换页
        section.AddPageBreak();
    }

    /// <summary>
    /// 添加摘要统计章节
    /// </summary>
    private void AddSummarySection(Document document, List<ComponentRecognitionResult> results)
    {
        var section = document.LastSection;

        // 标题
        var heading = section.AddParagraph("一、工程量汇总统计");
        heading.Style = "Heading1";

        // 统计表格
        var table = section.AddTable();
        table.Style = "Table";
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.Rows.LeftIndent = 0;

        // 定义列
        var column1 = table.AddColumn("8cm");
        column1.Format.Alignment = ParagraphAlignment.Left;
        var column2 = table.AddColumn("8cm");
        column2.Format.Alignment = ParagraphAlignment.Right;

        // 表头
        var headerRow = table.AddRow();
        headerRow.HeadingFormat = true;
        headerRow.Format.Font.Bold = true;
        headerRow.Shading.Color = Colors.LightBlue;
        headerRow.Cells[0].AddParagraph("统计项");
        headerRow.Cells[1].AddParagraph("数值");

        // 数据行
        AddStatRow(table, "构件总数", $"{results.Count} 个");
        AddStatRow(table, "有效构件", $"{results.Count(r => r.Status == "有效")} 个");
        AddStatRow(table, "识别置信度 (平均)", $"{(results.Any() ? results.Average(r => r.Confidence) * 100 : 0):F2}%");
        AddStatRow(table, "总工程量", $"{results.Sum(r => r.Quantity):F2} {GetMostCommonUnit(results)}");
        AddStatRow(table, "总造价", $"¥{results.Sum(r => r.Cost):N2}");

        // 按类型统计
        section.AddParagraph("\n类型分布统计:");
        var typeStats = results.GroupBy(r => r.ComponentType).OrderByDescending(g => g.Count());
        foreach (var group in typeStats)
        {
            var para = section.AddParagraph();
            para.Format.LeftIndent = "1cm";
            para.AddText($"• {group.Key}: {group.Count()} 个 ({group.Sum(r => r.Quantity):F2} {GetUnitForType(group.Key)})");
        }

        section.AddParagraph(); // 空行
    }

    /// <summary>
    /// 添加详细构件清单
    /// </summary>
    private void AddDetailedComponentList(Document document, List<ComponentRecognitionResult> results)
    {
        var section = document.LastSection;

        // 标题
        var heading = section.AddParagraph("二、构件明细清单");
        heading.Style = "Heading1";

        // 详细表格
        var table = section.AddTable();
        table.Style = "Table";
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.Rows.LeftIndent = 0;

        // 定义列 - 7列
        table.AddColumn("0.8cm"); // 序号
        table.AddColumn("2.5cm"); // 类型
        table.AddColumn("3cm");   // 规格
        table.AddColumn("1.5cm"); // 数量
        table.AddColumn("1.2cm"); // 单位
        table.AddColumn("2cm");   // 单价
        table.AddColumn("2cm");   // 造价

        // 表头
        var headerRow = table.AddRow();
        headerRow.HeadingFormat = true;
        headerRow.Format.Font.Bold = true;
        headerRow.Shading.Color = Colors.LightBlue;
        headerRow.Cells[0].AddParagraph("序号");
        headerRow.Cells[1].AddParagraph("构件类型");
        headerRow.Cells[2].AddParagraph("规格参数");
        headerRow.Cells[3].AddParagraph("数量");
        headerRow.Cells[4].AddParagraph("单位");
        headerRow.Cells[5].AddParagraph("单价");
        headerRow.Cells[6].AddParagraph("造价");

        // 数据行
        int index = 1;
        foreach (var result in results.OrderBy(r => r.ComponentType))
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(index.ToString());
            row.Cells[1].AddParagraph(result.ComponentType);
            row.Cells[2].AddParagraph(result.Specification ?? "-");
            row.Cells[3].AddParagraph($"{result.Quantity:F2}");
            row.Cells[4].AddParagraph(result.Unit);
            row.Cells[5].AddParagraph($"¥{result.UnitPrice:F2}");
            row.Cells[6].AddParagraph($"¥{result.Cost:F2}");

            // 右对齐数值列
            row.Cells[0].Format.Alignment = ParagraphAlignment.Center;
            row.Cells[3].Format.Alignment = ParagraphAlignment.Right;
            row.Cells[4].Format.Alignment = ParagraphAlignment.Center;
            row.Cells[5].Format.Alignment = ParagraphAlignment.Right;
            row.Cells[6].Format.Alignment = ParagraphAlignment.Right;

            index++;
        }

        // 合计行
        var totalRow = table.AddRow();
        totalRow.Format.Font.Bold = true;
        totalRow.Shading.Color = Colors.LightYellow;
        totalRow.Cells[0].MergeRight = 5;
        totalRow.Cells[0].AddParagraph("合计");
        totalRow.Cells[0].Format.Alignment = ParagraphAlignment.Right;
        totalRow.Cells[6].AddParagraph($"¥{results.Sum(r => r.Cost):N2}");
        totalRow.Cells[6].Format.Alignment = ParagraphAlignment.Right;

        section.AddParagraph(); // 空行
    }

    /// <summary>
    /// 添加材料汇总
    /// </summary>
    private void AddMaterialSummary(Document document, List<ComponentRecognitionResult> results)
    {
        var section = document.LastSection;

        // 标题
        var heading = section.AddParagraph("三、材料用量汇总");
        heading.Style = "Heading1";

        // 汇总材料
        var materialSummary = new Dictionary<string, decimal>();
        foreach (var result in results)
        {
            if (result.Materials != null)
            {
                foreach (var material in result.Materials)
                {
                    if (materialSummary.ContainsKey(material.Key))
                    {
                        materialSummary[material.Key] += material.Value;
                    }
                    else
                    {
                        materialSummary[material.Key] = material.Value;
                    }
                }
            }
        }

        if (materialSummary.Any())
        {
            // 材料表格
            var table = section.AddTable();
            table.Style = "Table";
            table.Borders.Width = 0.5;
            table.Borders.Color = Colors.Gray;
            table.Rows.LeftIndent = 0;

            // 定义列
            table.AddColumn("2cm");  // 序号
            table.AddColumn("10cm"); // 材料名称
            table.AddColumn("4cm");  // 用量

            // 表头
            var headerRow = table.AddRow();
            headerRow.HeadingFormat = true;
            headerRow.Format.Font.Bold = true;
            headerRow.Shading.Color = Colors.LightBlue;
            headerRow.Cells[0].AddParagraph("序号");
            headerRow.Cells[1].AddParagraph("材料名称");
            headerRow.Cells[2].AddParagraph("用量");

            // 数据行
            int index = 1;
            foreach (var material in materialSummary.OrderByDescending(m => m.Value))
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(index.ToString());
                row.Cells[0].Format.Alignment = ParagraphAlignment.Center;
                row.Cells[1].AddParagraph(material.Key);
                row.Cells[2].AddParagraph($"{material.Value:F2}");
                row.Cells[2].Format.Alignment = ParagraphAlignment.Right;
                index++;
            }
        }
        else
        {
            section.AddParagraph("暂无材料用量数据");
        }

        section.AddParagraph(); // 空行
    }

    /// <summary>
    /// 添加成本分析
    /// </summary>
    private void AddCostAnalysis(Document document, List<ComponentRecognitionResult> results)
    {
        var section = document.LastSection;

        // 标题
        var heading = section.AddParagraph("四、成本分析");
        heading.Style = "Heading1";

        // 按类型统计成本
        var costByType = results
            .GroupBy(r => r.ComponentType)
            .Select(g => new { Type = g.Key, Cost = g.Sum(r => r.Cost), Count = g.Count() })
            .OrderByDescending(x => x.Cost);

        // 成本表格
        var table = section.AddTable();
        table.Style = "Table";
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.Rows.LeftIndent = 0;

        // 定义列
        table.AddColumn("6cm");  // 构件类型
        table.AddColumn("3cm");  // 数量
        table.AddColumn("4cm");  // 总造价
        table.AddColumn("3cm");  // 占比

        // 表头
        var headerRow = table.AddRow();
        headerRow.HeadingFormat = true;
        headerRow.Format.Font.Bold = true;
        headerRow.Shading.Color = Colors.LightBlue;
        headerRow.Cells[0].AddParagraph("构件类型");
        headerRow.Cells[1].AddParagraph("数量");
        headerRow.Cells[2].AddParagraph("总造价");
        headerRow.Cells[3].AddParagraph("占比");

        // 数据行
        var totalCost = results.Sum(r => r.Cost);
        foreach (var item in costByType)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.Type);
            row.Cells[1].AddParagraph($"{item.Count} 个");
            row.Cells[1].Format.Alignment = ParagraphAlignment.Right;
            row.Cells[2].AddParagraph($"¥{item.Cost:N2}");
            row.Cells[2].Format.Alignment = ParagraphAlignment.Right;
            row.Cells[3].AddParagraph($"{(totalCost > 0 ? item.Cost / totalCost * 100 : 0):F2}%");
            row.Cells[3].Format.Alignment = ParagraphAlignment.Right;
        }

        // 合计行
        var totalRow = table.AddRow();
        totalRow.Format.Font.Bold = true;
        totalRow.Shading.Color = Colors.LightYellow;
        totalRow.Cells[0].AddParagraph("合计");
        totalRow.Cells[1].AddParagraph($"{results.Count} 个");
        totalRow.Cells[1].Format.Alignment = ParagraphAlignment.Right;
        totalRow.Cells[2].AddParagraph($"¥{totalCost:N2}");
        totalRow.Cells[2].Format.Alignment = ParagraphAlignment.Right;
        totalRow.Cells[3].AddParagraph("100.00%");
        totalRow.Cells[3].Format.Alignment = ParagraphAlignment.Right;

        // 页脚
        section.AddParagraph();
        var footer = section.AddParagraph();
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText($"\n--- 报告结束 ---\n由CAD翻译算量工具自动生成 © {DateTime.Now.Year}");
    }

    /// <summary>
    /// 添加统计行
    /// </summary>
    private void AddStatRow(Table table, string label, string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(value);
    }

    /// <summary>
    /// 获取最常用单位
    /// </summary>
    private string GetMostCommonUnit(List<ComponentRecognitionResult> results)
    {
        return results.GroupBy(r => r.Unit)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "个";
    }

    /// <summary>
    /// 根据类型获取单位
    /// </summary>
    private string GetUnitForType(string type)
    {
        return type switch
        {
            "墙体" => "m²",
            "柱子" => "m³",
            "梁" => "m³",
            "板" => "m²",
            "门窗" => "个",
            "楼梯" => "个",
            _ => "个"
        };
    }
}
