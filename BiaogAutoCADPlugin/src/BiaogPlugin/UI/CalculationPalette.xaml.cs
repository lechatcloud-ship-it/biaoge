using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using BiaogPlugin.Services;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// CalculationPalette.xaml 的交互逻辑
    /// </summary>
    public partial class CalculationPalette : UserControl
    {
        private ComponentRecognizer? _recognizer;
        private QuantityCalculator? _calculator;
        private ExcelExporter? _exporter;
        private List<ComponentRecognitionResult>? _currentResults;
        private QuantitySummary? _currentSummary;

        public CalculationPalette()
        {
            InitializeComponent();
            InitializeServices();
            InitializeUI();
        }

        private void InitializeServices()
        {
            try
            {
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                if (bailianClient != null)
                {
                    _recognizer = new ComponentRecognizer(bailianClient);
                }

                _calculator = new QuantityCalculator();
                _exporter = new ExcelExporter();

                AddLog("算量工具已就绪");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "初始化算量服务失败");
                AddLog($"[错误] 初始化失败: {ex.Message}");
            }
        }

        private void InitializeUI()
        {
            // 置信度阈值滑块事件
            ConfidenceThresholdSlider.ValueChanged += (s, e) =>
            {
                ConfidenceThresholdText.Text = $"{ConfidenceThresholdSlider.Value:P0}";
            };

            AddLog("支持多策略构件识别（正则+AI验证+规范约束）");
            AddLog("自动计算工程量和成本估算");
        }

        private async void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recognizer == null)
            {
                MessageBox.Show("算量服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                RecognizeButton.IsEnabled = false;
                ProgressCard.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;

                AddLog("开始识别构件...");

                // 获取当前文档
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    AddLog("[错误] 没有活动的文档");
                    return;
                }

                // 提取文本
                ProgressText.Text = "提取文本...";
                ProgressBar.Value = 10;

                var extractor = new DwgTextExtractor();
                var textEntities = extractor.ExtractAllText();

                AddLog($"提取到 {textEntities.Count} 个文本实体");

                // 识别构件
                ProgressText.Text = "识别构件...";
                ProgressBar.Value = 30;

                var useAi = UseAiVerificationCheckBox.IsChecked ?? false;
                _currentResults = await _recognizer.RecognizeFromTextEntitiesAsync(textEntities, useAi);

                AddLog($"识别完成: {_currentResults.Count} 个构件");

                // 过滤低置信度构件
                ProgressText.Text = "过滤结果...";
                ProgressBar.Value = 60;

                var threshold = ConfidenceThresholdSlider.Value;
                _currentResults = _currentResults.Where(r => r.Confidence >= threshold).ToList();

                AddLog($"置信度>{threshold:P0}的构件: {_currentResults.Count}个");

                // 计算工程量
                ProgressText.Text = "计算工程量...";
                ProgressBar.Value = 80;

                if (_calculator != null)
                {
                    _currentSummary = _calculator.CalculateSummary(_currentResults);
                    UpdateStatistics(_currentSummary);
                    UpdateComponentsList(_currentResults);

                    AddLog("工程量计算完成");
                }

                ProgressBar.Value = 100;
                ProgressCard.Visibility = Visibility.Collapsed;

                // 显示结果卡片
                StatsCard.Visibility = Visibility.Visible;
                ComponentsCard.Visibility = Visibility.Visible;

                // 启用导出按钮
                ExportExcelButton.IsEnabled = true;

                MessageBox.Show(
                    $"识别完成！\n\n" +
                    $"构件总数: {_currentSummary.TotalComponents}\n" +
                    $"有效构件: {_currentSummary.ValidCount}\n" +
                    $"总成本: ¥{_currentSummary.TotalCost:N2}",
                    "识别完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "构件识别失败");
                AddLog($"[错误] {ex.Message}");
                MessageBox.Show($"识别失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RecognizeButton.IsEnabled = true;
                ProgressCard.Visibility = Visibility.Collapsed;
            }
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSummary == null || _exporter == null)
            {
                MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 选择保存位置
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var defaultFileName = $"工程量清单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var outputPath = Path.Combine(desktopPath, defaultFileName);

                // 导出Excel
                AddLog("正在导出Excel...");
                _exporter.ExportSummary(_currentSummary, outputPath);

                AddLog($"Excel已导出: {outputPath}");
                MessageBox.Show(
                    $"Excel清单已导出到:\n{outputPath}",
                    "导出成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // 打开文件夹
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导出Excel失败");
                AddLog($"[错误] 导出失败: {ex.Message}");
                MessageBox.Show($"导出失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatistics(QuantitySummary summary)
        {
            Dispatcher.Invoke(() =>
            {
                TotalComponentsText.Text = summary.TotalComponents.ToString();
                ValidComponentsText.Text = summary.ValidCount.ToString();
                AbnormalComponentsText.Text = summary.AbnormalCount.ToString();
                AvgConfidenceText.Text = $"{summary.AverageConfidence:P0}";
                TotalCostText.Text = $"¥{summary.TotalCost:N2}";
                TotalVolumeText.Text = $"{summary.TotalVolume:F2}m³";
            });
        }

        private void UpdateComponentsList(List<ComponentRecognitionResult> results)
        {
            Dispatcher.Invoke(() =>
            {
                ComponentsList.ItemsSource = results.Take(20).ToList(); // 只显示前20个
            });
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogText.Text += $"[{timestamp}] {message}\n";

                // 限制日志长度
                if (LogText.Text.Length > 3000)
                {
                    LogText.Text = LogText.Text.Substring(LogText.Text.Length - 2000);
                }
            });
        }
    }
}
