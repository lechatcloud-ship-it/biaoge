using System;
using System.Windows.Forms;

namespace BiaogInstaller
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                // 使用DpiUnaware模式：窗口会被Windows放大，显示尺寸最大
                Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 设置全局异常处理
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                Application.Run(new InstallerForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"安装程序启动失败：\n\n{ex.Message}\n\n详细信息：\n{ex.StackTrace}",
                    "启动错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show(
                $"程序运行出错：\n\n{e.Exception.Message}\n\n详细信息：\n{e.Exception.StackTrace}",
                "运行错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"未处理的异常：\n\n{ex.Message}\n\n详细信息：\n{ex.StackTrace}",
                    "严重错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
