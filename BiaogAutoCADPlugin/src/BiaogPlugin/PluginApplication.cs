using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;

// 声明这是一个AutoCAD扩展应用程序
[assembly: ExtensionApplication(typeof(BiaogPlugin.PluginApplication))]

namespace BiaogPlugin
{
    /// <summary>
    /// 标哥 - AutoCAD翻译插件主应用程序类
    /// 实现IExtensionApplication接口，用于插件的初始化和清理
    /// </summary>
    public class PluginApplication : IExtensionApplication
    {
        /// <summary>
        /// 插件初始化 - AutoCAD加载插件时调用
        /// </summary>
        public void Initialize()
        {
            try
            {
                // 配置日志系统
                ConfigureLogging();

                Log.Information("标哥 - AutoCAD翻译插件正在初始化...");

                // 初始化服务
                InitializeServices();

                // 获取当前文档
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    var ed = doc.Editor;

                    // 输出欢迎信息
                    ed.WriteMessage("\n╔══════════════════════════════════════════════════╗");
                    ed.WriteMessage("\n║                                                  ║");
                    ed.WriteMessage("\n║      标哥 - 建筑工程CAD翻译工具 v1.0           ║");
                    ed.WriteMessage("\n║                                                  ║");
                    ed.WriteMessage("\n║      基于AutoCAD .NET API                        ║");
                    ed.WriteMessage("\n║      100%准确的DWG文件处理                       ║");
                    ed.WriteMessage("\n║                                                  ║");
                    ed.WriteMessage("\n╚══════════════════════════════════════════════════╝");
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n可用命令:");
                    ed.WriteMessage("\n  BIAOGE_TRANSLATE  - 翻译当前图纸");
                    ed.WriteMessage("\n  BIAOGE_CALCULATE  - 构件识别和工程量计算");
                    ed.WriteMessage("\n  BIAOGE_SETTINGS   - 打开设置对话框");
                    ed.WriteMessage("\n  BIAOGE_HELP       - 显示帮助信息");
                    ed.WriteMessage("\n");
                }

                // 初始化UI面板
                UI.PaletteManager.Initialize();

                // 注册右键上下文菜单
                Extensions.ContextMenuManager.RegisterContextMenus();

                // 加载Ribbon工具栏
                UI.Ribbon.RibbonManager.LoadRibbon();

                // 启用双击翻译功能
                Extensions.DoubleClickHandler.Enable();

                // 启用智能输入法切换
                Services.InputMethodManager.Enable();

                Log.Information("插件初始化成功");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "插件初始化失败");

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[错误] 插件初始化失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 插件终止 - AutoCAD卸载插件时调用
        /// </summary>
        public void Terminate()
        {
            try
            {
                Log.Information("标哥插件正在卸载...");

                // 注销右键上下文菜单
                Extensions.ContextMenuManager.UnregisterContextMenus();

                // 卸载Ribbon工具栏
                UI.Ribbon.RibbonManager.UnloadRibbon();

                // 禁用双击翻译功能
                Extensions.DoubleClickHandler.Disable();

                // 禁用智能输入法切换
                Services.InputMethodManager.Disable();

                // 清理UI资源
                UI.PaletteManager.Cleanup();

                // 清理其他资源（如数据库连接等）
                Services.ServiceLocator.Cleanup();

                Log.Information("插件卸载成功");
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "插件卸载时发生错误");
            }
        }

        /// <summary>
        /// 配置Serilog日志系统
        /// </summary>
        private void ConfigureLogging()
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Biaoge",
                "Logs",
                $"BiaogPlugin-{DateTime.Now:yyyyMMdd}.log"
            );

            // 确保日志目录存在
            var logDir = System.IO.Path.GetDirectoryName(logPath);
            if (!System.IO.Directory.Exists(logDir))
            {
                System.IO.Directory.CreateDirectory(logDir!);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.Console()
                .CreateLogger();
        }

        /// <summary>
        /// 初始化所有服务并注册到ServiceLocator
        /// </summary>
        private void InitializeServices()
        {
            Log.Information("初始化服务...");

            try
            {
                // 1. 配置管理器
                var configManager = new Services.ConfigManager();
                Services.ServiceLocator.RegisterService(configManager);
                Log.Debug("ConfigManager已注册");

                // 2. 缓存服务
                var cacheService = new Services.CacheService();
                Services.ServiceLocator.RegisterService(cacheService);
                Log.Debug("CacheService已注册");

                // 3. HTTP客户端
                var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);
                Services.ServiceLocator.RegisterService(httpClient);
                Log.Debug("HttpClient已注册");

                // 4. 百炼API客户端
                var bailianClient = new Services.BailianApiClient(httpClient, configManager);
                Services.ServiceLocator.RegisterService(bailianClient);
                Log.Debug("BailianApiClient已注册");

                // 5. 翻译引擎
                var translationEngine = new Services.TranslationEngine(bailianClient, cacheService);
                Services.ServiceLocator.RegisterService(translationEngine);
                Log.Debug("TranslationEngine已注册");

                // 6. 性能监控器
                var performanceMonitor = new Services.PerformanceMonitor();
                Services.ServiceLocator.RegisterService(performanceMonitor);
                Log.Debug("PerformanceMonitor已注册");

                // 7. 诊断工具
                var diagnosticTool = new Services.DiagnosticTool(configManager, bailianClient, cacheService);
                Services.ServiceLocator.RegisterService(diagnosticTool);
                Log.Debug("DiagnosticTool已注册");

                // 8. 翻译历史记录
                var translationHistory = new Services.TranslationHistory(
                    configManager.Config.Translation.HistoryMaxSize
                );
                Services.ServiceLocator.RegisterService(translationHistory);
                Log.Debug("TranslationHistory已注册");

                Log.Information("所有服务初始化完成");

                // 检查API密钥配置
                if (!bailianClient.HasApiKey)
                {
                    Log.Warning("未配置百炼API密钥，请使用BIAOGE_SETTINGS命令配置");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "服务初始化失败");
                throw;
            }
        }
    }
}
