using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 关于对话框 - 显示插件信息、版本、功能列表
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        /// <summary>
        /// 加载版本信息
        /// </summary>
        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                VersionText.Text = $"版本 {version?.Major}.{version?.Minor}.{version?.Build}";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "获取版本信息失败");
                VersionText.Text = "版本 1.0.0";
            }
        }

        /// <summary>
        /// 超链接点击事件 - 打开浏览器
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开链接失败: {Url}", e.Uri.AbsoluteUri);
                MessageBox.Show($"无法打开链接: {e.Uri.AbsoluteUri}\n\n{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 查看帮助按钮点击事件
        /// </summary>
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 关闭当前对话框
                this.DialogResult = false;
                this.Close();

                // 打开帮助对话框
                var helpDialog = new HelpDialog();
                helpDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开帮助对话框失败");
                MessageBox.Show($"无法打开帮助对话框: {ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
