using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;
using BiaogPlugin.Services;
using BiaogPlugin.UI;

namespace BiaogPlugin
{
    /// <summary>
    /// 表哥插件的AutoCAD命令集
    /// </summary>
    public class Commands
    {
        #region 翻译命令

        /// <summary>
        /// 翻译当前图纸的命令
        /// </summary>
        [CommandMethod("BIAOGE_TRANSLATE", CommandFlags.Modal)]
        public async void TranslateDrawing()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("执行翻译命令: BIAOGE_TRANSLATE");

                // 显示翻译面板
                PaletteManager.ShowTranslationPalette();

                ed.WriteMessage("\n翻译面板已打开，请在右侧面板中选择目标语言并开始翻译。");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示翻译面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 快速翻译命令（直接翻译为英语）
        /// </summary>
        [CommandMethod("BIAOGE_TRANSLATE_EN", CommandFlags.Modal)]
        public async void QuickTranslateToEnglish()
        {
            await QuickTranslate("en", "英语");
        }

        /// <summary>
        /// 快速翻译到指定语言
        /// </summary>
        private async Task QuickTranslate(string targetLanguage, string languageName)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information($"执行快速翻译: {languageName}");

                ed.WriteMessage($"\n开始翻译为{languageName}...");

                var controller = new TranslationController();

                var progress = new Progress<TranslationProgress>(p =>
                {
                    ed.WriteMessage($"\r{p.Stage}: {p.Percentage}%    ");
                });

                await controller.TranslateCurrentDrawing(targetLanguage, progress);

                ed.WriteMessage($"\n翻译完成！");
                Log.Information($"翻译完成: {languageName}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"翻译失败: {languageName}");
                ed.WriteMessage($"\n[错误] 翻译失败: {ex.Message}");
            }
        }

        #endregion

        #region 算量命令

        /// <summary>
        /// 构件识别和工程量计算命令
        /// </summary>
        [CommandMethod("BIAOGE_CALCULATE", CommandFlags.Modal)]
        public void CalculateQuantities()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("执行算量命令: BIAOGE_CALCULATE");

                // 显示算量面板
                PaletteManager.ShowCalculationPalette();

                ed.WriteMessage("\n算量面板已打开，请在右侧面板中选择识别模式。");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示算量面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        #endregion

        #region 设置命令

        /// <summary>
        /// 打开设置对话框
        /// </summary>
        [CommandMethod("BIAOGE_SETTINGS", CommandFlags.Modal)]
        public void OpenSettings()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("打开设置对话框");

                var settingsDialog = new SettingsDialog();
                Application.ShowModalDialog(settingsDialog);

                ed.WriteMessage("\n设置已保存。");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开设置对话框失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        #endregion

        #region 帮助和工具命令

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        [CommandMethod("BIAOGE_HELP", CommandFlags.Modal)]
        public void ShowHelp()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
            ed.WriteMessage("\n║  表哥 - 建筑工程CAD翻译工具 v1.0 - 帮助                ║");
            ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【翻译功能】");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE      - 打开翻译面板");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE_EN   - 快速翻译为英语");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【算量功能】");
            ed.WriteMessage("\n  BIAOGE_CALCULATE      - 打开算量面板");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【设置】");
            ed.WriteMessage("\n  BIAOGE_SETTINGS       - 打开设置对话框");
            ed.WriteMessage("\n  BIAOGE_ABOUT          - 关于插件");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【工具】");
            ed.WriteMessage("\n  BIAOGE_HELP           - 显示此帮助信息");
            ed.WriteMessage("\n  BIAOGE_VERSION        - 显示版本信息");
            ed.WriteMessage("\n  BIAOGE_CLEARCACHE     - 清除翻译缓存");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n详细文档: https://github.com/lechatcloud-ship-it/biaoge");
            ed.WriteMessage("\n");
        }

        /// <summary>
        /// 显示版本信息
        /// </summary>
        [CommandMethod("BIAOGE_VERSION", CommandFlags.Modal)]
        public void ShowVersion()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
            ed.WriteMessage("\n║  表哥 - 建筑工程CAD翻译工具                            ║");
            ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
            ed.WriteMessage($"\n  版本: {version}");
            ed.WriteMessage("\n  技术: AutoCAD .NET API (100%准确的DWG处理)");
            ed.WriteMessage("\n  AI: 阿里云百炼大模型");
            ed.WriteMessage("\n  作者: Your Company");
            ed.WriteMessage("\n  版权: Copyright © 2025");
            ed.WriteMessage("\n");
        }

        /// <summary>
        /// 关于对话框
        /// </summary>
        [CommandMethod("BIAOGE_ABOUT", CommandFlags.Modal)]
        public void ShowAbout()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ShowVersion();

            ed.WriteMessage("\n【核心功能】");
            ed.WriteMessage("\n  ✓ AI智能翻译 (8种语言)");
            ed.WriteMessage("\n  ✓ 构件识别算量 (超高精度)");
            ed.WriteMessage("\n  ✓ 多格式导出 (Excel/PDF)");
            ed.WriteMessage("\n  ✓ 智能缓存 (90%+命中率)");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【技术优势】");
            ed.WriteMessage("\n  ✓ 100%准确的DWG读取 (AutoCAD官方引擎)");
            ed.WriteMessage("\n  ✓ 无缝集成AutoCAD工作流");
            ed.WriteMessage("\n  ✓ 符合建筑行业标准");
            ed.WriteMessage("\n");
        }

        /// <summary>
        /// 清除翻译缓存
        /// </summary>
        [CommandMethod("BIAOGE_CLEARCACHE", CommandFlags.Modal)]
        public void ClearCache()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                // 提示用户确认
                var options = new PromptKeywordOptions("\n确定要清除所有翻译缓存吗? [是(Y)/否(N)]")
                {
                    Keywords = { "Y", "N" },
                    AllowNone = false
                };
                options.Keywords.Default = "N";

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK || result.StringResult != "Y")
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 清除缓存
                var cacheService = ServiceLocator.GetService<CacheService>();
                cacheService?.ClearAll();

                ed.WriteMessage("\n缓存已清除。");
                Log.Information("用户清除了翻译缓存");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清除缓存失败");
                ed.WriteMessage($"\n[错误] 清除缓存失败: {ex.Message}");
            }
        }

        #endregion

        #region 调试命令（仅在Debug模式下可用）

#if DEBUG
        /// <summary>
        /// 测试DWG文本提取（调试用）
        /// </summary>
        [CommandMethod("BIAOGE_TEST_EXTRACT", CommandFlags.Modal)]
        public void TestExtract()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n开始测试文本提取...");

                var extractor = new DwgTextExtractor();
                var texts = extractor.ExtractAllText();

                ed.WriteMessage($"\n提取到 {texts.Count} 个文本实体:");

                // 显示前10个文本
                int count = 0;
                foreach (var text in texts)
                {
                    if (count++ >= 10) break;
                    ed.WriteMessage($"\n  [{text.Type}] {text.Content} (图层: {text.Layer})");
                }

                if (texts.Count > 10)
                {
                    ed.WriteMessage($"\n  ... 还有 {texts.Count - 10} 个文本");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "测试提取失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }
#endif

        #endregion
    }
}
