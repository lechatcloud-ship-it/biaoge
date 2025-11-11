using System;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 输入法管理器
    /// 自动切换中英文输入法，提升AutoCAD使用体验
    /// </summary>
    public static class InputMethodManager
    {
        // Windows API 导入
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        private static extern bool ImmGetConversionStatus(IntPtr hIMC, ref int lpfdwConversion, ref int lpfdwSentence);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetConversionStatus(IntPtr hIMC, int fdwConversion, int fdwSentence);

        // 输入法转换模式常量
        private const int IME_CMODE_ALPHANUMERIC = 0x0000;  // 英文模式
        private const int IME_CMODE_NATIVE = 0x0001;         // 中文模式

        private static bool _isEnabled = false;
        private static DocumentCollection? _docs;

        /// <summary>
        /// 启用自动输入法切换
        /// </summary>
        public static void Enable()
        {
            if (_isEnabled)
            {
                Log.Warning("输入法自动切换已启用，跳过重复启用");
                return;
            }

            try
            {
                _docs = Application.DocumentManager;

                // 监听命令事件
                var doc = _docs.MdiActiveDocument;
                if (doc != null)
                {
                    RegisterDocumentEvents(doc);
                }

                // 监听文档激活事件
                _docs.DocumentActivated += OnDocumentActivated;

                _isEnabled = true;
                Log.Information("输入法自动切换已启用");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启用输入法自动切换失败");
                throw;
            }
        }

        /// <summary>
        /// 禁用自动输入法切换
        /// </summary>
        public static void Disable()
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                if (_docs != null)
                {
                    _docs.DocumentActivated -= OnDocumentActivated;

                    // 为所有文档注销事件
                    foreach (Document doc in _docs)
                    {
                        UnregisterDocumentEvents(doc);
                    }
                }

                _isEnabled = false;
                Log.Information("输入法自动切换已禁用");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "禁用输入法自动切换失败");
            }
        }

        /// <summary>
        /// 文档激活事件
        /// </summary>
        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            try
            {
                if (e.Document != null)
                {
                    RegisterDocumentEvents(e.Document);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "注册文档事件失败");
            }
        }

        /// <summary>
        /// 注册文档事件
        /// </summary>
        private static void RegisterDocumentEvents(Document doc)
        {
            try
            {
                // 注销旧事件（避免重复）
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandEnded;
                doc.CommandFailed -= OnCommandEnded;

                // 注册新事件
                doc.CommandWillStart += OnCommandWillStart;
                doc.CommandEnded += OnCommandEnded;
                doc.CommandCancelled += OnCommandEnded;
                doc.CommandFailed += OnCommandEnded;

                Log.Debug($"已为文档 {doc.Name} 注册输入法切换事件");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"注册文档 {doc.Name} 事件失败");
            }
        }

        /// <summary>
        /// 注销文档事件
        /// </summary>
        private static void UnregisterDocumentEvents(Document doc)
        {
            try
            {
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandEnded;
                doc.CommandFailed -= OnCommandEnded;

                Log.Debug($"已为文档 {doc.Name} 注销输入法切换事件");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"注销文档 {doc.Name} 事件失败");
            }
        }

        /// <summary>
        /// 命令即将开始 - 切换到英文输入法
        /// </summary>
        private static void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            try
            {
                // 检查是否启用自动切换
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager == null || !configManager.Config.InputMethod.AutoSwitch)
                {
                    return;
                }

                // 切换到英文输入法
                SwitchToEnglish();
                Log.Debug($"命令开始: {e.GlobalCommandName}，已切换到英文输入法");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换输入法失败");
            }
        }

        /// <summary>
        /// 命令结束 - 恢复中文输入法
        /// </summary>
        private static void OnCommandEnded(object sender, CommandEventArgs e)
        {
            try
            {
                // 检查是否启用自动切换
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager == null || !configManager.Config.InputMethod.AutoSwitch)
                {
                    return;
                }

                // 某些文本编辑命令结束后切换到中文
                var commandName = e.GlobalCommandName.ToUpper();
                if (IsTextEditingCommand(commandName))
                {
                    SwitchToChinese();
                    Log.Debug($"文本编辑命令结束: {e.GlobalCommandName}，已切换到中文输入法");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换输入法失败");
            }
        }

        /// <summary>
        /// 判断是否为文本编辑命令
        /// </summary>
        private static bool IsTextEditingCommand(string commandName)
        {
            var textEditingCommands = new[]
            {
                "TEXT",      // 单行文本
                "DTEXT",     // 动态文本
                "MTEXT",     // 多行文本
                "MTEXTEDIT", // 编辑多行文本
                "EATTEDIT",  // 编辑块属性
                "ATTEDIT",   // 编辑属性
                "DDEDIT"     // 编辑文本
            };

            foreach (var cmd in textEditingCommands)
            {
                if (commandName.Contains(cmd))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 切换到英文输入法
        /// </summary>
        public static void SwitchToEnglish()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                IntPtr hIMC = ImmGetContext(hWnd);
                if (hIMC == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    // 设置为英文模式
                    ImmSetConversionStatus(hIMC, IME_CMODE_ALPHANUMERIC, 0);
                    Log.Verbose("已切换到英文输入法");
                }
                finally
                {
                    ImmReleaseContext(hWnd, hIMC);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换到英文输入法失败");
            }
        }

        /// <summary>
        /// 切换到中文输入法
        /// </summary>
        public static void SwitchToChinese()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                IntPtr hIMC = ImmGetContext(hWnd);
                if (hIMC == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    // 设置为中文模式
                    ImmSetConversionStatus(hIMC, IME_CMODE_NATIVE, 0);
                    Log.Verbose("已切换到中文输入法");
                }
                finally
                {
                    ImmReleaseContext(hWnd, hIMC);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换到中文输入法失败");
            }
        }

        /// <summary>
        /// 获取当前输入法状态
        /// </summary>
        public static bool IsChineseMode()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr hIMC = ImmGetContext(hWnd);
                if (hIMC == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    int conversionMode = 0;
                    int sentenceMode = 0;
                    ImmGetConversionStatus(hIMC, ref conversionMode, ref sentenceMode);

                    return (conversionMode & IME_CMODE_NATIVE) != 0;
                }
                finally
                {
                    ImmReleaseContext(hWnd, hIMC);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取输入法状态失败");
                return false;
            }
        }

        /// <summary>
        /// 检查是否启用
        /// </summary>
        public static bool IsEnabled => _isEnabled;
    }
}
