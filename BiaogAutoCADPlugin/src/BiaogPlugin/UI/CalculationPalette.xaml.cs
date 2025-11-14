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
        private AIComponentRecognizer? _aiRecognizer;
        private QuantityCalculator? _calculator;
        private ExcelExporter? _exporter;
        private List<ComponentRecognitionResult>? _currentResults;
        private QuantitySummary? _currentSummary;
        private int _aiVerifiedCount = 0;

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
                    _aiRecognizer = new AIComponentRecognizer(bailianClient);
                }

                _calculator = new QuantityCalculator();
                _exporter = new ExcelExporter();

                AddLog("算量工具已就绪（集成qwen3-vl-flash视觉识别）");
            }
            catch (System.Exception ex)
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

            // 精度模式切换事件
            PrecisionModeComboBox.SelectionChanged += (s, e) =>
            {
                var selected = PrecisionModeComboBox.SelectedItem as ComboBoxItem;
                if (selected != null)
                {
                    switch (selected.Tag as string)
                    {
                        case "QuickEstimate":
                            PrecisionModeDescription.Text = "仅规则引擎，速度快，成本¥0";
                            break;
                        case "Budget":
                            PrecisionModeDescription.Text = "推荐：预算控制模式，平衡精度和成本";
                            break;
                        case "FinalAccount":
                            PrecisionModeDescription.Text = "最高精度，适用于竣工结算审计";
                            break;
                    }
                }
            };

            AddLog("支持多策略构件识别（规则引擎+qwen3-vl-flash视觉验证）");
            AddLog("自动计算工程量和成本估算");
        }

        private async void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_aiRecognizer == null)
            {
                MessageBox.Show("AI算量服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                RecognizeButton.IsEnabled = false;
                ProgressCard.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                _aiVerifiedCount = 0;

                // 获取精度模式
                var selected = PrecisionModeComboBox.SelectedItem as ComboBoxItem;
                var precisionMode = CalculationPrecision.Budget; // 默认预算模式
                if (selected != null)
                {
                    switch (selected.Tag as string)
                    {
                        case "QuickEstimate":
                            precisionMode = CalculationPrecision.QuickEstimate;
                            AddLog("精度模式: 快速估算（仅规则引擎）");
                            break;
                        case "Budget":
                            precisionMode = CalculationPrecision.Budget;
                            AddLog("精度模式: 预算控制（规则+AI验证30%）");
                            break;
                        case "FinalAccount":
                            precisionMode = CalculationPrecision.FinalAccount;
                            AddLog("精度模式: 竣工结算（规则+AI验证100%）");
                            break;
                    }
                }

                AddLog("开始识别构件...");

                // 获取当前文档
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    AddLog("[错误] 没有活动的文档");
                    return;
                }

                // ===== AutoCAD API调用（需要文档锁定） =====
                // ✅ 最佳实践：PaletteSet事件中调用AutoCAD API应显式锁定文档
                // 参考：AutoCAD官方文档 - "When to Lock the Document"
                List<TextEntity> textEntities;
                List<string> layerNames;

                ProgressText.Text = "提取文本...";
                ProgressBar.Value = 10;

                using (var docLock = doc.LockDocument())
                {
                    // 在文档锁定下提取DWG数据
                    var extractor = new DwgTextExtractor();
                    textEntities = extractor.ExtractAllText();

                    // 提取图层名称
                    layerNames = textEntities.Select(t => t.Layer).Distinct().ToList();
                }
                // ✅ 文档锁定在await之前释放（避免死锁）

                AddLog($"提取到 {textEntities.Count} 个文本实体");
                AddLog($"图层数: {layerNames.Count}");

                // ===== AI异步识别（不需要文档锁定） =====
                ProgressText.Text = "AI构件识别中...";
                ProgressBar.Value = 30;

                _currentResults = await _aiRecognizer.RecognizeAsync(
                    textEntities,
                    layerNames,
                    precisionMode
                );

                AddLog($"识别完成: {_currentResults.Count} 个构件");

                // 统计AI验证数量
                _aiVerifiedCount = _currentResults.Count(r => r.OriginalText?.Contains("VL") ?? false);
                if (_aiVerifiedCount > 0)
                {
                    AddLog($"AI视觉验证: {_aiVerifiedCount} 个构件");
                }

                // 过滤低置信度构件
                ProgressText.Text = "过滤结果...";
                ProgressBar.Value = 60;

                var threshold = ConfidenceThresholdSlider.Value;
                var beforeFilterCount = _currentResults.Count;
                _currentResults = _currentResults.Where(r => r.Confidence >= threshold).ToList();

                AddLog($"置信度>{threshold:P0}的构件: {_currentResults.Count}/{beforeFilterCount}");

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
                    $"AI验证率: {(_aiVerifiedCount * 100.0 / Math.Max(_currentResults.Count, 1)):F1}%\n" +
                    $"总成本: ¥{_currentSummary.TotalCost:N2}",
                    "识别完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception acEx)
            {
                // AutoCAD API异常
                Log.Error(acEx, "AutoCAD API调用失败");
                AddLog($"[错误] AutoCAD API错误: {acEx.Message}");
                MessageBox.Show(
                    $"AutoCAD操作失败:\n{acEx.Message}\n\n" +
                    "可能原因：\n" +
                    "• 图纸已损坏或无效\n" +
                    "• 图层或文本实体已删除\n" +
                    "• AutoCAD版本不兼容\n\n" +
                    "建议：检查图纸完整性，或联系技术支持",
                    "AutoCAD错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                // 网络/API调用异常
                Log.Error(httpEx, "百炼API网络请求失败");
                AddLog($"[错误] 网络错误: {httpEx.Message}");
                MessageBox.Show(
                    $"AI服务连接失败:\n{httpEx.Message}\n\n" +
                    "可能原因：\n" +
                    "• 网络连接中断\n" +
                    "• API密钥无效或过期\n" +
                    "• 百炼服务暂时不可用\n\n" +
                    "建议：\n" +
                    "1. 检查网络连接\n" +
                    "2. 运行BIAOGE_DIAGNOSTIC诊断\n" +
                    "3. 验证API密钥（BIAOGE_SETTINGS）",
                    "网络错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (System.InvalidOperationException invEx) when (invEx.Message.Contains("没有活动的AutoCAD文档"))
            {
                // 文档状态异常
                Log.Warning(invEx, "文档状态异常");
                AddLog($"[错误] {invEx.Message}");
                MessageBox.Show(
                    "无法访问AutoCAD文档\n\n" +
                    "请确保：\n" +
                    "• 至少打开一个DWG文件\n" +
                    "• 文档未处于编辑锁定状态",
                    "文档错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (System.Exception ex)
            {
                // 未知异常
                Log.Error(ex, "构件识别发生未知错误");
                AddLog($"[错误] 未知错误: {ex.GetType().Name} - {ex.Message}");
                MessageBox.Show(
                    $"识别过程发生错误:\n{ex.Message}\n\n" +
                    $"错误类型: {ex.GetType().Name}\n\n" +
                    "详细信息已记录到日志文件\n" +
                    $"日志路径: %APPDATA%\\Biaoge\\Logs\\",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                RecognizeButton.IsEnabled = true;
                ProgressCard.Visibility = Visibility.Collapsed;
                ProgressText.Text = "就绪";
                ProgressBar.Value = 0;
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
            catch (System.Exception ex)
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
                AvgConfidenceText.Text = $"{summary.AverageConfidence:P0}";
                TotalVolumeText.Text = $"{summary.TotalVolume:F2}m³";
                TotalCostText.Text = $"¥{summary.TotalCost:N2}";

                // AI验证率
                var verificationRate = _currentResults != null && _currentResults.Count > 0
                    ? (_aiVerifiedCount * 100.0 / _currentResults.Count)
                    : 0;
                AiVerificationRateText.Text = $"{verificationRate:F1}%";
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
