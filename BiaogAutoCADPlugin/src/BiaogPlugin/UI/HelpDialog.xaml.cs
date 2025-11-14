using System;
using System.Diagnostics;
using System.Windows;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 帮助对话框 - 显示所有命令的使用说明和快速入门指南
    /// </summary>
    public partial class HelpDialog : Window
    {
        public HelpDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 在线文档按钮点击事件
        /// </summary>
        private void OnlineDocsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = "https://biaogecad.shangyanyun.com";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开在线文档失败");
                MessageBox.Show($"无法打开在线文档\n\n{ex.Message}",
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
