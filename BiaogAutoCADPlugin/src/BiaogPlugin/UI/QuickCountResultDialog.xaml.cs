using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 快速统计结果对话框 - 以表格形式展示构件统计结果
    /// </summary>
    public partial class QuickCountResultDialog : Window
    {
        private List<Services.ComponentRecognitionResult> _allResults;
        private double _minimumConfidence = 0.7;

        public QuickCountResultDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置统计结果数据
        /// </summary>
        /// <param name="results">构件识别结果列表</param>
        public void SetResults(List<Services.ComponentRecognitionResult> results)
        {
            _allResults = results ?? new List<Services.ComponentRecognitionResult>();
            UpdateDisplay();
        }

        /// <summary>
        /// 更新显示内容
        /// </summary>
        private void UpdateDisplay()
        {
            if (_allResults == null || _allResults.Count == 0)
            {
                SummaryText.Text = "未找到构件";
                ResultsDataGrid.ItemsSource = null;
                return;
            }

            // 按置信度过滤
            var filtered = _allResults
                .Where(r => r.Confidence >= _minimumConfidence)
                .ToList();

            // 按类型分组统计
            var grouped = filtered
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Sum(r => r.Quantity))
                .Select((g, index) => new ComponentSummary
                {
                    Index = index + 1,
                    Type = g.Key,
                    Quantity = g.Sum(r => r.Quantity),
                    Confidence = $"{g.Average(r => r.Confidence):P0}",
                    Count = g.Count(),
                    Notes = $"最高: {g.Max(r => r.Confidence):P0}"
                })
                .ToList();

            // 更新UI
            ResultsDataGrid.ItemsSource = grouped;

            var totalComponents = filtered.Count;
            var totalTypes = grouped.Count;
            var totalQuantity = grouped.Sum(g => g.Quantity);
            var avgConfidence = filtered.Any() ? filtered.Average(r => r.Confidence) : 0;

            SummaryText.Text = $"共识别 {totalTypes} 种构件类型，{totalComponents} 个构件实例，总数量: {totalQuantity:N0}，平均置信度: {avgConfidence:P0}";
        }

        /// <summary>
        /// 置信度滑块值改变事件
        /// </summary>
        private void ConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ConfidenceValueText == null) return; // 初始化期间

            _minimumConfidence = e.NewValue / 100.0;
            ConfidenceValueText.Text = $"{e.NewValue:F0}%";
            UpdateDisplay();
        }

        /// <summary>
        /// 导出Excel按钮点击事件
        /// </summary>
        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取过滤后的结果
                var filtered = _allResults
                    .Where(r => r.Confidence >= _minimumConfidence)
                    .ToList();

                if (filtered.Count == 0)
                {
                    MessageBox.Show("没有符合条件的构件可导出", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取工程量计算器
                var calculator = Services.ServiceLocator.GetService<Services.QuantityCalculator>();
                if (calculator == null)
                {
                    MessageBox.Show("工程量计算器未初始化", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 计算工程量汇总
                var summary = calculator.CalculateSummary(filtered);

                // 获取Excel导出服务
                var exporter = Services.ServiceLocator.GetService<Services.ExcelExporter>();
                if (exporter == null)
                {
                    MessageBox.Show("Excel导出服务未初始化", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 导出Excel
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"构件统计_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = System.IO.Path.Combine(desktopPath, fileName);

                exporter.ExportSummary(summary, filePath);

                var result = MessageBox.Show($"Excel文件已导出到桌面：\n{fileName}\n\n是否打开文件所在文件夹？",
                    "导出成功", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }

                Log.Information("导出Excel成功: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导出Excel失败");
                MessageBox.Show($"导出Excel失败：\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 复制到剪贴板按钮点击事件
        /// </summary>
        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var items = ResultsDataGrid.ItemsSource as List<ComponentSummary>;
                if (items == null || items.Count == 0)
                {
                    MessageBox.Show("没有数据可复制", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 构建文本表格
                var sb = new StringBuilder();
                sb.AppendLine("构件统计结果");
                sb.AppendLine(SummaryText.Text);
                sb.AppendLine();
                sb.AppendLine("序号\t构件类型\t数量\t平均置信度\t识别次数\t备注");
                sb.AppendLine(new string('-', 80));

                foreach (var item in items)
                {
                    sb.AppendLine($"{item.Index}\t{item.Type}\t{item.Quantity}\t{item.Confidence}\t{item.Count}\t{item.Notes}");
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("统计结果已复制到剪贴板", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Log.Information("复制统计结果到剪贴板成功");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "复制到剪贴板失败");
                MessageBox.Show($"复制失败：\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// 构件汇总数据模型（用于DataGrid显示）
        /// </summary>
        public class ComponentSummary
        {
            public int Index { get; set; }
            public string Type { get; set; } = string.Empty;
            public double Quantity { get; set; }
            public string Confidence { get; set; } = string.Empty;
            public int Count { get; set; }
            public string Notes { get; set; } = string.Empty;
        }
    }
}
