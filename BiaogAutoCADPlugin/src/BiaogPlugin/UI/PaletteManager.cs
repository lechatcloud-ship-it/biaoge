using System;
using Autodesk.AutoCAD.ApplicationServices;
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
                // ✅ 使用 GUID 构造函数以持久化面板位置和大小设置
                _translationPaletteSet = new PaletteSet(
                    "标哥 - 翻译工具",
                    new System.Guid("A5B6C7D8-E9F0-1234-5678-9ABCDEF01111")
                )
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

                // ✅ 设置默认停靠位置为右侧
                _translationPaletteSet.Dock = DockSides.Right;

                Log.Debug("翻译面板已创建（默认停靠在右侧）");
            }
        }

        /// <summary>
        /// 显示翻译面板
        /// </summary>
        public static void ShowTranslationPalette()
        {
            try
            {
                Log.Debug("准备显示翻译面板...");

                // ✅ 关键修复：第一次初始化时需要特殊处理
                bool isFirstTime = (_translationPaletteSet == null);

                if (isFirstTime)
                {
                    Log.Debug("翻译面板未初始化，开始初始化...");
                    InitializeTranslationPalette();
                }

                if (_translationPaletteSet != null)
                {
                    // ✅ 优化：首次显示时强制使用预设配置
                    if (isFirstTime)
                    {
                        Log.Information("首次显示翻译面板，强制使用预设配置：停靠右侧");

                        // 强制停靠到右侧
                        _translationPaletteSet.Dock = DockSides.Right;

                        // 使用合理的初始尺寸（高度使用屏幕90%）
                        var screen = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                        int idealHeight = (int)(screen.Height * 0.9);
                        _translationPaletteSet.Size = new System.Drawing.Size(380, idealHeight);

                        Log.Information($"✓ 已应用首次显示配置：Dock=Right, Size=380x{idealHeight}");
                    }
                    else
                    {
                        // ✅ 非首次显示：只在浮动时修正为停靠
                        if (_translationPaletteSet.Dock == DockSides.None)
                        {
                            Log.Debug("面板处于浮动状态，修正为停靠右侧");
                            _translationPaletteSet.Dock = DockSides.Right;
                        }
                    }

                    // ✅ 设置可见并激活
                    _translationPaletteSet.Visible = true;
                    _translationPaletteSet.Activate(0);

                    // ✅ 首次显示时执行双重resize触发标签页渲染
                    if (isFirstTime)
                    {
                        Log.Debug("首次显示，执行双重resize触发标签页渲染...");
                        var tempSize = _translationPaletteSet.Size;
                        _translationPaletteSet.Size = new System.Drawing.Size(tempSize.Width + 1, tempSize.Height + 1);
                        System.Threading.Thread.Sleep(10);
                        _translationPaletteSet.Size = tempSize;
                    }

                    // ✅ 修复中文输入法焦点跳转：KeepFocus=true保持焦点在面板内
                    _translationPaletteSet.KeepFocus = true;

                    Log.Information($"✓ 翻译面板已显示（Dock={_translationPaletteSet.Dock}, Size={_translationPaletteSet.Size}, Visible={_translationPaletteSet.Visible}）");
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
                // ✅ 使用 GUID 构造函数以持久化面板位置和大小设置
                _calculationPaletteSet = new PaletteSet(
                    "标哥 - 构件识别算量",
                    new System.Guid("A5B6C7D8-E9F0-1234-5678-9ABCDEF02223")
                )
                {
                    Size = new System.Drawing.Size(420, 700),
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

                // ✅ 设置默认停靠位置为右侧
                _calculationPaletteSet.Dock = DockSides.Right;

                Log.Debug("算量面板已创建（默认停靠在右侧）");
            }
        }

        /// <summary>
        /// 显示算量面板
        /// </summary>
        public static void ShowCalculationPalette()
        {
            try
            {
                Log.Debug("准备显示算量面板...");

                // ✅ 关键修复：第一次初始化时需要特殊处理
                bool isFirstTime = (_calculationPaletteSet == null);

                if (isFirstTime)
                {
                    Log.Debug("算量面板未初始化，开始初始化...");
                    InitializeCalculationPalette();
                }

                if (_calculationPaletteSet != null)
                {
                    // ✅ 优化：首次显示时强制使用预设配置
                    if (isFirstTime)
                    {
                        Log.Information("首次显示算量面板，强制使用预设配置：停靠右侧");

                        // 强制停靠到右侧
                        _calculationPaletteSet.Dock = DockSides.Right;

                        // 使用合理的初始尺寸（高度使用屏幕90%）
                        var screen = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                        int idealHeight = (int)(screen.Height * 0.9);
                        _calculationPaletteSet.Size = new System.Drawing.Size(420, idealHeight);

                        Log.Information($"✓ 已应用首次显示配置：Dock=Right, Size=420x{idealHeight}");
                    }
                    else
                    {
                        // ✅ 非首次显示：只在浮动时修正为停靠
                        if (_calculationPaletteSet.Dock == DockSides.None)
                        {
                            Log.Debug("面板处于浮动状态，修正为停靠右侧");
                            _calculationPaletteSet.Dock = DockSides.Right;
                        }
                    }

                    // ✅ 设置可见并激活
                    _calculationPaletteSet.Visible = true;
                    _calculationPaletteSet.Activate(0);

                    // ✅ 首次显示时执行双重resize触发标签页渲染
                    if (isFirstTime)
                    {
                        Log.Debug("首次显示，执行双重resize触发标签页渲染...");
                        var tempSize = _calculationPaletteSet.Size;
                        _calculationPaletteSet.Size = new System.Drawing.Size(tempSize.Width + 1, tempSize.Height + 1);
                        System.Threading.Thread.Sleep(10);
                        _calculationPaletteSet.Size = tempSize;
                    }

                    // ✅ 修复中文输入法焦点跳转：KeepFocus=true保持焦点在面板内
                    _calculationPaletteSet.KeepFocus = true;

                    Log.Information($"✓ 算量面板已显示（Dock={_calculationPaletteSet.Dock}, Size={_calculationPaletteSet.Size}, Visible={_calculationPaletteSet.Visible}）");
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
                try
                {
                    Log.Debug("开始创建AI助手面板...");

                    // ✅ 使用 GUID 构造函数以持久化面板位置和大小设置
                    _aiPaletteSet = new PaletteSet(
                        "标哥 - AI助手",
                        new System.Guid("A5B6C7D8-E9F0-1234-5678-9ABCDEF03333")
                    );

                    Log.Debug("PaletteSet已创建，准备设置属性...");

                    // ✅ 关键修复：先设置Style，再设置Size
                    // 参考AutoCAD文档：某些属性必须在Add之前设置
                    _aiPaletteSet.Style = PaletteSetStyles.ShowPropertiesMenu |
                                          PaletteSetStyles.ShowAutoHideButton |
                                          PaletteSetStyles.ShowCloseButton;

                    // 设置停靠能力
                    _aiPaletteSet.DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right);

                    // 创建 WPF 控件
                    Log.Debug("创建AIPalette WPF控件...");
                    _aiPalette = new AIPalette();

                    // 使用ElementHost包装WPF控件
                    Log.Debug("创建ElementHost...");
                    var elementHost = new System.Windows.Forms.Integration.ElementHost
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        Child = _aiPalette
                        // ❌ 删除 AutoSize = true（与Dock冲突，导致尺寸为0）
                    };

                    // ✅ 添加控件到 PaletteSet
                    Log.Debug("添加控件到PaletteSet...");
                    _aiPaletteSet.Add("AI助手", elementHost);

                    // ✅ 关键修复：在Add之后设置Size
                    // ❌ 不要强制设置Dock=Right，会导致面板无法显示（AutoCAD Bug）
                    // ✅ 正确做法：保持Dock=None（浮动模式），让用户自己拖动到右侧停靠
                    _aiPaletteSet.Size = new System.Drawing.Size(850, 800);  // 宽度850, 高度800
                    _aiPaletteSet.MinimumSize = new System.Drawing.Size(700, 600);  // 最小宽度700, 最小高度600
                    // _aiPaletteSet.Dock = DockSides.Right;  // ❌ 删除强制停靠，避免面板无法显示

                    // 保持隐藏，等待用户调用命令
                    _aiPaletteSet.Visible = false;

                    Log.Information("✓ AI助手面板创建成功（停靠右侧，尺寸: 850x800）");
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "创建AI助手面板失败");
                    throw;
                }
            }
        }

        /// <summary>
        /// 显示AI助手面板
        /// </summary>
        public static void ShowAIPalette()
        {
            try
            {
                Log.Debug("准备显示AI助手面板...");

                // ✅ 关键修复：检测是否是第一次初始化
                bool isFirstTime = (_aiPaletteSet == null);

                if (isFirstTime)
                {
                    Log.Debug("AI助手面板未初始化，开始初始化...");
                    InitializeAIPalette();
                }

                // ✅ 确保初始化成功
                if (_aiPaletteSet == null)
                {
                    Log.Error("AI助手面板初始化失败");
                    throw new System.InvalidOperationException("无法创建AI助手面板");
                }

                Log.Debug($"AI助手面板当前状态: Visible={_aiPaletteSet.Visible}, Dock={_aiPaletteSet.Dock}, Size={_aiPaletteSet.Size}");

                // ✅ 关键修复：不强制设置Dock，只确保尺寸合理
                // ❌ 删除强制Dock=Right逻辑（会导致面板无法显示）
                // ✅ 让用户自己拖动到右侧停靠，AutoCAD会自动保存位置

                // 检查尺寸是否过小，如果过小则修复
                var currentSize = _aiPaletteSet.Size;
                if (currentSize.Width < 700 || currentSize.Height < 600)
                {
                    Log.Warning($"检测到异常尺寸: {currentSize}，修复为850x800");
                    _aiPaletteSet.Size = new System.Drawing.Size(850, 800);
                }

                // ✅ 设置可见并激活
                _aiPaletteSet.Visible = true;
                _aiPaletteSet.Activate(0);

                // ✅ 修复中文输入法焦点跳转：KeepFocus=true保持焦点在面板内
                // 防止输入中文时焦点跳转到AutoCAD命令行
                _aiPaletteSet.KeepFocus = true;

                // ✅ 首次显示时执行双重resize触发标签页渲染（修复AutoCAD PaletteSet Bug）
                if (isFirstTime)
                {
                    Log.Debug("首次显示，执行双重resize触发标签页渲染...");
                    var tempSize = _aiPaletteSet.Size;
                    _aiPaletteSet.Size = new System.Drawing.Size(tempSize.Width + 1, tempSize.Height + 1);
                    System.Threading.Thread.Sleep(10);  // 短暂延迟确保UI更新
                    _aiPaletteSet.Size = tempSize;  // 恢复原始尺寸
                }

                Log.Information($"✓ AI助手面板已显示（Dock={_aiPaletteSet.Dock}, Size={_aiPaletteSet.Size}, Visible={_aiPaletteSet.Visible}, Count={_aiPaletteSet.Count}）");
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
