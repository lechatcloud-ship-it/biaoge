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

        private static TranslationPalette? _translationPalette;
        private static CalculationPalette? _calculationPalette;

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

                Log.Information("UI面板初始化完成");
            }
            catch (Exception ex)
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
                _translationPaletteSet.Add("翻译", _translationPalette);

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
            catch (Exception ex)
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
                _calculationPaletteSet.Add("算量", _calculationPalette);

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
            catch (Exception ex)
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

                Log.Information("UI面板清理完成");
            }
            catch (Exception ex)
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

        #endregion
    }
}
