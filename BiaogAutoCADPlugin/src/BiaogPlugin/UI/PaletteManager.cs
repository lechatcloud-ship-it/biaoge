using System;
using Autodesk.AutoCAD.Windows;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// AutoCAD工具面板管理器
    /// 管理所有PaletteSet的创建、显示和清理
    /// </summary>
    public static class PaletteManager
    {
        private static PaletteSet? _translationPaletteSet;
        private static PaletteSet? _calculationPaletteSet;
        private static PaletteSet? _aiPaletteSet;

        private static TranslationPalette? _translationPalette;
        private static CalculationPalette? _calculationPalette;
        private static AIPalette? _aiPalette;

        /// <summary>
        /// 初始化所有面板
        /// </summary>
        public static void Initialize()
        {
            try
            {
                Log.Information("初始化UI面板...");

                // 预创建面板（但不显示）
                InitializeTranslationPalette();
                InitializeCalculationPalette();
                InitializeAIPalette();

                Log.Information("UI面板初始化完成");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "初始化UI面板失败");
            }
        }

        #region 翻译面板

        /// <summary>
        /// 初始化翻译面板
        /// </summary>
        private static void InitializeTranslationPalette()
        {
            if (_translationPaletteSet == null)
            {
                _translationPaletteSet = new PaletteSet("标哥 - 翻译工具")
                {
                    Size = new System.Drawing.Size(380, 700),
                    MinimumSize = new System.Drawing.Size(350, 500),
                    DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                    Visible = false  // 初始不显示
                };

                _translationPalette = new TranslationPalette();

                // 使用ElementHost包装WPF控件
                var elementHost = new System.Windows.Forms.Integration.ElementHost
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Child = _translationPalette
                };

                _translationPaletteSet.Add("翻译", elementHost);

                // 设置样式
                _translationPaletteSet.Style = PaletteSetStyles.ShowPropertiesMenu |
                                               PaletteSetStyles.ShowAutoHideButton |
                                               PaletteSetStyles.ShowCloseButton;

                Log.Debug("翻译面板已创建");
            }
        }

        /// <summary>
        /// 显示翻译面板
        /// </summary>
        public static void ShowTranslationPalette()
        {
            try
            {
                if (_translationPaletteSet == null)
                {
                    InitializeTranslationPalette();
                }

                if (_translationPaletteSet != null)
                {
                    _translationPaletteSet.Visible = true;
                    _translationPaletteSet.Activate(0);  // 激活第一个选项卡
                    Log.Debug("翻译面板已显示");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示翻译面板失败");
                throw;
            }
        }

        /// <summary>
        /// 隐藏翻译面板
        /// </summary>
        public static void HideTranslationPalette()
        {
            if (_translationPaletteSet != null)
            {
                _translationPaletteSet.Visible = false;
                Log.Debug("翻译面板已隐藏");
            }
        }

        #endregion

        #region 算量面板

        /// <summary>
        /// 初始化算量面板
        /// </summary>
        private static void InitializeCalculationPalette()
        {
            if (_calculationPaletteSet == null)
            {
                _calculationPaletteSet = new PaletteSet("标哥 - 构件识别算量")
                {
                    Size = new System.Drawing.Size(400, 700),
                    MinimumSize = new System.Drawing.Size(350, 500),
                    DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                    Visible = false
                };

                _calculationPalette = new CalculationPalette();

                // 使用ElementHost包装WPF控件
                var elementHost2 = new System.Windows.Forms.Integration.ElementHost
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Child = _calculationPalette
                };

                _calculationPaletteSet.Add("算量", elementHost2);

                _calculationPaletteSet.Style = PaletteSetStyles.ShowPropertiesMenu |
                                               PaletteSetStyles.ShowAutoHideButton |
                                               PaletteSetStyles.ShowCloseButton;

                Log.Debug("算量面板已创建");
            }
        }

        /// <summary>
        /// 显示算量面板
        /// </summary>
        public static void ShowCalculationPalette()
        {
            try
            {
                if (_calculationPaletteSet == null)
                {
                    InitializeCalculationPalette();
                }

                if (_calculationPaletteSet != null)
                {
                    _calculationPaletteSet.Visible = true;
                    _calculationPaletteSet.Activate(0);
                    Log.Debug("算量面板已显示");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示算量面板失败");
                throw;
            }
        }

        /// <summary>
        /// 隐藏算量面板
        /// </summary>
        public static void HideCalculationPalette()
        {
            if (_calculationPaletteSet != null)
            {
                _calculationPaletteSet.Visible = false;
                Log.Debug("算量面板已隐藏");
            }
        }

        #endregion

        #region AI助手面板

        /// <summary>
        /// 初始化AI助手面板
        /// </summary>
        private static void InitializeAIPalette()
        {
            if (_aiPaletteSet == null)
            {
                _aiPaletteSet = new PaletteSet("标哥 - AI助手")
                {
                    // ✅ 增加默认高度，更适合AI对话界面
                    Size = new System.Drawing.Size(450, 850),
                    MinimumSize = new System.Drawing.Size(400, 600),
                    DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                    Visible = false
                };

                _aiPalette = new AIPalette();

                // 使用ElementHost包装WPF控件
                var elementHost = new System.Windows.Forms.Integration.ElementHost
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Child = _aiPalette
                };

                _aiPaletteSet.Add("AI助手", elementHost);

                _aiPaletteSet.Style = PaletteSetStyles.ShowPropertiesMenu |
                                      PaletteSetStyles.ShowAutoHideButton |
                                      PaletteSetStyles.ShowCloseButton;

                Log.Debug("AI助手面板已创建");
            }
        }

        /// <summary>
        /// 显示AI助手面板
        /// </summary>
        public static void ShowAIPalette()
        {
            try
            {
                if (_aiPaletteSet == null)
                {
                    InitializeAIPalette();
                }

                if (_aiPaletteSet != null)
                {
                    _aiPaletteSet.Visible = true;
                    _aiPaletteSet.Activate(0);
                    Log.Debug("AI助手面板已显示");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示AI助手面板失败");
                throw;
            }
        }

        /// <summary>
        /// 隐藏AI助手面板
        /// </summary>
        public static void HideAIPalette()
        {
            if (_aiPaletteSet != null)
            {
                _aiPaletteSet.Visible = false;
                Log.Debug("AI助手面板已隐藏");
            }
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清理所有面板
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                Log.Information("清理UI面板...");

                // 关闭并释放翻译面板
                if (_translationPaletteSet != null)
                {
                    _translationPaletteSet.Visible = false;
                    _translationPaletteSet.Dispose();
                    _translationPaletteSet = null;
                    _translationPalette = null;
                    Log.Debug("翻译面板已清理");
                }

                // 关闭并释放算量面板
                if (_calculationPaletteSet != null)
                {
                    _calculationPaletteSet.Visible = false;
                    _calculationPaletteSet.Dispose();
                    _calculationPaletteSet = null;
                    _calculationPalette = null;
                    Log.Debug("算量面板已清理");
                }

                // 关闭并释放AI助手面板
                if (_aiPaletteSet != null)
                {
                    _aiPaletteSet.Visible = false;
                    _aiPaletteSet.Dispose();
                    _aiPaletteSet = null;
                    _aiPalette = null;
                    Log.Debug("AI助手面板已清理");
                }

                Log.Information("UI面板清理完成");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "清理UI面板时发生错误");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查翻译面板是否可见
        /// </summary>
        public static bool IsTranslationPaletteVisible =>
            _translationPaletteSet?.Visible ?? false;

        /// <summary>
        /// 检查算量面板是否可见
        /// </summary>
        public static bool IsCalculationPaletteVisible =>
            _calculationPaletteSet?.Visible ?? false;

        /// <summary>
        /// 检查AI助手面板是否可见
        /// </summary>
        public static bool IsAIPaletteVisible =>
            _aiPaletteSet?.Visible ?? false;

        /// <summary>
        /// 切换翻译面板显示状态
        /// </summary>
        public static void ToggleTranslationPalette()
        {
            if (IsTranslationPaletteVisible)
            {
                HideTranslationPalette();
            }
            else
            {
                ShowTranslationPalette();
            }
        }

        /// <summary>
        /// 切换算量面板显示状态
        /// </summary>
        public static void ToggleCalculationPalette()
        {
            if (IsCalculationPaletteVisible)
            {
                HideCalculationPalette();
            }
            else
            {
                ShowCalculationPalette();
            }
        }

        /// <summary>
        /// 切换AI助手面板显示状态
        /// </summary>
        public static void ToggleAIPalette()
        {
            if (IsAIPaletteVisible)
            {
                HideAIPalette();
            }
            else
            {
                ShowAIPalette();
            }
        }

        #endregion
    }
}
