using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;

namespace BiaogPlugin.UI.Controls
{
    /// <summary>
    /// 自定义TextBox，解决在AutoCAD PaletteSet中中文输入法焦点跳转问题
    ///
    /// ✅ 重大修复 (2025-01-14)：
    /// 1. 增强焦点保持 - 监听PreviewTextInput/TextInput强制保持焦点
    /// 2. IME激活时锁定焦点 - 防止跳转到AutoCAD命令行
    /// 3. 强化Windows消息钩子
    ///
    /// 问题原因：
    /// WPF控件在AutoCAD的PaletteSet中通过ElementHost托管时，
    /// 输入中文IME激活时焦点会被AutoCAD命令行抢走
    ///
    /// 解决方案：
    /// 1. Hook HwndSource并处理WM_GETDLGCODE消息
    /// 2. 监听所有文本输入事件并强制保持焦点
    /// 3. PreviewLostKeyboardFocus中取消焦点丢失
    ///
    /// 参考：
    /// https://stackoverflow.com/questions/835878/wpf-textbox-not-accepting-input-when-in-elementhost-in-window-forms
    /// https://social.msdn.microsoft.com/Forums/vstudio/en-US/cfd20c98-a809-481c-8f68-34e473c182fa/keyboard-input-on-a-hwndsourcehosted-textbox
    /// </summary>
    public class AutoCADTextBox : TextBox
    {
        // Windows消息常量
        private const int WM_GETDLGCODE = 0x0087;
        private const int WM_CHAR = 0x0102;
        private const int WM_IME_SETCONTEXT = 0x0281;  // ✅ IME上下文切换
        private const int WM_IME_NOTIFY = 0x0282;       // ✅ IME通知消息

        // 对话框代码标志
        private const int DLGC_WANTCHARS = 0x0080;   // 接收字符输入（包括中文IME）
        private const int DLGC_WANTARROWS = 0x0001;  // 接收方向键
        private const int DLGC_HASSETSEL = 0x0008;   // 支持文本选择
        private const int DLGC_WANTTAB = 0x0002;     // 接收Tab键（可选）
        private const int DLGC_WANTALLKEYS = 0x0004; // 接收所有键盘输入

        private bool _hookInstalled = false;
        // ✅ 已移除 _isComposing - 不再需要焦点锁定逻辑

        public AutoCADTextBox() : base()
        {
            // 当控件加载完成时，安装Windows消息钩子
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // ✅ 极简修复：只保留Windows消息钩子支持中文输入
            // 完全移除焦点锁定逻辑，让焦点自然流动：点哪里焦点就到哪里

            Log.Debug("AutoCADTextBox已创建，中文输入支持已启用（无焦点锁定）");
        }

        // ✅ 极简修复完成：所有焦点锁定逻辑已移除
        // 现在只保留Windows消息钩子支持中文输入
        // 焦点自然流动：用户点击AI输入框→焦点到AI输入框，点击AutoCAD命令行→焦点到命令行

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
