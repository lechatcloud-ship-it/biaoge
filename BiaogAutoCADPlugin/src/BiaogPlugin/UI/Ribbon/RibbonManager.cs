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

        /// <summary>
        /// 加载Ribbon工具栏
        /// </summary>
        public static void LoadRibbon()
        {
            if (_isLoaded || _isInitializing)
            {
                Log.Warning("Ribbon已加载或正在初始化，跳过");
                return;
            }

            try
            {
                Log.Information("正在加载Ribbon工具栏...");

                // 检查Ribbon是否可用
                if (ComponentManager.Ribbon == null)
                {
                    // Ribbon未就绪，注册事件延迟加载
                    Log.Information("Ribbon未就绪，注册延迟加载事件");
                    _isInitializing = true;
                    ComponentManager.ItemInitialized += ComponentManager_ItemInitialized;
                }
                else
                {
                    // Ribbon已就绪，直接创建
                    CreateRibbon();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "加载Ribbon工具栏失败");
                _isInitializing = false;
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

                // ✅ 关键修复：使用Idle事件延迟激活Tab
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
        /// AutoCAD空闲时激活标哥Tab（只执行一次）
        /// </summary>
        private static void OnIdleActivateTab(object? sender, System.EventArgs e)
        {
            try
            {
                // 移除事件处理器，只执行一次
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdleActivateTab;

                if (_biaogTab != null && ComponentManager.Ribbon != null)
                {
                    // 激活标哥Tab，使其显示出来
                    _biaogTab.IsActive = true;
                    Log.Information("✅ Ribbon Tab已激活显示");
                }
            }
            catch (System.Exception ex)
            {
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

            // === 大按钮：快速翻译为中文（推荐） ===
            RibbonButton translateZH = new RibbonButton
            {
                Text = "翻译为中文\n(推荐)★",
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandParameter = "BIAOGE_TRANSLATE_ZH ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "一键翻译整个图纸为简体中文\n使用qwen-mt-flash模型\n支持92种语言识别"
            };

            panelSource.Items.Add(translateZH);

            // === 第二行：小按钮组1 ===
            RibbonRowPanel row2 = new RibbonRowPanel();

            // 框选翻译
            RibbonButton translateSelected = new RibbonButton
            {
                Text = "框选翻译",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_TRANSLATE_SELECTED ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "选择文本实体进行翻译\n支持DBText, MText, AttributeReference"
            };
            row2.Items.Add(translateSelected);

            // 全图翻译
            RibbonButton translateAll = new RibbonButton
            {
                Text = "全图翻译",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_TRANSLATE ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "打开翻译面板，翻译整个图纸"
            };
            row2.Items.Add(translateAll);

            // 翻译为英语
            RibbonButton translateEN = new RibbonButton
            {
                Text = "译为英语",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_TRANSLATE_EN ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "快速翻译整个图纸为英语"
            };
            row2.Items.Add(translateEN);

            panelSource.Items.Add(row2);

            // === 第三行：小按钮组2 ===
            RibbonRowPanel row3 = new RibbonRowPanel();

            // 图层翻译
            RibbonButton translateLayer = new RibbonButton
            {
                Text = "图层翻译",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_TRANSLATE_LAYER ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "按图层选择性翻译（即将推出）"
            };
            row3.Items.Add(translateLayer);

            // 翻译预览
            RibbonButton translatePreview = new RibbonButton
            {
                Text = "翻译预览",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_PREVIEW ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "预览翻译结果后再应用（即将推出）"
            };
            row3.Items.Add(translatePreview);

            // 清除缓存
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
            row3.Items.Add(clearCache);

            panelSource.Items.Add(row3);

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

            // === 大按钮：标哥AI助手 ===
            RibbonButton aiAssistant = new RibbonButton
            {
                Text = "标哥AI\n助手",
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

            // === 大按钮：智能识别 ===
            RibbonButton calculate = new RibbonButton
            {
                Text = "智能\n识别",
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

            // 测试按钮（用于调试）
            RibbonButton testButton = new RibbonButton
            {
                Text = "测试",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "TEST_RIBBON_CLICK ",
                CommandHandler = new RibbonTestHandler(),
                ToolTip = "测试Ribbon按钮是否工作"
            };
            row1.Items.Add(testButton);

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

            // 快捷键
            RibbonButton keys = new RibbonButton
            {
                Text = "快捷键",
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

            // 帮助
            RibbonButton help = new RibbonButton
            {
                Text = "帮助",
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = "BIAOGE_HELP ",
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = "显示帮助信息"
            };
            row2.Items.Add(help);

            // 关于
            RibbonButton about = new RibbonButton
            {
                Text = "关于",
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
    /// Ribbon测试处理器（用于调试）
    /// </summary>
    public class RibbonTestHandler : System.Windows.Input.ICommand
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
                // 显示消息框确认按钮被点击
                System.Windows.MessageBox.Show(
                    "Ribbon按钮点击成功！\n\n" +
                    "如果您看到这个消息，说明Ribbon按钮工作正常。\n" +
                    "现在问题可能出在命令执行部分。\n\n" +
                    $"参数: {parameter}",
                    "标哥插件 - Ribbon测试",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information
                );

                Log.Information($"Ribbon测试按钮被点击，参数: {parameter}");

                // 尝试在命令行显示消息
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage("\n[成功] Ribbon测试按钮已触发！");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Ribbon测试按钮执行失败");
            }
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

                // 清理命令字符串
                string cleanCommand = commandStr.Trim();

                // 重要：添加 ^C^C 前缀来取消当前命令（AutoCAD最佳实践）
                // 这确保命令在干净的状态下执行
                string fullCommand = "^C^C" + cleanCommand + " ";

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
