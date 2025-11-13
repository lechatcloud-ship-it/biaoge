using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 导出Excel选项对话框
    /// </summary>
    public partial class ExportExcelOptionsDialog : Window
    {
        public ExportOptions Options { get; private set; }

        public ExportExcelOptionsDialog()
        {
            InitializeComponent();
            InitializeDefaults();
        }

        /// <summary>
        /// 初始化默认值
        /// </summary>
        private void InitializeDefaults()
        {
            // 默认文件名
            FileNameTextBox.Text = $"工程量清单_{DateTime.Now:yyyyMMdd_HHmmss}";

            // 默认保存到桌面
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            CustomPathTextBox.Text = desktopPath;

            Options = new ExportOptions();
        }

        /// <summary>
        /// 浏览按钮点击事件
        /// </summary>
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "选择保存位置",
                    Filter = "Excel文件 (*.xlsx)|*.xlsx",
                    FileName = FileNameTextBox.Text,
                    InitialDirectory = CustomPathTextBox.Text
                };

                if (dialog.ShowDialog() == true)
                {
                    var directory = Path.GetDirectoryName(dialog.FileName);
                    var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);

                    CustomPathTextBox.Text = directory ?? string.Empty;
                    FileNameTextBox.Text = fileName;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "浏览文件夹失败");
                MessageBox.Show($"浏览文件夹失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出按钮点击事件
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证文件名
                var fileName = FileNameTextBox.Text.Trim();
                if (string.IsNullOrEmpty(fileName))
                {
                    MessageBox.Show("请输入文件名", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    FileNameTextBox.Focus();
                    return;
                }

                // 验证文件名有效性
                if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show("文件名包含无效字符", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    FileNameTextBox.Focus();
                    return;
                }

                // 确定保存路径
                string savePath;
                if (SaveToDesktopRadio.IsChecked == true)
                {
                    savePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                else
                {
                    savePath = CustomPathTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath))
                    {
                        MessageBox.Show("自定义路径不存在，请重新选择", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 构建完整文件路径
                var fullPath = Path.Combine(savePath, fileName + ".xlsx");

                // 检查文件是否存在
                if (File.Exists(fullPath))
                {
                    var result = MessageBox.Show(
                        $"文件已存在，是否覆盖？\n{fullPath}",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                // 设置导出选项
                Options = new ExportOptions
                {
                    FilePath = fullPath,
                    IncludeSummary = IncludeSummaryCheckBox.IsChecked == true,
                    IncludeDetail = IncludeDetailCheckBox.IsChecked == true,
                    IncludeMaterial = IncludeMaterialCheckBox.IsChecked == true,
                    AfterExportAction = GetAfterExportAction()
                };

                // 验证至少选择一个导出内容
                if (!Options.IncludeSummary && !Options.IncludeDetail && !Options.IncludeMaterial)
                {
                    MessageBox.Show("请至少选择一个导出内容", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "验证导出选项失败");
                MessageBox.Show($"操作失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取完成后操作
        /// </summary>
        private AfterExportAction GetAfterExportAction()
        {
            if (OpenFileRadio.IsChecked == true)
                return AfterExportAction.OpenFile;
            if (OpenFolderRadio.IsChecked == true)
                return AfterExportAction.OpenFolder;
            return AfterExportAction.DoNothing;
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    /// <summary>
    /// 导出选项数据模型
    /// </summary>
    public class ExportOptions
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IncludeSummary { get; set; } = true;
        public bool IncludeDetail { get; set; } = true;
        public bool IncludeMaterial { get; set; } = true;
        public AfterExportAction AfterExportAction { get; set; } = AfterExportAction.OpenFolder;
    }

    /// <summary>
    /// 完成后操作枚举
    /// </summary>
    public enum AfterExportAction
    {
        OpenFile,
        OpenFolder,
        DoNothing
    }
}
