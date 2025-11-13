using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Serilog;

namespace BiaogPlugin.UI.Controls
{
    /// <summary>
    /// 自定义TextBox，解决在AutoCAD PaletteSet中中文输入法焦点跳转问题
    ///
    /// 问题原因：
    /// WPF控件在AutoCAD的PaletteSet中通过ElementHost托管时，
    /// 需要处理Windows消息循环才能正确处理键盘输入和IME（Input Method Editor）
    ///
    /// 解决方案：
    /// Hook HwndSource并处理WM_GETDLGCODE消息，告诉系统此控件需要接收字符输入
    ///
    /// 参考：
    /// https://stackoverflow.com/questions/835878/wpf-textbox-not-accepting-input-when-in-elementhost-in-window-forms
    /// https://social.msdn.microsoft.com/Forums/vstudio/en-US/cfd20c98-a809-481c-8f68-34e473c182fa/keyboard-input-on-a-hwndsourcehosted-textbox
    /// </summary>
    public class AutoCADTextBox : TextBox
    {
        // Windows消息常量
        private const int WM_GETDLGCODE = 0x0087;
        private const int WM_CHAR = 0x0102;  // ✅ 字符消息（用于防止重复输入）

        // 对话框代码标志
        private const int DLGC_WANTCHARS = 0x0080;   // 接收字符输入（包括中文IME）
        private const int DLGC_WANTARROWS = 0x0001;  // 接收方向键
        private const int DLGC_HASSETSEL = 0x0008;   // 支持文本选择
        private const int DLGC_WANTTAB = 0x0002;     // 接收Tab键（可选）
        private const int DLGC_WANTALLKEYS = 0x0004; // 接收所有键盘输入

        private bool _hookInstalled = false;

        public AutoCADTextBox() : base()
        {
            // 当控件加载完成时，安装Windows消息钩子
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// 控件加载时安装消息钩子
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hookInstalled)
                {
                    return;
                }

                // 获取WPF控件的底层窗口句柄
                HwndSource source = HwndSource.FromVisual(this) as HwndSource;
                if (source != null)
                {
                    // 添加消息钩子
                    source.AddHook(WndProcHook);
                    _hookInstalled = true;
                    Log.Debug("AutoCADTextBox消息钩子已安装");
                }
                else
                {
                    Log.Warning("无法获取HwndSource，消息钩子未安装");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "安装AutoCADTextBox消息钩子失败");
            }
        }

        /// <summary>
        /// 控件卸载时移除消息钩子
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_hookInstalled)
                {
                    return;
                }

                HwndSource source = HwndSource.FromVisual(this) as HwndSource;
                if (source != null)
                {
                    source.RemoveHook(WndProcHook);
                    _hookInstalled = false;
                    Log.Debug("AutoCADTextBox消息钩子已移除");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "移除AutoCADTextBox消息钩子失败");
            }
        }

        /// <summary>
        /// Windows消息处理钩子
        ///
        /// 关键：处理WM_GETDLGCODE和WM_CHAR消息
        /// 1. WM_GETDLGCODE：告诉系统此控件需要接收字符输入（包括中文IME）
        /// 2. WM_CHAR：防止父窗口重复处理字符（特别是空格键）
        /// </summary>
        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // ✅ 处理WM_GETDLGCODE消息：声明需要接收的输入类型
            if (msg == WM_GETDLGCODE)
            {
                // 标记消息已处理
                handled = true;

                // 返回组合标志，告诉系统此控件需要的输入类型
                int flags = DLGC_WANTCHARS | DLGC_WANTARROWS | DLGC_HASSETSEL;

                // 如果需要支持Tab键导航，取消下面这行的注释
                // flags |= DLGC_WANTTAB;

                Log.Verbose($"WM_GETDLGCODE: 返回标志 0x{flags:X}");

                return new IntPtr(flags);
            }

            // ✅ 处理WM_CHAR消息：防止父窗口重复处理空格等字符
            // 参考：https://stackoverflow.com/questions/835878/wpf-textbox-not-accepting-input-when-in-elementhost-in-window-forms
            if (msg == WM_CHAR && wParam.ToInt32() == 32)  // 32 = 空格键
            {
                handled = true;  // 阻止父窗口再次处理空格，避免重复输入
                Log.Verbose("WM_CHAR: 拦截空格键，防止重复输入");
            }

            // 其他消息交给默认处理
            return IntPtr.Zero;
        }
    }
}
