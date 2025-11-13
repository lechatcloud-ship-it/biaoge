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
        // ✅ 静态HttpClient实例，整个应用程序生命周期复用
        // 根据Microsoft最佳实践：HttpClient应该被实例化一次并复用，避免Socket耗尽
        // 参考：https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
        private static readonly System.Net.Http.HttpClient _sharedHttpClient = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // 5分钟超时，适合长时间AI翻译操作
        };

        /// <summary>
        /// 插件初始化 - AutoCAD加载插件时调用
        /// </summary>
        public void Initialize()
        {
            try
            {
                // ✅ 关键修复：注册程序集解析事件，解决System.Memory等依赖加载问题
                // 当AutoCAD尝试加载依赖DLL时，如果在默认路径找不到，会触发此事件
                // 我们手动从插件所在目录加载依赖
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

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
                    ed.WriteMessage("\n快速开始:");
                    ed.WriteMessage("\n  BIAOGE_QUICKSTART - 5分钟快速上手指南（新用户推荐）");
                    ed.WriteMessage("\n  BIAOGE_TRANSLATE_ZH - 一键翻译为中文");
                    ed.WriteMessage("\n  BIAOGE_AI         - 启动AI助手");
                    ed.WriteMessage("\n  BIAOGE_HELP       - 查看完整命令列表");
                    ed.WriteMessage("\n");
                }

                // 初始化UI面板
                UI.PaletteManager.Initialize();

                // 注册右键上下文菜单（先注销避免重复注册）
                try
                {
                    Extensions.ContextMenuManager.UnregisterContextMenus();
                }
                catch { /* 忽略未注册的错误 */ }
                Extensions.ContextMenuManager.RegisterContextMenus();

                // 加载Ribbon工具栏（先卸载避免重复）
                try
                {
                    UI.Ribbon.RibbonManager.UnloadRibbon();
                }
                catch { /* 忽略未加载的错误 */ }
                UI.Ribbon.RibbonManager.LoadRibbon();

                // 启用双击翻译功能（先禁用避免重复）
                try
                {
                    Extensions.DoubleClickHandler.Disable();
                }
                catch { /* 忽略未启用的错误 */ }
                Extensions.DoubleClickHandler.Enable();

                // 启用智能输入法切换（先禁用避免重复）
                try
                {
                    Services.InputMethodManager.Disable();
                }
                catch { /* 忽略未启用的错误 */ }
                Services.InputMethodManager.Enable();

                Log.Information("插件初始化成功");
            }
            catch (System.Exception ex)
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
        /// 首次启动时弹出API密钥设置对话框
        /// 使用Idle事件确保AutoCAD完全初始化后再弹窗
        /// </summary>
        private void OnFirstTimeApiKeySetup(object sender, System.EventArgs e)
        {
            // 移除事件处理器，只执行一次
            Application.Idle -= OnFirstTimeApiKeySetup;

            try
            {
                Log.Information("显示首次配置对话框");

                // 弹出设置对话框
                var settingsDialog = new UI.SettingsDialog();
                var result = settingsDialog.ShowDialog();

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    var ed = doc.Editor;
                    if (result == true)
                    {
                        // 用户配置了密钥
                        var bailianClient = Services.ServiceLocator.GetService<Services.BailianApiClient>();
                        if (bailianClient != null && bailianClient.HasApiKey)
                        {
                            ed.WriteMessage("\n✓ API密钥配置成功！现在可以使用翻译功能了。");
                            ed.WriteMessage("\n");
                            ed.WriteMessage("\n快速开始:");
                            ed.WriteMessage("\n  BIAOGE_TRANSLATE_ZH - 一键翻译为中文");
                            ed.WriteMessage("\n  BIAOGE_AI         - 启动AI助手");
                            ed.WriteMessage("\n  BIAOGE_HELP       - 查看完整命令列表");
                            ed.WriteMessage("\n");
                            Log.Information("用户已配置API密钥");
                        }
                        else
                        {
                            ed.WriteMessage("\n⚠ 未检测到API密钥，请确认已正确保存配置。");
                            ed.WriteMessage("\n可随时运行 BIAOGE_SETTINGS 重新配置。");
                            ed.WriteMessage("\n");
                            Log.Warning("用户关闭对话框但未配置API密钥");
                        }
                    }
                    else
                    {
                        // 用户取消了配置
                        ed.WriteMessage("\n您取消了API密钥配置。");
                        ed.WriteMessage("\n插件的翻译和AI功能需要配置密钥才能使用。");
                        ed.WriteMessage("\n运行 BIAOGE_SETTINGS 命令可随时配置。");
                        ed.WriteMessage("\n");
                        Log.Information("用户取消了API密钥配置");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示首次配置对话框失败");

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[错误] 无法打开设置对话框: {ex.Message}");
                    doc.Editor.WriteMessage("\n请运行 BIAOGE_SETTINGS 命令手动配置。");
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

                // ✅ 取消注册程序集解析事件，避免内存泄漏
                AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;

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
            catch (System.Exception ex)
            {
                Log.Error(ex, "插件卸载时发生错误");
            }
        }

        /// <summary>
        /// 程序集解析事件处理器 - 解决依赖DLL加载问题
        /// 当AutoCAD无法找到依赖程序集（如System.Memory.dll）时，此方法会被调用
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">包含请求程序集信息的参数</param>
        /// <returns>找到的程序集，如果未找到返回null</returns>
        private static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                // 获取请求的程序集名称（例如："System.Memory, Version=4.0.1.1, Culture=neutral, PublicKeyToken=..."）
                var assemblyName = new System.Reflection.AssemblyName(args.Name);
                var simpleName = assemblyName.Name; // 提取简单名称，例如："System.Memory"

                // 获取当前插件DLL所在的目录
                // 例如：C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle\Contents\2021\
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var pluginDirectory = System.IO.Path.GetDirectoryName(assemblyLocation);

                if (string.IsNullOrEmpty(pluginDirectory))
                {
                    Log.Warning($"无法确定插件目录，程序集解析失败: {simpleName}");
                    return null;
                }

                // 构造依赖DLL的完整路径
                var dependencyPath = System.IO.Path.Combine(pluginDirectory, simpleName + ".dll");

                // 检查文件是否存在
                if (System.IO.File.Exists(dependencyPath))
                {
                    // 从文件加载程序集
                    var assembly = System.Reflection.Assembly.LoadFrom(dependencyPath);
                    Log.Debug($"成功加载依赖程序集: {simpleName} 从 {dependencyPath}");
                    return assembly;
                }
                else
                {
                    // 文件不存在，返回null让系统继续尝试默认查找
                    Log.Debug($"依赖程序集不存在: {dependencyPath}");
                    return null;
                }
            }
            catch (System.Exception ex)
            {
                // 记录错误但不抛出异常，避免影响AutoCAD
                Log.Error(ex, $"程序集解析失败: {args.Name}");
                return null;
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

                // 3. HTTP客户端（使用静态共享实例）
                // ✅ 修复：使用静态HttpClient避免Socket耗尽
                Services.ServiceLocator.RegisterService(_sharedHttpClient);
                Log.Debug("HttpClient已注册（静态实例）");

                // 4. 百炼API客户端
                var bailianClient = new Services.BailianApiClient(_sharedHttpClient, configManager);
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

                // ✅ UX改进: 检查是否曾经配置过API密钥（只有完全没配置过才弹出首次设置）
                // 注意：不检查密钥是否有效，只检查是否曾经保存过，避免每次重启都弹出
                var savedApiKey = configManager.GetString("Bailian:ApiKey");
                if (string.IsNullOrWhiteSpace(savedApiKey))
                {
                    Log.Warning("检测到首次使用，未配置百炼API密钥，弹出设置对话框");

                    var doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        var ed = doc.Editor;
                        ed.WriteMessage("\n");
                        ed.WriteMessage("\n╔══════════════════════════════════════════════════╗");
                        ed.WriteMessage("\n║  欢迎使用标哥AutoCAD插件！                      ║");
                        ed.WriteMessage("\n║                                                  ║");
                        ed.WriteMessage("\n║  首次使用需要配置百炼API密钥                    ║");
                        ed.WriteMessage("\n║  配置窗口即将打开...                            ║");
                        ed.WriteMessage("\n╚══════════════════════════════════════════════════╝");
                        ed.WriteMessage("\n");
                    }

                    // 使用Application.Idle事件确保在AutoCAD完全初始化后弹出对话框
                    // 这样可以避免在初始化期间弹窗导致的问题
                    Application.Idle += OnFirstTimeApiKeySetup;
                }
                else
                {
                    // 已配置过密钥，但需要验证是否有效
                    if (!bailianClient.HasApiKey)
                    {
                        Log.Warning("API密钥配置无效，请在设置中重新配置");
                        var doc = Application.DocumentManager.MdiActiveDocument;
                        if (doc != null)
                        {
                            doc.Editor.WriteMessage("\n⚠ 警告：当前API密钥无效，请运行 BIAOGE_SETTINGS 重新配置\n");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "服务初始化失败");
                throw;
            }
        }
    }
}
