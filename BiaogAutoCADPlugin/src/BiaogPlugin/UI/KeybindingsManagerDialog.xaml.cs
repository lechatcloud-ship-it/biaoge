using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 快捷键管理对话框
    /// </summary>
    public partial class KeybindingsManagerDialog : Window
    {
        public KeybindingsManagerDialog()
        {
            InitializeComponent();
            LoadKeybindings();
        }

        /// <summary>
        /// 加载快捷键列表
        /// </summary>
        private void LoadKeybindings()
        {
            try
            {
                var keybindingsMap = Services.KeybindingsManager.GetKeybindingsMap();

                var keybindingsList = keybindingsMap.Select(kvp => new KeybindingItem
                {
                    CommandName = kvp.Key,
                    Shortcut = kvp.Value.shortcut,
                    Description = kvp.Value.description
                }).OrderBy(k => k.Shortcut).ToList();

                KeybindingsGrid.ItemsSource = keybindingsList;

                Log.Information($"已加载 {keybindingsList.Count} 个快捷键配置");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载快捷键列表失败");
                MessageBox.Show($"加载快捷键列表失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出配置到桌面
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = Services.KeybindingsManager.SavePgpConfigToDesktop();

                MessageBox.Show(
                    $"快捷键配置已导出到桌面：\n\n{filePath}\n\n" +
                    "请按照文件中的说明手动添加到 acad.pgp 文件。",
                    "导出成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // 打开文件所在文件夹
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");

                Log.Information($"快捷键配置已导出: {filePath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导出快捷键配置失败");
                MessageBox.Show($"导出失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 自动安装快捷键
        /// </summary>
        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "自动安装将会：\n\n" +
                    "1. 自动备份您的 acad.pgp 文件\n" +
                    "2. 将标哥插件的快捷键添加到配置中\n" +
                    "3. 不会覆盖您现有的快捷键\n\n" +
                    "是否继续？",
                    "确认自动安装",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                bool success = Services.KeybindingsManager.TryInstallKeybindings(out string message);

                if (success)
                {
                    MessageBox.Show(
                        message,
                        "安装成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Log.Information("快捷键自动安装成功");
                }
                else
                {
                    var fallbackResult = MessageBox.Show(
                        $"{message}\n\n是否导出配置文件以便手动安装？",
                        "自动安装失败",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (fallbackResult == MessageBoxResult.Yes)
                    {
                        ExportButton_Click(sender, e);
                    }

                    Log.Warning($"快捷键自动安装失败: {message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "自动安装快捷键失败");
                MessageBox.Show($"自动安装失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 查看安装指南
        /// </summary>
        private void GuideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var guide = Services.KeybindingsManager.GetKeybindingsGuide();

                var guideDialog = new TextDisplayDialog("快捷键安装指南", guide);
                guideDialog.Owner = this;
                guideDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示快捷键指南失败");
                MessageBox.Show($"显示指南失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// 快捷键列表项
    /// </summary>
    public class KeybindingItem
    {
        public string CommandName { get; set; } = string.Empty;
        public string Shortcut { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 文本显示对话框（用于显示指南）
    /// </summary>
    public class TextDisplayDialog : Window
    {
        public TextDisplayDialog(string title, string content)
        {
            Title = title;
            Width = 700;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(45, 45, 48));
            Foreground = System.Windows.Media.Brushes.White;

            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new Thickness(15)
            };

            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = content,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, 微软雅黑"),
                FontSize = 13,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = System.Windows.Media.Brushes.White
            };

            scrollViewer.Content = textBlock;

            Content = scrollViewer;
        }
    }
}
