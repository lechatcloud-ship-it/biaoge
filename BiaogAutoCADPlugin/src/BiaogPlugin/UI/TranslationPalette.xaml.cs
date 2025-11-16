using System;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using BiaogPlugin.Services;
using BiaogPlugin.Models;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// TranslationPalette.xaml 的交互逻辑
    /// </summary>
    public partial class TranslationPalette : UserControl
    {
        private readonly TranslationController _controller;

        public TranslationPalette()
        {
            InitializeComponent();
            _controller = new TranslationController();

            // 初始化UI
            InitializeUI();

            // ✅ 商业级最佳实践：订阅Unloaded事件清理资源
            Unloaded += TranslationPalette_Unloaded;
        }

        /// <summary>
        /// ✅ 商业级最佳实践: UserControl卸载时清理所有资源
        /// 虽然当前没有需要清理的资源，但保持一致性以符合商业软件质量标准
        /// </summary>
        private void TranslationPalette_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 当前无需清理的资源
                // TranslationController从ServiceLocator获取服务，无需释放
                // 按钮事件会随UI树销毁自动清理

                Log.Debug("TranslationPalette资源清理完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TranslationPalette资源清理失败");
            }
        }

        private void InitializeUI()
        {
            // 添加日志
            AddLog("翻译面板已就绪");
            AddLog("支持8种语言的AI智能翻译");
            AddLog("基于AutoCAD .NET API - 100%准确的DWG处理");
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ✅ 交互反馈：禁用按钮并修改文本
                TranslateButton.IsEnabled = false;
                TranslateButton.Content = "翻译中...";
                ProgressCard.Visibility = Visibility.Visible;

                // 获取选择的语言
                var selectedItem = LanguageComboBox.SelectedItem as ComboBoxItem;
                var targetLang = selectedItem?.Tag as string ?? "en";
                var langName = selectedItem?.Content as string ?? "英语";

                AddLog($"开始翻译为{langName}...");

                // 清空进度
                ProgressBar.Value = 0;
                ProgressText.Text = "准备中...";

                // 进度回调
                var progress = new Progress<TranslationProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = p.Percentage;
                        ProgressText.Text = $"{p.Stage}: {p.Percentage}%";
                        AddLog($"[{DateTime.Now:HH:mm:ss}] {p.Stage}");
                    });
                });

                // 执行翻译
                var stats = await _controller.TranslateCurrentDrawing(targetLang, progress);

                // 更新统计信息
                UpdateStatistics(stats);

                AddLog("翻译完成！");
                AddLog(stats.ToString());

                MessageBox.Show(
                    $"翻译完成！\n\n" +
                    $"总文本: {stats.TotalTextCount}\n" +
                    $"唯一文本: {stats.UniqueTextCount}\n" +
                    $"缓存命中率: {stats.CacheHitRate:F1}%\n" +
                    $"成功率: {stats.SuccessRate:F1}%\n" +
                    $"耗时: {stats.TotalSeconds:F2}秒",
                    "翻译完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "翻译失败");
                AddLog($"[错误] {ex.Message}");

                MessageBox.Show(
                    $"翻译失败:\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // ✅ 恢复按钮状态和文本
                TranslateButton.IsEnabled = true;
                TranslateButton.Content = "开始翻译";
                ProgressCard.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStatistics(TranslationStatistics stats)
        {
            TotalCountText.Text = stats.TotalTextCount.ToString();
            UniqueCountText.Text = stats.UniqueTextCount.ToString();
            CacheHitText.Text = $"{stats.CacheHitRate:F1}%";
            ApiCallText.Text = stats.ApiCallCount.ToString();
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogText.Text += $"[{timestamp}] {message}\n";

                // 自动滚动到底部
                if (LogText.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToEnd();
                }

                // 限制日志长度
                if (LogText.Text.Length > 5000)
                {
                    LogText.Text = LogText.Text.Substring(LogText.Text.Length - 4000);
                }
            });
        }

        private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清除所有翻译缓存吗？\n\n这将删除所有已缓存的翻译结果。",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var cacheService = ServiceLocator.GetService<CacheService>();
                    if (cacheService != null)
                    {
                        await cacheService.ClearCacheAsync();
                        AddLog("缓存已清除");
                        MessageBox.Show("缓存已清除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        AddLog("[警告] 缓存服务未初始化");
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "清除缓存失败");
                    AddLog($"[错误] 清除缓存失败: {ex.Message}");
                }
            }
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"BiaogPlugin_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                System.IO.File.WriteAllText(logPath, LogText.Text);

                AddLog($"日志已导出: {logPath}");
                MessageBox.Show($"日志已导出到:\n{logPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "导出日志失败");
                MessageBox.Show($"导出日志失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
