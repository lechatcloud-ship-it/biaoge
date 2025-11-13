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
                if (_translationPaletteSet == null)
                {
                    InitializeTranslationPalette();
                }

                if (_translationPaletteSet != null)
                {
                    // ✅ 确保面板以停靠模式显示
                    if (_translationPaletteSet.Dock == DockSides.None)
                    {
                        _translationPaletteSet.Dock = DockSides.Right;
                    }

                    _translationPaletteSet.Visible = true;
                    _translationPaletteSet.Activate(0);  // 激活第一个选项卡
                    Log.Debug("翻译面板已显示（停靠在右侧）");
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
                    new System.Guid("A5B6C7D8-E9F0-1234-5678-9ABCDEF02222")
                )
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
                if (_calculationPaletteSet == null)
                {
                    InitializeCalculationPalette();
                }

                if (_calculationPaletteSet != null)
                {
                    // ✅ 确保面板以停靠模式显示
                    if (_calculationPaletteSet.Dock == DockSides.None)
                    {
                        _calculationPaletteSet.Dock = DockSides.Right;
                    }

                    _calculationPaletteSet.Visible = true;
                    _calculationPaletteSet.Activate(0);
                    Log.Debug("算量面板已显示（停靠在右侧）");
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
                        Child = _aiPalette,
                        AutoSize = true
                    };

                    // ✅ 添加控件到 PaletteSet
                    Log.Debug("添加控件到PaletteSet...");
                    _aiPaletteSet.Add("AI助手", elementHost);

                    // ✅ 关键修复：在Add之后设置Size和Dock
                    // 这样可以确保控件已经被添加到容器中
                    _aiPaletteSet.Size = new System.Drawing.Size(450, 850);
                    _aiPaletteSet.MinimumSize = new System.Drawing.Size(400, 600);
                    _aiPaletteSet.Dock = DockSides.Right;

                    // 保持隐藏，等待用户调用命令
                    _aiPaletteSet.Visible = false;

                    Log.Information("✓ AI助手面板创建成功（停靠右侧，尺寸: 450x850）");
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

                if (_aiPaletteSet == null)
                {
                    Log.Debug("AI助手面板未初始化，开始初始化...");
                    InitializeAIPalette();
                }

                if (_aiPaletteSet != null)
                {
                    Log.Debug($"AI助手面板状态检查: Visible={_aiPaletteSet.Visible}, Dock={_aiPaletteSet.Dock}, Size={_aiPaletteSet.Size}");

                    // ✅ 确保面板以停靠模式显示
                    if (_aiPaletteSet.Dock == DockSides.None)
                    {
                        Log.Debug("面板未停靠，设置为右侧停靠");
                        _aiPaletteSet.Dock = DockSides.Right;
                    }

                    // ✅ 确保尺寸正常（防止被最小化或隐藏）
                    if (_aiPaletteSet.Size.Width < 300 || _aiPaletteSet.Size.Height < 400)
                    {
                        Log.Warning($"检测到异常尺寸: {_aiPaletteSet.Size}，重置为默认尺寸");
                        _aiPaletteSet.Size = new System.Drawing.Size(450, 850);
                    }

                    // ✅ 设置为可见
                    _aiPaletteSet.Visible = true;
                    Log.Debug("面板Visible已设置为true");

                    // ✅ 关键修复：AutoCAD已知bug - 如果没有保存的设置，Tab不会被渲染
                    // 解决方案：在Visible=true后，程序化地调整两次Size（使用不同的值）强制渲染
                    // 参考：https://forums.autodesk.com/t5/net/custom-palette-display-issue/td-p/8228560
                    var targetSize = new System.Drawing.Size(450, 850);
                    var tempSize = new System.Drawing.Size(targetSize.Width + 10, targetSize.Height + 10);

                    _aiPaletteSet.Size = tempSize;
                    Log.Debug($"临时调整尺寸为: {tempSize}");

                    System.Threading.Thread.Sleep(50);  // 短暂延迟确保UI更新

                    _aiPaletteSet.Size = targetSize;
                    Log.Debug($"恢复目标尺寸为: {targetSize}");

                    // ✅ 激活面板（确保获得焦点）
                    _aiPaletteSet.Activate(0);
                    Log.Debug("面板已激活");

                    // ✅ 强制让面板获得焦点
                    try
                    {
                        if (_aiPaletteSet.Count > 0)
                        {
                            _aiPaletteSet.KeepFocus = true;
                            Log.Debug("面板焦点已锁定");
                        }
                    }
                    catch (System.Exception focusEx)
                    {
                        Log.Warning(focusEx, "锁定焦点失败（可忽略）");
                    }

                    Log.Information($"✓ AI助手面板已显示（停靠: {_aiPaletteSet.Dock}, 尺寸: {_aiPaletteSet.Size}, 可见: {_aiPaletteSet.Visible}）");
                }
                else
                {
                    Log.Error("AI助手面板初始化后仍为null");
                    throw new System.InvalidOperationException("无法创建AI助手面板");
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
