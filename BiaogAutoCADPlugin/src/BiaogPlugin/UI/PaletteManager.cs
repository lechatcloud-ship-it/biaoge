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

                    // ✅ 第一次创建后，调整Size来触发渲染（不隐藏面板）
                    if (_translationPaletteSet != null)
                    {
                        Log.Debug("第一次创建，执行Size调整触发渲染...");

                        // 调整两次Size（不同值）触发UI布局计算
                        var tempSize = new System.Drawing.Size(410, 610);
                        _translationPaletteSet.Size = tempSize;

                        // ❌ 修复：删除Toggle Visible逻辑，避免首次调用不显示
                        // _translationPaletteSet.Visible = true;
                        // _translationPaletteSet.Visible = false;

                        Log.Debug("强制渲染完成");
                    }
                }

                if (_translationPaletteSet != null)
                {
                    // ✅ 确保面板以停靠模式显示
                    if (_translationPaletteSet.Dock == DockSides.None)
                    {
                        _translationPaletteSet.Dock = DockSides.Right;
                    }

                    // ✅ 简化的显示逻辑：直接设置可见并激活
                    _translationPaletteSet.Visible = true;
                    _translationPaletteSet.Activate(0);  // 激活第一个选项卡

                    // ✅ 修复问题7：KeepFocus=false允许焦点切换到AutoCAD命令行
                    // 用户可以点击AutoCAD窗口切换焦点，不会被强制保持在面板
                    _translationPaletteSet.KeepFocus = false;

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
                Log.Debug("准备显示算量面板...");

                // ✅ 关键修复：第一次初始化时需要特殊处理
                bool isFirstTime = (_calculationPaletteSet == null);

                if (isFirstTime)
                {
                    Log.Debug("算量面板未初始化，开始初始化...");
                    InitializeCalculationPalette();

                    // ✅ 第一次创建后，调整Size来触发渲染（不隐藏面板）
                    if (_calculationPaletteSet != null)
                    {
                        Log.Debug("第一次创建，执行Size调整触发渲染...");

                        // 调整两次Size（不同值）触发UI布局计算
                        var tempSize = new System.Drawing.Size(510, 710);
                        _calculationPaletteSet.Size = tempSize;

                        // ❌ 修复：删除Toggle Visible逻辑，避免首次调用不显示
                        // _calculationPaletteSet.Visible = true;
                        // _calculationPaletteSet.Visible = false;

                        Log.Debug("强制渲染完成");
                    }
                }

                if (_calculationPaletteSet != null)
                {
                    // ✅ 确保面板以停靠模式显示
                    if (_calculationPaletteSet.Dock == DockSides.None)
                    {
                        _calculationPaletteSet.Dock = DockSides.Right;
                    }

                    // ✅ 简化的显示逻辑：直接设置可见并激活
                    _calculationPaletteSet.Visible = true;
                    _calculationPaletteSet.Activate(0);

                    // ✅ 修复问题7：KeepFocus=false允许焦点切换到AutoCAD命令行
                    _calculationPaletteSet.KeepFocus = false;

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
                        Child = _aiPalette,
                        AutoSize = true
                    };

                    // ✅ 添加控件到 PaletteSet
                    Log.Debug("添加控件到PaletteSet...");
                    _aiPaletteSet.Add("AI助手", elementHost);

                    // ✅ 关键修复：在Add之后设置Size和Dock
                    // 这样可以确保控件已经被添加到容器中
                    _aiPaletteSet.Size = new System.Drawing.Size(800, 850);
                    _aiPaletteSet.MinimumSize = new System.Drawing.Size(600, 700);
                    _aiPaletteSet.Dock = DockSides.Right;

                    // 保持隐藏，等待用户调用命令
                    _aiPaletteSet.Visible = false;

                    Log.Information("✓ AI助手面板创建成功（停靠右侧，尺寸: 800x850）");
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

                // ✅ 关键修复：第一次初始化时需要特殊处理
                bool isFirstTime = (_aiPaletteSet == null);

                if (isFirstTime)
                {
                    Log.Debug("AI助手面板未初始化，开始初始化...");
                    InitializeAIPalette();

                    // ✅ 第一次创建后，调整Size来触发渲染（不隐藏面板）
                    if (_aiPaletteSet != null)
                    {
                        Log.Debug("第一次创建，执行Size调整触发渲染...");

                        // 调整两次Size（不同值）触发UI布局计算
                        var tempSize = new System.Drawing.Size(810, 860);
                        _aiPaletteSet.Size = tempSize;

                        // ❌ 修复：删除Toggle Visible逻辑，避免首次调用不显示
                        // _aiPaletteSet.Visible = true;
                        // _aiPaletteSet.Visible = false;

                        Log.Debug("强制渲染完成");
                    }
                }

                if (_aiPaletteSet != null)
                {
                    Log.Debug($"AI助手面板状态: Visible={_aiPaletteSet.Visible}, Dock={_aiPaletteSet.Dock}, Size={_aiPaletteSet.Size}");

                    // ✅ 按照AutoCAD官方最佳实践：先Size，后Dock
                    // 参考：https://stackoverflow.com/questions/23372182
                    var targetSize = new System.Drawing.Size(800, 850);

                    // 第一步：设置Size（调整两次，不同值，触发渲染）
                    _aiPaletteSet.Size = new System.Drawing.Size(810, 860);
                    _aiPaletteSet.Size = targetSize;

                    // 第二步：设置Dock（在Size之后）
                    _aiPaletteSet.Dock = DockSides.Right;

                    // 第三步：✅ 修复问题7：KeepFocus=false允许焦点切换到AutoCAD命令行
                    _aiPaletteSet.KeepFocus = false;

                    // 第四步：显示并激活
                    _aiPaletteSet.Visible = true;
                    _aiPaletteSet.Activate(0);

                    Log.Information($"✓ AI助手面板已显示（Dock={_aiPaletteSet.Dock}, Size={_aiPaletteSet.Size}, Visible={_aiPaletteSet.Visible}）");
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
