using System;
using System.Linq;
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
        public async void ClearCache()
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
                if (cacheService != null)
                {
                    await cacheService.ClearCacheAsync();
                }

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

        #region 诊断和性能监控命令

        /// <summary>
        /// 运行系统诊断
        /// </summary>
        [CommandMethod("BIAOGE_DIAGNOSTIC", CommandFlags.Modal)]
        public async void RunDiagnostic()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n正在运行系统诊断，请稍候...");
                Log.Information("开始运行诊断");

                // 获取服务
                var configManager = ServiceLocator.GetService<ConfigManager>();
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var cacheService = ServiceLocator.GetService<CacheService>();

                if (configManager == null || bailianClient == null || cacheService == null)
                {
                    ed.WriteMessage("\n[错误] 无法获取必要的服务，插件可能未正确初始化");
                    return;
                }

                var diagnostic = new DiagnosticTool(configManager, bailianClient, cacheService);
                var report = await diagnostic.RunFullDiagnosticAsync();

                // 显示报告
                ed.WriteMessage("\n\n" + report.ToString());

                // 保存到桌面
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var reportPath = System.IO.Path.Combine(desktopPath, $"BiaogPlugin_Diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                System.IO.File.WriteAllText(reportPath, report.ToString());

                ed.WriteMessage($"\n诊断报告已保存到: {reportPath}");
                Log.Information($"诊断报告已保存: {reportPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "运行诊断失败");
                ed.WriteMessage($"\n[错误] 诊断失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示性能监控报告
        /// </summary>
        [CommandMethod("BIAOGE_PERFORMANCE", CommandFlags.Modal)]
        public void ShowPerformanceReport()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var monitor = ServiceLocator.GetService<PerformanceMonitor>();
                if (monitor == null)
                {
                    ed.WriteMessage("\n[警告] 性能监控器未初始化");
                    return;
                }

                // 生成报告
                var report = monitor.GenerateReport();
                ed.WriteMessage("\n\n" + report);

                // 检查性能问题
                var warnings = monitor.CheckForIssues();
                if (warnings.Any())
                {
                    ed.WriteMessage("\n\n=== 性能警告 ===\n");
                    foreach (var warning in warnings)
                    {
                        ed.WriteMessage($"\n{warning}");
                    }
                }

                // 询问是否保存报告
                var options = new PromptKeywordOptions("\n是否保存性能报告到桌面? [是(Y)/否(N)]")
                {
                    Keywords = { "Y", "N" },
                    AllowNone = false
                };
                options.Keywords.Default = "N";

                var result = ed.GetKeywords(options);
                if (result.Status == PromptStatus.OK && result.StringResult == "Y")
                {
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var reportPath = System.IO.Path.Combine(desktopPath, $"BiaogPlugin_Performance_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                    var fullReport = report;
                    if (warnings.Any())
                    {
                        fullReport += "\n\n=== 性能警告 ===\n";
                        fullReport += string.Join("\n\n", warnings.Select(w => w.ToString()));
                    }

                    System.IO.File.WriteAllText(reportPath, fullReport);
                    ed.WriteMessage($"\n性能报告已保存到: {reportPath}");
                }

                Log.Information("显示性能报告");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示性能报告失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 重置性能统计
        /// </summary>
        [CommandMethod("BIAOGE_RESETPERF", CommandFlags.Modal)]
        public void ResetPerformanceStats()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var monitor = ServiceLocator.GetService<PerformanceMonitor>();
                if (monitor == null)
                {
                    ed.WriteMessage("\n[警告] 性能监控器未初始化");
                    return;
                }

                monitor.Reset();
                ed.WriteMessage("\n性能统计已重置。");
                Log.Information("性能统计已重置");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "重置性能统计失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        #endregion

        #region 快捷工具命令

        /// <summary>
        /// 快速导出Excel工程量清单
        /// </summary>
        [CommandMethod("BIAOGE_EXPORTEXCEL", CommandFlags.Modal)]
        public async void QuickExportExcel()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n开始快速识别构件...");
                Log.Information("执行快速Excel导出");

                // 提取文本
                var extractor = new DwgTextExtractor();
                var textEntities = extractor.ExtractAllText();
                ed.WriteMessage($"\n提取到 {textEntities.Count} 个文本实体");

                // 识别构件
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var recognizer = new ComponentRecognizer(bailianClient);
                var results = await recognizer.RecognizeFromTextEntitiesAsync(textEntities, useAiVerification: false);

                // 过滤低置信度（默认0.7）
                results = results.Where(r => r.Confidence >= 0.7).ToList();
                ed.WriteMessage($"\n识别到 {results.Count} 个构件（置信度≥70%）");

                // 计算工程量
                var calculator = new QuantityCalculator();
                var summary = calculator.CalculateSummary(results);

                // 导出Excel
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"工程量清单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var outputPath = System.IO.Path.Combine(desktopPath, fileName);

                var exporter = new ExcelExporter();
                exporter.ExportSummary(summary, outputPath);

                ed.WriteMessage($"\n\nExcel清单已导出到: {outputPath}");
                ed.WriteMessage($"\n  构件总数: {summary.TotalComponents}");
                ed.WriteMessage($"\n  总成本: ¥{summary.TotalCost:N2}");

                // 打开文件夹
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{outputPath}\"");

                Log.Information($"Excel导出完成: {outputPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "快速导出Excel失败");
                ed.WriteMessage($"\n[错误] 导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 快速统计构件数量
        /// </summary>
        [CommandMethod("BIAOGE_QUICKCOUNT", CommandFlags.Modal)]
        public async void QuickCountComponents()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n正在快速统计构件...");

                // 提取文本
                var extractor = new DwgTextExtractor();
                var textEntities = extractor.ExtractAllText();

                // 识别构件（不使用AI）
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var recognizer = new ComponentRecognizer(bailianClient);
                var results = await recognizer.RecognizeFromTextEntitiesAsync(textEntities, useAiVerification: false);

                // 按类型分组
                var grouped = results
                    .Where(r => r.Confidence >= 0.7)
                    .GroupBy(r => r.Type)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                ed.WriteMessage("\n\n╔══════════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  构件统计（置信度≥70%）                                ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝\n");

                foreach (var group in grouped.Take(15))
                {
                    var totalQty = group.Sum(r => r.Quantity);
                    var avgConf = group.Average(r => r.Confidence);
                    ed.WriteMessage($"\n  {group.Key,-20} × {totalQty,4}  (置信度: {avgConf:P0})");
                }

                if (grouped.Count > 15)
                {
                    ed.WriteMessage($"\n  ... 还有 {grouped.Count - 15} 种构件类型");
                }

                ed.WriteMessage($"\n\n  总计: {results.Count(r => r.Confidence >= 0.7)} 个构件");
                ed.WriteMessage("\n");

                Log.Information("快速统计完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "快速统计失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 统计文本实体
        /// </summary>
        [CommandMethod("BIAOGE_TEXTCOUNT", CommandFlags.Modal)]
        public void CountTextEntities()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n正在统计文本实体...");

                var extractor = new DwgTextExtractor();
                var texts = extractor.ExtractAllText();

                var byType = texts.GroupBy(t => t.Type).ToList();
                var byLayer = texts.GroupBy(t => t.Layer).OrderByDescending(g => g.Count()).ToList();

                ed.WriteMessage("\n\n╔══════════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  文本实体统计                                          ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝\n");

                ed.WriteMessage("\n【按类型统计】");
                foreach (var group in byType)
                {
                    ed.WriteMessage($"\n  {group.Key,-20} × {group.Count(),4}");
                }

                ed.WriteMessage("\n\n【按图层统计（前10个）】");
                foreach (var group in byLayer.Take(10))
                {
                    ed.WriteMessage($"\n  {group.Key,-20} × {group.Count(),4}");
                }

                if (byLayer.Count > 10)
                {
                    ed.WriteMessage($"\n  ... 还有 {byLayer.Count - 10} 个图层");
                }

                ed.WriteMessage($"\n\n  总计: {texts.Count} 个文本实体");
                ed.WriteMessage("\n");

                Log.Information($"文本统计完成: {texts.Count} 个实体");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "统计文本失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 显示图层信息
        /// </summary>
        [CommandMethod("BIAOGE_LAYERINFO", CommandFlags.Modal)]
        public void ShowLayerInfo()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = (Autodesk.AutoCAD.DatabaseServices.LayerTable)tr.GetObject(
                        db.LayerTableId,
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                    ed.WriteMessage("\n\n╔══════════════════════════════════════════════════════════╗");
                    ed.WriteMessage("\n║  图层信息                                              ║");
                    ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝\n");

                    int count = 0;
                    foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId layerId in layerTable)
                    {
                        var layer = (Autodesk.AutoCAD.DatabaseServices.LayerTableRecord)tr.GetObject(
                            layerId,
                            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                        var status = layer.IsOff ? "[关闭]" : layer.IsFrozen ? "[冻结]" : "[打开]";
                        var locked = layer.IsLocked ? "[锁定]" : "";

                        ed.WriteMessage($"\n  {layer.Name,-30} {status,-8} {locked}");
                        count++;
                    }

                    ed.WriteMessage($"\n\n  总计: {count} 个图层");
                    ed.WriteMessage("\n");

                    tr.Commit();
                }

                Log.Information("显示图层信息");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示图层信息失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 备份当前图纸
        /// </summary>
        [CommandMethod("BIAOGE_BACKUP", CommandFlags.Modal)]
        public void BackupDrawing()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                if (string.IsNullOrEmpty(doc.Name))
                {
                    ed.WriteMessage("\n[错误] 当前图纸未保存，无法备份");
                    return;
                }

                var originalPath = doc.Name;
                var directory = System.IO.Path.GetDirectoryName(originalPath);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(originalPath);
                var extension = System.IO.Path.GetExtension(originalPath);

                var backupPath = System.IO.Path.Combine(
                    directory!,
                    $"{fileName}_backup_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");

                // 复制文件
                System.IO.File.Copy(originalPath, backupPath, overwrite: false);

                ed.WriteMessage($"\n图纸已备份到: {backupPath}");
                Log.Information($"图纸已备份: {backupPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "备份图纸失败");
                ed.WriteMessage($"\n[错误] 备份失败: {ex.Message}");
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
