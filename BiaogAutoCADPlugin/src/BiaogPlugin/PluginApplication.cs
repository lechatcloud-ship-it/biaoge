using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;

// 声明这是一个AutoCAD扩展应用程序
[assembly: ExtensionApplication(typeof(BiaogPlugin.PluginApplication))]

namespace BiaogPlugin
{
    /// <summary>
    /// 表哥 - AutoCAD翻译插件主应用程序类
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

                Log.Information("表哥 - AutoCAD翻译插件正在初始化...");

                // 获取当前文档
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    var ed = doc.Editor;

                    // 输出欢迎信息
                    ed.WriteMessage("\n╔══════════════════════════════════════════════════╗");
                    ed.WriteMessage("\n║                                                  ║");
                    ed.WriteMessage("\n║      表哥 - 建筑工程CAD翻译工具 v1.0           ║");
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
                Log.Information("表哥插件正在卸载...");

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
    }
}
