using System;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using Serilog;

namespace BiaogPlugin.UI.Ribbon
{
    /// <summary>
    /// Ribbon工具栏管理器
    /// 负责创建和管理AutoCAD Ribbon界面
    /// </summary>
    public static class RibbonManager
    {
        private static RibbonTab? _biaogTab;
        private static bool _isLoaded = false;
        private static bool _isInitializing = false;
        private static bool _workspaceEventRegistered = false;

        /// <summary>
        /// 加载Ribbon工具栏
        /// </summary>
        public static void LoadRibbon()
        {
            if (_isInitializing)
            {
                Log.Warning("Ribbon正在初始化，跳过");
                return;
            }

            try
            {
                Log.Information("正在加载Ribbon工具栏...");

                // ✅ 关键修复：注册工作空间切换事件（只注册一次）
                if (!_workspaceEventRegistered)
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.SystemVariableChanged += OnSystemVariableChanged;
                    _workspaceEventRegistered = true;
                    Log.Debug("已注册SystemVariableChanged事件监听");
                }

                // ✅ 使用Application.Idle事件延迟加载，确保Ribbon完全初始化
                Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnIdleLoadRibbon;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "加载Ribbon工具栏失败");
                _isInitializing = false;
            }
        }

        /// <summary>
        /// ✅ Application.Idle事件处理：延迟加载Ribbon
        /// 参考AutoCAD官方最佳实践：在Idle事件中检查Ribbon是否为null
        /// https://forums.autodesk.com/t5/net/getting-componentmanager-ribbon-as-null-for-autocad-2020
        /// </summary>
        private static void OnIdleLoadRibbon(object? sender, System.EventArgs e)
        {
            try
            {
                // ✅ 关键修复：在Ribbon完全就绪之前不移除Idle事件
                if (ComponentManager.Ribbon == null)
                {
                    Log.Debug("Ribbon尚未就绪，等待下一个Idle周期");
                    return;  // 保持Idle事件订阅，等待下一次触发
                }

                // Ribbon已就绪，移除Idle事件
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleLoadRibbon;
                Log.Debug("Ribbon已就绪，开始创建");

                CreateRibbon();
            }
            catch (System.Exception ex)
            {
                // 发生错误时也要移除事件，避免无限循环
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleLoadRibbon;
                Log.Error(ex, "Idle加载Ribbon失败");
                _isInitializing = false;
            }
        }

        /// <summary>
        /// ✅ 工作空间切换事件处理：重新创建Ribbon确保显示
        /// </summary>
        private static void OnSystemVariableChanged(object? sender, Autodesk.AutoCAD.ApplicationServices.SystemVariableChangedEventArgs e)
        {
            try
            {
                // 监听WSCURRENT变量（当前工作空间）
                if (e.Name.Equals("WSCURRENT", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug($"工作空间已切换，重新创建Ribbon");

                    // ✅ 检查是否Classic工作空间（Ribbon被禁用）
                    if (ComponentManager.Ribbon == null)
                    {
                        Log.Warning("当前工作空间不支持Ribbon（可能是Classic模式）");
                        _isLoaded = false;
                        return;
                    }

                    // 重新创建Ribbon Tab
                    CreateRibbon();
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "处理工作空间切换事件失败");
            }
        }

        /// <summary>
        /// 处理Ribbon初始化完成事件
        /// </summary>
        private static void ComponentManager_ItemInitialized(object? sender, RibbonItemEventArgs e)
        {
            // Ribbon已初始化，创建Tab
            if (ComponentManager.Ribbon != null)
            {
                ComponentManager.ItemInitialized -= ComponentManager_ItemInitialized;
                _isInitializing = false;
                CreateRibbon();
            }
        }

        /// <summary>
        /// 创建Ribbon工具栏（AutoCAD主线程上调用）
        /// </summary>
        private static void CreateRibbon()
        {
            try
            {
                RibbonControl ribbonControl = ComponentManager.Ribbon;
                if (ribbonControl == null)
                {
                    Log.Error("无法获取Ribbon控件");
                    return;
                }

                // 检查是否已存在标哥Tab
                foreach (RibbonTab tab in ribbonControl.Tabs)
                {
                    if (tab.Id == "BIAOGE_TAB")
                    {
                        Log.Information("Ribbon Tab已存在，移除旧Tab");
                        ribbonControl.Tabs.Remove(tab);
                        break;
                    }
                }

                // 创建标哥Tab
                _biaogTab = new RibbonTab
                {
                    Title = "标哥工具",
                    Id = "BIAOGE_TAB"
                };

                // 添加面板
                CreateTranslationPanel(_biaogTab);
                CreateAIPanel(_biaogTab);
                CreateCalculationPanel(_biaogTab);
                CreateSettingsPanel(_biaogTab);

                // 添加Tab到Ribbon
                ribbonControl.Tabs.Add(_biaogTab);

                _isLoaded = true;
                Log.Information("Ribbon工具栏已创建");

                // ✅ 关键修复：立即尝试激活，然后再用Idle事件确保激活
                // 某些情况下立即激活会成功，某些情况下需要延迟
                try
                {
                    _biaogTab.IsActive = true;
                    ribbonControl.ActiveTab = _biaogTab;
                    Log.Debug("尝试立即激活Ribbon Tab");
                }
                catch (System.Exception ex)
                {
                    Log.Debug(ex, "立即激活失败，将使用Idle延迟激活");
                }

                // ✅ 使用Idle事件延迟激活Tab（确保显示）
                // 原因：AutoCAD启动时，Ribbon可能还未完全就绪，直接设置IsActive会被覆盖
                // 解决方案：等待AutoCAD进入空闲状态（完全启动完成）后再激活
                Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnIdleActivateTab;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "创建Ribbon失败: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// ✅ AutoCAD空闲时激活标哥Tab（可能需要多次尝试）
        /// </summary>
        private static int _activateAttempts = 0;
        private static void OnIdleActivateTab(object? sender, System.EventArgs e)
        {
            try
            {
                _activateAttempts++;

                if (_biaogTab != null && ComponentManager.Ribbon != null)
                {
                    // 检查Tab是否已经激活
                    if (_biaogTab.IsActive)
                    {
                        // 移除事件处理器
                        Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleActivateTab;
                        Log.Information($"✅ Ribbon Tab已激活（尝试{_activateAttempts}次）");
                        return;
                    }

                    // ✅ 关键修复：多次设置确保激活
                    // 1. 设置Tab的IsActive属性
                    _biaogTab.IsActive = true;

                    // 2. 设置RibbonControl的ActiveTab属性
                    ComponentManager.Ribbon.ActiveTab = _biaogTab;

                    Log.Debug($"尝试激活Ribbon Tab（第{_activateAttempts}次）: IsActive={_biaogTab.IsActive}");

                    // 最多尝试5次
                    if (_activateAttempts >= 5)
                    {
                        Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleActivateTab;
                        Log.Warning($"Ribbon Tab激活失败，已尝试{_activateAttempts}次");
                    }
                }
                else
                {
                    // 如果Ribbon或Tab还不可用，继续等待
                    if (_activateAttempts >= 10)
                    {
                        Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleActivateTab;
                        Log.Error("Ribbon Tab激活失败：Ribbon或Tab不可用");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleActivateTab;
                Log.Error(ex, "激活Ribbon Tab失败: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// 卸载Ribbon工具栏
        /// </summary>
        public static void UnloadRibbon()
        {
            try
            {
                // 移除Idle事件（如果还未触发）
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleActivateTab;
            }
            catch { /* 忽略移除事件的错误 */ }

            if (!_isLoaded || _biaogTab == null)
            {
                return;
            }

            try
            {
                // 直接在AutoCAD主线程上操作Ribbon
                RibbonControl ribbonControl = ComponentManager.Ribbon;
                if (ribbonControl != null && ribbonControl.Tabs.Contains(_biaogTab))
                {
                    ribbonControl.Tabs.Remove(_biaogTab);
                }

                _biaogTab = null;
                _isLoaded = false;
                Log.Information("Ribbon工具栏卸载成功");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "卸载Ribbon工具栏失败");
            }
        }

        /// <summary>
        /// 创建翻译面板
        /// </summary>
        private static void CreateTranslationPanel(RibbonTab tab)
        {
            RibbonPanelSource panelSource = new RibbonPanelSource
            {
                Title = "AI翻译"
            };

            RibbonPanel panel = new RibbonPanel
            {
                Source = panelSource
            };

            // === 大按钮：翻译图纸 ===
            RibbonButton translateBtn = new RibbonButton
            {
                Text = "翻译图纸",
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandParameter = "BIAOGE_TRANSLATE_ZH ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "一键翻译整个图纸为简体中文\n使用qwen-mt-flash模型\n支持92种语言识别"
            };

            panelSource.Items.Add(translateBtn);

            // === 第二行：清除缓存 ===
            RibbonRowPanel row = new RibbonRowPanel();

            RibbonButton clearCache = new RibbonButton
            {
                Text = "清除缓存",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_CLEARCACHE ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "清除翻译缓存数据库"
            };
            row.Items.Add(clearCache);

            panelSource.Items.Add(row);

            tab.Panels.Add(panel);
        }

        /// <summary>
        /// 创建AI助手面板
        /// </summary>
        private static void CreateAIPanel(RibbonTab tab)
        {
            RibbonPanelSource panelSource = new RibbonPanelSource
            {
                Title = "AI助手"
            };

            RibbonPanel panel = new RibbonPanel
            {
                Source = panelSource
            };

            // === 大按钮：AI助手 ===
            RibbonButton aiAssistant = new RibbonButton
            {
                Text = "AI助手",
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandParameter = "BIAOGE_AI ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "启动标哥AI助手\n核心: qwen3-max-preview\n智能调用专用模型"
            };

            panelSource.Items.Add(aiAssistant);

            tab.Panels.Add(panel);
        }

        /// <summary>
        /// 创建算量面板
        /// </summary>
        private static void CreateCalculationPanel(RibbonTab tab)
        {
            RibbonPanelSource panelSource = new RibbonPanelSource
            {
                Title = "工程算量"
            };

            RibbonPanel panel = new RibbonPanel
            {
                Source = panelSource
            };

            // === 大按钮：智能算量 ===
            RibbonButton calculate = new RibbonButton
            {
                Text = "智能算量",
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandParameter = "BIAOGE_CALCULATE ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "打开算量面板\n智能识别构件\nAI辅助统计"
            };

            panelSource.Items.Add(calculate);

            // === 第二行：小按钮组 ===
            RibbonRowPanel row = new RibbonRowPanel();

            // 快速统计
            RibbonButton quickCount = new RibbonButton
            {
                Text = "快速统计",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_QUICKCOUNT ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "快速统计构件数量"
            };
            row.Items.Add(quickCount);

            // 导出Excel
            RibbonButton exportExcel = new RibbonButton
            {
                Text = "导出Excel",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_EXPORTEXCEL ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "快速导出Excel工程量清单"
            };
            row.Items.Add(exportExcel);

            panelSource.Items.Add(row);

            tab.Panels.Add(panel);
        }

        /// <summary>
        /// 创建设置面板
        /// </summary>
        private static void CreateSettingsPanel(RibbonTab tab)
        {
            RibbonPanelSource panelSource = new RibbonPanelSource
            {
                Title = "设置"
            };

            RibbonPanel panel = new RibbonPanel
            {
                Source = panelSource
            };

            // === 第一行 ===
            RibbonRowPanel row1 = new RibbonRowPanel();

            // 插件设置
            RibbonButton settings = new RibbonButton
            {
                Text = "插件设置",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_SETTINGS ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "打开设置对话框\n配置API密钥和参数"
            };
            row1.Items.Add(settings);

            // 快捷键设置
            RibbonButton keys = new RibbonButton
            {
                Text = "快捷键设置",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_KEYS ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "查看和管理快捷键"
            };
            row1.Items.Add(keys);

            panelSource.Items.Add(row1);

            // === 第二行 ===
            RibbonRowPanel row2 = new RibbonRowPanel();

            // 查看帮助
            RibbonButton help = new RibbonButton
            {
                Text = "查看帮助",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_HELP ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "显示帮助信息"
            };
            row2.Items.Add(help);

            // 关于工具
            RibbonButton about = new RibbonButton
            {
                Text = "关于工具",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_ABOUT ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "关于标哥插件"
            };
            row2.Items.Add(about);

            panelSource.Items.Add(row2);

            tab.Panels.Add(panel);
        }
    }

    /// <summary>
    /// Ribbon命令处理器
    /// 将Ribbon按钮点击转换为AutoCAD命令执行
    /// </summary>
    public class RibbonCommandHandler : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            try
            {
                // 获取活动文档
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    Log.Warning("无法获取活动文档");
                    return;
                }

                // 从CommandParameter获取命令字符串
                string commandStr = string.Empty;

                // parameter 可能是字符串（CommandParameter）或 RibbonButton
                if (parameter is string str)
                {
                    commandStr = str;
                }
                else if (parameter is Autodesk.Windows.RibbonButton ribbonBtn && ribbonBtn.CommandParameter is string cmdParam)
                {
                    commandStr = cmdParam;
                }

                if (string.IsNullOrWhiteSpace(commandStr))
                {
                    Log.Warning($"无效的命令参数: {parameter?.GetType().Name}");
                    doc.Editor.WriteMessage("\n[警告] Ribbon按钮没有有效的命令参数");
                    return;
                }

                // 清理命令字符串（移除末尾空格）
                string cleanCommand = commandStr.Trim();

                // ✅ 重要修复：对于 SendStringToExecute，使用 \x03 而不是 ^C^C
                // \x03 是 ASCII 码 3（Ctrl+C），用于取消当前命令
                // 参考：https://adndevblog.typepad.com/autocad/2012/07/start-command-with-escape-characters-cc.html
                // 注意：^C^C 是 CUI 宏语法，不适用于 .NET SendStringToExecute
                string fullCommand = "\x03\x03" + cleanCommand + " ";

                Log.Information($"Ribbon执行命令: {cleanCommand}");

                // 发送命令到AutoCAD命令行
                // 参数说明：
                // - command: 要执行的命令字符串
                // - activate: true = 激活文档窗口
                // - wrapUpInactiveDoc: false = 不等待非活动文档
                // - echoCommand: true = 在命令行显示命令
                doc.SendStringToExecute(fullCommand, true, false, true);

                // 在命令行确认
                doc.Editor.WriteMessage($"\n[Ribbon] → {cleanCommand}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Ribbon命令执行失败");

                // 显示错误消息
                try
                {
                    var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        doc.Editor.WriteMessage($"\n[错误] Ribbon命令执行失败: {ex.Message}");
                    }
                }
                catch { }
            }
        }
    }
}
