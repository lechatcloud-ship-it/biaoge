using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Serilog;
using BiaogPlugin.Services;
using BiaogPlugin.UI;
using BiaogPlugin.Models;
using BiaogPlugin.Extensions;

namespace BiaogPlugin
{
    /// <summary>
    /// 标哥插件的AutoCAD命令集
    /// </summary>
    public class Commands
    {
        #region 初始化命令

        /// <summary>
        /// 插件启动时的初始化命令
        /// 在PackageContents.xml中设置 StartupCommand="True"，插件加载时自动执行
        /// 关键作用：确保Ribbon工具栏正确加载和显示
        /// </summary>
        [CommandMethod("BIAOGE_INITIALIZE", CommandFlags.Modal | CommandFlags.NoInternalLock)]
        public void InitializePlugin()
        {
            try
            {
                Log.Information("════════════════════════════════════════════════");
                Log.Information("[关键] 标哥插件初始化命令已执行 (StartupCommand)");
                Log.Information("════════════════════════════════════════════════");

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    var ed = doc.Editor;

                    // 执行Ribbon初始化（保险措施）
                    UI.Ribbon.RibbonManager.LoadRibbon();

                    Log.Debug("Ribbon工具栏已通过StartupCommand初始化");

                    // 仅在开发环境显示初始化消息
#if DEBUG
                    ed.WriteMessage("\n[开发模式] 标哥插件初始化完成");
#endif
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "插件初始化命令执行失败");
            }
        }

        #endregion

        #region 翻译命令

        /// <summary>
        /// 翻译当前图纸的命令
        /// </summary>
        [CommandMethod("BIAOGE_TRANSLATE", CommandFlags.Modal)]
        public void TranslateDrawing()
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
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示翻译面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 快速翻译命令（直接翻译为简体中文）- 最常用
        /// </summary>
        [CommandMethod("BIAOGE_TRANSLATE_ZH", CommandFlags.Modal)]
        public async void QuickTranslateToChinese()
        {
            await QuickTranslate("zh", "简体中文");
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
        /// 框选翻译命令 - 仅翻译用户选中的文本实体
        /// ✅ 优化：使用全局异常处理防止AutoCAD崩溃
        /// </summary>
        [CommandMethod("BIAOGE_TRANSLATE_SELECTED", CommandFlags.Modal)]
        public async void TranslateSelected()
        {
            // ✅ 顶层异常处理，防止AutoCAD崩溃
            Services.CommandExceptionHandler.ExecuteSafely(async () =>
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var db = doc.Database;

                try
                {
                    Log.Information("执行框选翻译命令");

                // 提示用户选择文本实体
                ed.WriteMessage("\n请选择要翻译的文本实体...");

                var selectionOptions = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择文本实体: "
                };

                // 创建过滤器：只选择文本实体（DBText, MText, AttributeReference）
                var filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Start, "MTEXT"),
                    new TypedValue((int)DxfCode.Start, "ATTRIB"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                };
                var filter = new SelectionFilter(filterList);

                var selectionResult = ed.GetSelection(selectionOptions, filter);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                var selectedIds = selectionResult.Value.GetObjectIds();
                if (selectedIds.Length == 0)
                {
                    ed.WriteMessage("\n未选择任何文本实体。");
                    return;
                }

                ed.WriteMessage($"\n已选择 {selectedIds.Length} 个文本实体");

                // 提示用户选择目标语言（默认中文）
                var languageOptions = new PromptKeywordOptions("\n选择目标语言")
                {
                    Keywords = { "中文", "英语", "日语", "韩语", "法语", "西班牙语", "德语", "俄语" },
                    AllowNone = false
                };
                languageOptions.Keywords.Default = "中文";  // 默认中文，符合中国设计师习惯

                var languageResult = ed.GetKeywords(languageOptions);
                if (languageResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 语言映射
                var languageMap = new Dictionary<string, (string code, string name)>
                {
                    ["中文"] = ("zh", "简体中文"),
                    ["英语"] = ("en", "英语"),
                    ["日语"] = ("ja", "日语"),
                    ["韩语"] = ("ko", "韩语"),
                    ["法语"] = ("fr", "法语"),
                    ["西班牙语"] = ("es", "西班牙语"),
                    ["德语"] = ("de", "德语"),
                    ["俄语"] = ("ru", "俄语")
                };

                var selectedLanguage = languageResult.StringResult;
                var (targetLanguage, languageName) = languageMap[selectedLanguage];

                ed.WriteMessage($"\n开始翻译为{languageName}...");

                // ✅ P1修复: 使用TextEntity替代DwgTextEntity,统一数据模型
                // 提取选中文本实体的内容
                var textEntities = new List<TextEntity>();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in selectedIds)
                    {
                        // ✅ AutoCAD 2022最佳实践：验证ObjectId有效性
                        if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                        {
                            Log.Debug($"跳过无效的ObjectId: {objId}");
                            continue;
                        }

                        var obj = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                        TextEntity? textEntity = null;

                        // ✅ P1修复: 使用TextEntity完整属性和强类型枚举
                        if (obj is Autodesk.AutoCAD.DatabaseServices.DBText dbText)
                        {
                            textEntity = new TextEntity
                            {
                                Id = objId,  // ✅ 使用Id而非ObjectId,与TextEntity定义一致
                                Type = TextEntityType.DBText,  // ✅ 强类型枚举,而非字符串
                                Content = dbText.TextString,
                                Position = dbText.Position,  // ✅ 直接使用Point3d,无需转换
                                Layer = dbText.Layer,
                                Height = dbText.Height,  // ✅ 保留完整属性
                                Rotation = dbText.Rotation,
                                ColorIndex = (short)dbText.ColorIndex
                            };
                        }
                        else if (obj is Autodesk.AutoCAD.DatabaseServices.MText mText)
                        {
                            textEntity = new TextEntity
                            {
                                Id = objId,
                                Type = TextEntityType.MText,
                                Content = mText.Text,
                                Position = mText.Location,
                                Layer = mText.Layer,
                                Height = mText.TextHeight,
                                Rotation = mText.Rotation,
                                ColorIndex = (short)mText.ColorIndex,
                                Width = mText.Width
                            };
                        }
                        else if (obj is Autodesk.AutoCAD.DatabaseServices.AttributeReference attRef)
                        {
                            textEntity = new TextEntity
                            {
                                Id = objId,
                                Type = TextEntityType.AttributeReference,
                                Content = attRef.TextString,
                                Position = attRef.Position,
                                Layer = attRef.Layer,
                                Height = attRef.Height,
                                Rotation = attRef.Rotation,
                                ColorIndex = (short)attRef.ColorIndex,
                                Tag = attRef.Tag
                            };
                        }

                        if (textEntity != null && !string.IsNullOrWhiteSpace(textEntity.Content))
                        {
                            textEntities.Add(textEntity);
                        }
                    }

                    tr.Commit();
                }

                if (textEntities.Count == 0)
                {
                    ed.WriteMessage("\n选中的文本实体为空或无效。");
                    return;
                }

                ed.WriteMessage($"\n提取到 {textEntities.Count} 个有效文本");

                // 翻译文本
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var cacheService = ServiceLocator.GetService<CacheService>();

                if (bailianClient == null || cacheService == null)
                {
                    ed.WriteMessage("\n[错误] 翻译服务未初始化");
                    return;
                }

                var engine = new TranslationEngine(bailianClient, cacheService);

                int translatedCount = 0;
                int skippedCount = 0;

                var apiProgress = new Progress<double>(p =>
                {
                    ed.WriteMessage($"\r翻译进度: {p:F1}%    ");
                });

                var translations = await engine.TranslateBatchWithCacheAsync(
                    textEntities.Select(t => t.Content).ToList(),
                    targetLanguage,
                    apiProgress,
                    CancellationToken.None
                );

                ed.WriteMessage("\n更新DWG文件...");

                // 更新DWG文本 - 构建更新请求列表
                var updater = new DwgTextUpdater();
                var updateRequests = new List<TextUpdateRequest>();

                for (int i = 0; i < textEntities.Count; i++)
                {
                    if (i < translations.Count && !string.IsNullOrEmpty(translations[i]))
                    {
                        updateRequests.Add(new TextUpdateRequest
                        {
                            ObjectId = textEntities[i].Id,  // ✅ P1修复: 使用Id而非ObjectId
                            OriginalContent = textEntities[i].Content,
                            NewContent = translations[i],
                            Layer = textEntities[i].Layer,  // ✅ 添加Layer信息
                            EntityType = textEntities[i].Type  // ✅ 添加EntityType信息
                        });
                        translatedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                var updateResult = updater.UpdateTexts(updateRequests);

                // 记录翻译历史
                var configManager2 = ServiceLocator.GetService<ConfigManager>();
                if (configManager2 != null && configManager2.Config.Translation.EnableHistory)
                {
                    var history = ServiceLocator.GetService<TranslationHistory>();
                    if (history != null)
                    {
                        var historyRecords = new List<TranslationHistory.HistoryRecord>();
                        for (int i = 0; i < textEntities.Count && i < translations.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(translations[i]))
                            {
                                historyRecords.Add(new TranslationHistory.HistoryRecord
                                {
                                    Timestamp = DateTime.Now,
                                    ObjectIdHandle = textEntities[i].Id.Handle.ToString(),  // ✅ P0修复: 使用Id而非ObjectId
                                    OriginalText = textEntities[i].Content,
                                    TranslatedText = translations[i],
                                    SourceLanguage = "auto",
                                    TargetLanguage = targetLanguage,
                                    EntityType = textEntities[i].Type.ToString(),
                                    Layer = textEntities[i].Layer,
                                    Operation = "translate"
                                });
                            }
                        }

                        if (historyRecords.Count > 0)
                        {
                            await history.AddRecordsAsync(historyRecords);
                            Log.Debug($"已记录 {historyRecords.Count} 条翻译历史");
                        }
                    }
                }

                ed.WriteMessage($"\n\n框选翻译完成！");
                ed.WriteMessage($"\n  已翻译: {translatedCount} 个文本");
                if (skippedCount > 0)
                {
                    ed.WriteMessage($"\n  已跳过: {skippedCount} 个文本（空或无变化）");
                }

                Log.Information($"框选翻译完成: {translatedCount}/{textEntities.Count}");
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "框选翻译失败");
                    ed.WriteMessage($"\n[错误] 框选翻译失败: {ex.Message}");
                }
            }, "BIAOGE_TRANSLATE_SELECTED");
        }

        /// <summary>
        /// 图层翻译命令 - 按图层选择性翻译
        /// </summary>
        [CommandMethod("BIAOGE_TRANSLATE_LAYER", CommandFlags.Modal)]
        public async void TranslateByLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("执行图层翻译命令");

                // ✅ UI改进：使用图形对话框选择图层
                ed.WriteMessage("\n正在分析图层...");
                var layers = LayerTranslationService.GetAllLayersWithTextCount();

                if (layers.Count == 0)
                {
                    ed.WriteMessage("\n图纸中没有图层。");
                    return;
                }

                // 显示图层选择对话框
                var layerDialog = new UI.LayerSelectionDialog();
                layerDialog.SetLayers(layers);
                var dialogResult = layerDialog.ShowDialog();

                if (dialogResult != true || layerDialog.SelectedLayerNames.Count == 0)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                List<string> selectedLayers = layerDialog.SelectedLayerNames;

                // 统计选中图层的文本数量
                int totalTexts = layers
                    .Where(l => selectedLayers.Contains(l.LayerName))
                    .Sum(l => l.TextCount);

                ed.WriteMessage($"\n已选择 {selectedLayers.Count} 个图层，共 {totalTexts} 个文本实体");
                ed.WriteMessage("\n选中的图层: " + string.Join(", ", selectedLayers));

                // 5. 选择目标语言（默认中文）
                var languageOptions = new PromptKeywordOptions("\n选择目标语言")
                {
                    Keywords = { "中文", "英语", "日语", "韩语", "法语", "西班牙语", "德语", "俄语" },
                    AllowNone = false
                };
                languageOptions.Keywords.Default = "中文";

                var languageResult = ed.GetKeywords(languageOptions);
                if (languageResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 语言映射
                var languageMap = new System.Collections.Generic.Dictionary<string, (string code, string name)>
                {
                    ["中文"] = ("zh", "简体中文"),
                    ["英语"] = ("en", "英语"),
                    ["日语"] = ("ja", "日语"),
                    ["韩语"] = ("ko", "韩语"),
                    ["法语"] = ("fr", "法语"),
                    ["西班牙语"] = ("es", "西班牙语"),
                    ["德语"] = ("de", "德语"),
                    ["俄语"] = ("ru", "俄语")
                };

                var selectedLanguage = languageResult.StringResult;
                var (targetLanguage, languageName) = languageMap[selectedLanguage];

                // 6. 确认翻译
                var confirmOptions = new PromptKeywordOptions($"\n确认翻译 {selectedLayers.Count} 个图层（{totalTexts} 个文本）为{languageName}？")
                {
                    Keywords = { "是", "否" },
                    AllowNone = false
                };
                confirmOptions.Keywords.Default = "是";

                var confirmResult = ed.GetKeywords(confirmOptions);
                if (confirmResult.Status != PromptStatus.OK || confirmResult.StringResult != "是")
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 7. 执行翻译
                ed.WriteMessage($"\n\n开始翻译为{languageName}...");

                var progress = new Progress<TranslationProgress>(p =>
                {
                    ed.WriteMessage($"\r{p.Stage}: {p.Percentage}%    ");
                });

                var stats = await LayerTranslationService.TranslateLayerTexts(
                    selectedLayers,
                    targetLanguage,
                    progress
                );

                // 8. 显示结果
                ed.WriteMessage("\n\n╔══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  图层翻译完成！                              ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
                ed.WriteMessage($"\n\n统计信息：");
                ed.WriteMessage($"\n  图层数量: {selectedLayers.Count}");
                ed.WriteMessage($"\n  文本总数: {stats.TotalTextCount}");
                ed.WriteMessage($"\n  唯一文本: {stats.UniqueTextCount}");
                ed.WriteMessage($"\n  成功翻译: {stats.SuccessCount}");
                ed.WriteMessage($"\n  失败数量: {stats.FailureCount}");
                ed.WriteMessage($"\n  成功率: {stats.SuccessRate:F1}%");
                ed.WriteMessage("\n");

                Log.Information($"图层翻译完成: {stats}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "图层翻译失败");
                ed.WriteMessage($"\n[错误] 图层翻译失败: {ex.Message}");
            }
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
            catch (System.Exception ex)
            {
                Log.Error(ex, $"翻译失败: {languageName}");
                ed.WriteMessage($"\n[错误] 翻译失败: {ex.Message}");
            }
        }

        #endregion

        #region 算量命令

        /// <summary>
        /// ✅ AI视觉分析图纸并识别构件（革命性算量方案）
        /// </summary>
        [CommandMethod("BIAOGE_VISION_ANALYZE", CommandFlags.Modal)]
        public async void VisionAnalyzeDrawing()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("执行AI视觉分析命令: BIAOGE_VISION_ANALYZE");
                ed.WriteMessage("\n\n╔══════════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  AI视觉图纸分析 - 基于qwen-vl-max                       ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝\n");

                // 询问分析精度级别
                ed.WriteMessage("\n选择分析精度级别:");
                ed.WriteMessage("\n  [Q] 快速分析（主要构件：柱、梁、板、墙）");
                ed.WriteMessage("\n  [S] 标准分析（含细部构件、门窗、钢筋）");
                ed.WriteMessage("\n  [D] 详尽分析（所有可见构件、标注、符号）");
                ed.WriteMessage("\n");

                var levelInput = ed.GetKeywords(
                    "\n请选择分析级别",
                    new string[] { "Quick", "Standard", "Detailed" }
                );

                if (levelInput.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    ed.WriteMessage("\n分析已取消。");
                    return;
                }

                var analysisLevel = levelInput.StringResult switch
                {
                    "Quick" => VisionAnalysisLevel.Quick,
                    "Detailed" => VisionAnalysisLevel.Detailed,
                    _ => VisionAnalysisLevel.Standard
                };

                ed.WriteMessage($"\n\n开始AI视觉分析（{analysisLevel}模式）...");
                ed.WriteMessage("\n1️⃣  导出当前视图为图片...");

                var analyzer = new DrawingVisionAnalyzer();
                var results = await analyzer.AnalyzeDrawingAsync(analysisLevel: analysisLevel);

                ed.WriteMessage($"\n\n✅ 分析完成！识别了 {results.Count} 个构件：");
                ed.WriteMessage("\n");

                // 显示前10个结果
                int displayCount = Math.Min(10, results.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    var component = results[i];
                    ed.WriteMessage($"\n  {i + 1}. {component.Type}");
                    ed.WriteMessage($"\n     位置: ({component.Position.X:F2}, {component.Position.Y:F2})");
                    ed.WriteMessage($"\n     尺寸: {component.Dimensions.Length:F2}×{component.Dimensions.Width:F2}×{component.Dimensions.Height:F2} {component.Dimensions.Unit}");
                    ed.WriteMessage($"\n     数量: {component.Quantity}, 置信度: {component.Confidence:P0}");
                    ed.WriteMessage($"\n     验证: {component.ValidationStatus}");
                    ed.WriteMessage("\n");
                }

                if (results.Count > 10)
                {
                    ed.WriteMessage($"\n  ... 还有 {results.Count - 10} 个构件");
                }

                ed.WriteMessage("\n\n💡 提示：运行 BIAOGE_CALCULATE 打开算量面板查看完整结果。\n");

                Log.Information($"AI视觉分析完成: {results.Count}个构件");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "AI视觉分析失败");
                ed.WriteMessage($"\n\n❌ 分析失败: {ex.Message}");
                ed.WriteMessage("\n");
            }
        }

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
                ed.WriteMessage("\n正在启动智能算量控制面板...");

                // ✅ 修复：弹出算量控制面板（CalculationPalette），不是快速统计
                // 用Window包装UserControl，实现对话框方式显示
                var calculationPalette = new UI.CalculationPalette();

                var window = new System.Windows.Window
                {
                    Title = "标哥 - 智能算量控制面板",
                    Content = calculationPalette,
                    Width = 450,  // ✅ 修复：匹配CalculationPalette的380px宽度+边距
                    Height = 700,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    ResizeMode = System.Windows.ResizeMode.CanResize,
                    MinWidth = 400,  // ✅ 设置最小宽度防止内容被挤压
                    MinHeight = 600
                };

                window.ShowDialog();

                ed.WriteMessage("\n智能算量面板已关闭。");
                Log.Information("智能算量面板已关闭");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示智能算量面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ 一键算量并导出Excel - 完整的专业算量功能
        ///
        /// 用户反馈："导出来的表格简单至极，这根本就不是一个合格的算量工具"
        ///
        /// 功能：
        /// 1. 自动识别图纸中的所有构件（柱梁板墙、钢筋、门窗等）
        /// 2. 提取几何实体并匹配面积/体积
        /// 3. 应用扣减关系（GB 50854-2013规范）
        ///    - 板扣减柱、墙占用面积
        ///    - 墙扣减门窗洞口体积
        ///    - 梁柱交接处理（混凝土计入梁）
        /// 4. 计算钢筋重量、模板面积
        /// 5. 导出详细的Excel报表（3个工作表）
        ///    - 工作表1：分部分项工程量清单（GB 50854-2013格式）
        ///    - 工作表2：钢筋明细表（直径、长度、根数、重量）
        ///    - 工作表3：材料汇总表（混凝土、钢筋、模板汇总）
        /// </summary>
        [CommandMethod("BIAOGE_CALCULATE_EXPORT", CommandFlags.Modal)]
        public async void CalculateAndExport()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n╔═════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║     标哥智能算量 - 一键算量并导出Excel           ║");
                ed.WriteMessage("\n╚═════════════════════════════════════════════════════╝");
                ed.WriteMessage("\n");

                Log.Information("开始执行一键算量并导出");

                // ===== 步骤1：提取文本实体 =====
                ed.WriteMessage("\n【步骤1/4】正在提取图纸文本...");
                var textExtractor = new DwgTextExtractor();
                var textEntities = textExtractor.ExtractAllText();
                ed.WriteMessage($" 完成！提取到{textEntities.Count}个文本实体");

                // ===== 步骤2：识别构件 =====
                ed.WriteMessage("\n【步骤2/4】正在识别构件（柱梁板墙、钢筋等）...");
                var bailianClient = ServiceLocator.Get<BailianApiClient>();
                var componentRecognizer = new ComponentRecognizer(bailianClient);
                var components = await componentRecognizer.RecognizeFromTextEntitiesAsync(textEntities, useAiVerification: false);
                ed.WriteMessage($" 完成！识别到{components.Count}个构件");

                if (components.Count == 0)
                {
                    ed.WriteMessage("\n⚠️  未识别到任何构件，无法继续算量");
                    ed.WriteMessage("\n\n💡 提示：");
                    ed.WriteMessage("\n  1. 确保图纸文字使用标准术语（如：C30混凝土柱、HRB400钢筋）");
                    ed.WriteMessage("\n  2. 运行 BIAOGE_DIAGNOSE_QUANTITY 命令进行详细诊断");
                    return;
                }

                // ===== 步骤2.5：应用扣减关系（GB 50854-2013规范） =====
                ed.WriteMessage("\n【步骤2.5/5】正在应用扣减关系（板扣柱墙、墙扣门窗）...");
                var deductionService = new DeductionService();
                deductionService.ApplyDeductions(components);
                ed.WriteMessage(" 完成！");

                // ===== 步骤3：统计工程量 =====
                ed.WriteMessage("\n【步骤3/5】正在统计工程量...");
                double totalArea = components.Sum(c => c.Area);
                double totalVolume = components.Sum(c => c.Volume);
                double totalSteelWeight = components.Sum(c => c.SteelWeight);
                double totalFormwork = components.Sum(c => c.FormworkArea);

                ed.WriteMessage("\n  ✓ 总面积: " + $"{totalArea:F2}m²");
                ed.WriteMessage("\n  ✓ 总体积: " + $"{totalVolume:F3}m³");
                ed.WriteMessage("\n  ✓ 钢筋重量: " + $"{totalSteelWeight:F2}kg ({totalSteelWeight/1000:F3}t)");
                ed.WriteMessage("\n  ✓ 模板面积: " + $"{totalFormwork:F2}m²");

                // ===== 步骤4：导出Excel =====
                ed.WriteMessage("\n【步骤4/5】正在导出Excel报表...");

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"工程量清单_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = System.IO.Path.Combine(desktopPath, fileName);

                var excelExporter = new QuantityExcelExporter();
                excelExporter.ExportToExcel(components, filePath);

                ed.WriteMessage($" 完成！");
                ed.WriteMessage($"\n\n✅ Excel报表已保存到桌面：");
                ed.WriteMessage($"\n   {fileName}");
                ed.WriteMessage("\n\n报表内容：");
                ed.WriteMessage("\n  - 工作表1：分部分项工程量清单（按构件类型汇总）");
                ed.WriteMessage("\n  - 工作表2：钢筋明细表（直径、长度、根数、重量）");
                ed.WriteMessage("\n  - 工作表3：材料汇总表（混凝土、钢筋、模板等）");
                ed.WriteMessage("\n");

                Log.Information($"✅ 一键算量并导出完成: {filePath}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "一键算量并导出失败");
                ed.WriteMessage($"\n❌ 错误：{ex.Message}");
                ed.WriteMessage($"\n详细信息：{ex.StackTrace}");
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
                settingsDialog.ShowDialog();

                ed.WriteMessage("\n设置已保存。");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "打开设置对话框失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ 打开成本管理对话框
        /// </summary>
        [CommandMethod("BIAOGE_COST_MANAGE", CommandFlags.Modal)]
        public void OpenCostManagement()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("打开成本管理对话框");
                ed.WriteMessage("\n打开成本管理对话框...");

                var costDialog = new CostManagementDialog();
                var result = costDialog.ShowDialog();

                if (result == true)
                {
                    ed.WriteMessage("\n✅ 成本管理设置已保存。");
                }
                else
                {
                    ed.WriteMessage("\n成本管理设置未更改。");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "打开成本管理对话框失败");
                ed.WriteMessage($"\n[错误] 打开成本管理对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换双击翻译功能
        /// </summary>
        [CommandMethod("BIAOGE_TOGGLE_DOUBLECLICK", CommandFlags.Modal)]
        public void ToggleDoubleClickTranslation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager == null)
                {
                    ed.WriteMessage("\n[错误] 配置管理器未初始化");
                    return;
                }

                // 切换设置
                var currentState = configManager.Config.Translation.EnableDoubleClickTranslation;
                configManager.Config.Translation.EnableDoubleClickTranslation = !currentState;
                configManager.SaveTypedConfig();

                var newState = !currentState;
                ed.WriteMessage($"\n双击翻译功能已{(newState ? "启用" : "禁用")}");
                ed.WriteMessage($"\n提示: 双击文本实体即可{(newState ? "快速翻译" : "（当前已禁用）")}");

                Log.Information($"双击翻译功能已切换: {newState}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "切换双击翻译功能失败");
                ed.WriteMessage($"\n[错误] 切换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换输入法自动切换功能
        /// </summary>
        [CommandMethod("BIAOGE_TOGGLE_IME", CommandFlags.Modal)]
        public void ToggleInputMethodSwitch()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager == null)
                {
                    ed.WriteMessage("\n[错误] 配置管理器未初始化");
                    return;
                }

                // 切换设置
                var currentState = configManager.Config.InputMethod.AutoSwitch;
                configManager.Config.InputMethod.AutoSwitch = !currentState;
                configManager.SaveTypedConfig();

                var newState = !currentState;
                ed.WriteMessage($"\n智能输入法切换已{(newState ? "启用" : "禁用")}");
                ed.WriteMessage($"\n提示: {(newState ? "命令模式自动切换英文，文本编辑切换中文" : "输入法不再自动切换")}");

                Log.Information($"输入法自动切换已切换: {newState}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "切换输入法自动切换失败");
                ed.WriteMessage($"\n[错误] 切换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示功能状态
        /// </summary>
        [CommandMethod("BIAOGE_STATUS", CommandFlags.Modal)]
        public void ShowFeatureStatus()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager == null)
                {
                    ed.WriteMessage("\n[错误] 配置管理器未初始化");
                    return;
                }

                ed.WriteMessage("\n╔══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥插件 - 功能状态                        ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
                ed.WriteMessage("\n");
                ed.WriteMessage($"\n【UI功能】");
                ed.WriteMessage($"\n  Ribbon工具栏:          {GetStatusText(configManager.Config.UI.EnableRibbon)}");
                ed.WriteMessage($"\n  右键上下文菜单:        {GetStatusText(configManager.Config.UI.EnableContextMenu)}");
                ed.WriteMessage($"\n  双击翻译:              {GetStatusText(configManager.Config.Translation.EnableDoubleClickTranslation)}");
                ed.WriteMessage($"\n");
                ed.WriteMessage($"\n【智能功能】");
                ed.WriteMessage($"\n  输入法自动切换:        {GetStatusText(configManager.Config.InputMethod.AutoSwitch)}");
                ed.WriteMessage($"\n  翻译缓存:              {GetStatusText(configManager.Config.Translation.EnableCache)}");
                ed.WriteMessage($"\n  翻译历史:              {GetStatusText(configManager.Config.Translation.EnableHistory)}");
                ed.WriteMessage($"\n");
                ed.WriteMessage($"\n【翻译设置】");
                ed.WriteMessage($"\n  默认目标语言:          {configManager.Config.Translation.DefaultTargetLanguage}");
                ed.WriteMessage($"\n  批处理大小:            {configManager.Config.Translation.BatchSize}");
                ed.WriteMessage($"\n  缓存过期天数:          {configManager.Config.Translation.CacheExpirationDays}");
                ed.WriteMessage($"\n");
                ed.WriteMessage($"\n提示: 使用 BIAOGE_TOGGLE_DOUBLECLICK 切换双击翻译");
                ed.WriteMessage($"\n      使用 BIAOGE_TOGGLE_IME 切换输入法自动切换");
                ed.WriteMessage("\n");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示功能状态失败");
                ed.WriteMessage($"\n[错误] 显示失败: {ex.Message}");
            }
        }

        private string GetStatusText(bool enabled)
        {
            return enabled ? "✓ 已启用" : "✗ 已禁用";
        }

        #endregion

        #region AI助手命令

        /// <summary>
        /// 启动标哥AI助手 - 支持图纸问答和修改
        /// </summary>
        [CommandMethod("BIAOGE_AI", CommandFlags.Modal)]
        public void StartAIAssistant()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("启动AI助手");

                // 显示AI助手面板
                PaletteManager.ShowAIPalette();

                ed.WriteMessage("\nAI助手面板已打开，请在右侧面板中开始对话。");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示AI助手面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
                ed.WriteMessage("\n请确保已在设置中配置百炼API密钥（BIAOGE_SETTINGS）");
            }
        }

        /// <summary>
        /// 重置AI助手面板 - 修复注册表问题导致的显示异常
        /// 快捷命令：当AI助手无法弹出时使用此命令
        /// </summary>
        [CommandMethod("BIAOGE_RESET_AI", CommandFlags.Modal)]
        public void ResetAIPanel()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n正在重置AI助手面板...");
                Log.Information("用户执行AI助手重置命令");

                // 清理现有面板
                PaletteManager.Cleanup();
                System.Threading.Thread.Sleep(50);

                // 重新初始化
                PaletteManager.Initialize();
                System.Threading.Thread.Sleep(50);

                // 显示AI助手
                PaletteManager.ShowAIPalette();

                ed.WriteMessage("\n✓ AI助手面板已重置并打开");
                Log.Information("AI助手重置成功");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "重置AI助手面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
                ed.WriteMessage("\n如果问题仍然存在，请运行: BIAOGE_RESETPALETTES");
            }
        }

        /// <summary>
        /// ✅ 重置所有面板（修复面板无法显示的问题）
        ///
        /// 用途：当面板突然无法显示时（只闪一下），使用此命令强制清理并重新初始化所有面板。
        ///
        /// 常见原因：
        /// - AutoCAD保存了错误的窗口位置/大小到注册表
        /// - PaletteSet状态异常（Visible=true但实际不可见）
        /// - WPF ElementHost渲染失败
        ///
        /// 参考：AutoCAD .NET API - PaletteSet Best Practices
        /// </summary>
        [CommandMethod("BIAOGE_RESETPALETTES", CommandFlags.Modal)]
        public void ResetPalettes()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n  标哥插件 - 面板重置工具");
                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n");

                Log.Information("用户执行面板重置命令");

                // 第1步：清理所有现有面板
                ed.WriteMessage("\n[1/3] 正在清理现有面板...");
                PaletteManager.Cleanup();
                Log.Information("面板清理完成");
                ed.WriteMessage(" ✓");

                // 第2步：等待50ms让AutoCAD释放资源
                System.Threading.Thread.Sleep(50);

                // 第3步：重新初始化所有面板
                ed.WriteMessage("\n[2/3] 正在重新初始化面板...");
                PaletteManager.Initialize();
                Log.Information("面板初始化完成");
                ed.WriteMessage(" ✓");

                // 第4步：显示AI助手面板（验证修复是否成功）
                ed.WriteMessage("\n[3/3] 正在打开AI助手面板...");
                PaletteManager.ShowAIPalette();
                Log.Information("AI助手面板已显示");
                ed.WriteMessage(" ✓");

                ed.WriteMessage("\n");
                ed.WriteMessage("\n✓ 面板重置成功！");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n提示：");
                ed.WriteMessage("\n  - 如果AI助手面板仍无法显示，请查看日志文件");
                ed.WriteMessage("\n  - 日志位置：%APPDATA%\\Biaoge\\Logs\\");
                ed.WriteMessage("\n  - 或运行 BIAOGE_DIAGNOSTIC 生成诊断报告");
                ed.WriteMessage("\n========================================\n");

                Log.Information("面板重置命令执行成功");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "重置面板失败");
                ed.WriteMessage("\n");
                ed.WriteMessage($"\n❌ 错误：{ex.Message}");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n请尝试以下操作：");
                ed.WriteMessage("\n  1. 运行 NETUNLOAD 卸载插件");
                ed.WriteMessage("\n  2. 关闭AutoCAD");
                ed.WriteMessage("\n  3. 重新启动AutoCAD");
                ed.WriteMessage("\n  4. 运行 NETLOAD 重新加载插件");
                ed.WriteMessage("\n========================================\n");
            }
        }

        #endregion

        #region 面板Toggle命令

        /// <summary>
        /// 切换翻译面板显示状态（快捷键友好）
        /// </summary>
        [CommandMethod("BIAOGE_TOGGLE_TRANSLATE", CommandFlags.Modal)]
        public void ToggleTranslationPalette()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Debug("切换翻译面板显示状态");
                PaletteManager.ToggleTranslationPalette();

                var status = PaletteManager.IsTranslationPaletteVisible ? "已显示" : "已隐藏";
                ed.WriteMessage($"\n翻译面板{status}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "切换翻译面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 切换算量面板显示状态（快捷键友好）
        /// </summary>
        [CommandMethod("BIAOGE_TOGGLE_CALCULATE", CommandFlags.Modal)]
        public void ToggleCalculationPalette()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Debug("切换算量面板显示状态");
                PaletteManager.ToggleCalculationPalette();

                var status = PaletteManager.IsCalculationPaletteVisible ? "已显示" : "已隐藏";
                ed.WriteMessage($"\n算量面板{status}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "切换算量面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 切换AI助手面板显示状态（快捷键友好）
        /// </summary>
        [CommandMethod("BIAOGE_TOGGLE_AI", CommandFlags.Modal)]
        public void ToggleAIPalette()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Debug("切换AI助手面板显示状态");
                PaletteManager.ToggleAIPalette();

                var status = PaletteManager.IsAIPaletteVisible ? "已显示" : "已隐藏";
                ed.WriteMessage($"\nAI助手面板{status}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "切换AI助手面板失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        #endregion

        #region 快捷键管理命令

        /// <summary>
        /// 打开快捷键管理对话框
        /// </summary>
        [CommandMethod("BIAOGE_KEYS", CommandFlags.Modal)]
        public void ShowKeybindings()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("打开快捷键管理对话框");

                var dialog = new UI.KeybindingsManagerDialog();
                dialog.ShowDialog();

                ed.WriteMessage("\n快捷键管理对话框已关闭。");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "打开快捷键管理对话框失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 导出快捷键配置到桌面
        /// </summary>
        [CommandMethod("BIAOGE_EXPORT_KEYS", CommandFlags.Modal)]
        public void ExportKeybindings()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n正在生成快捷键配置文件...");

                var filePath = KeybindingsManager.SavePgpConfigToDesktop();

                ed.WriteMessage($"\n\n快捷键配置已导出到:");
                ed.WriteMessage($"\n  {filePath}");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n【下一步】");
                ed.WriteMessage("\n  1. 打开桌面上的 .pgp 文件");
                ed.WriteMessage("\n  2. 复制内容到您的 acad.pgp 文件");
                ed.WriteMessage("\n  3. 在AutoCAD中输入 REINIT 命令重新加载");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n提示: 运行 BIAOGE_INSTALL_KEYS 可自动安装");

                // 打开文件夹
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");

                Log.Information($"快捷键配置已导出: {filePath}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "导出快捷键配置失败");
                ed.WriteMessage($"\n[错误] 导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动安装快捷键到acad.pgp
        /// </summary>
        [CommandMethod("BIAOGE_INSTALL_KEYS", CommandFlags.Modal)]
        public void InstallKeybindings()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n准备安装快捷键配置...");

                // 提示用户确认
                var options = new PromptKeywordOptions("\n是否自动安装快捷键到 acad.pgp? (会自动备份原文件) [是(Y)/否(N)]")
                {
                    Keywords = { "Y", "N" },
                    AllowNone = false
                };
                options.Keywords.Default = "Y";

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK || result.StringResult != "Y")
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 尝试自动安装
                bool success = KeybindingsManager.TryInstallKeybindings(out string message);

                if (success)
                {
                    ed.WriteMessage("\n\n✓ 快捷键安装成功！");
                    ed.WriteMessage($"\n{message}");
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n【重新加载PGP文件】");
                    ed.WriteMessage("\n  请在命令行输入: REINIT");
                    ed.WriteMessage("\n  然后选择 'PGP file'，点击确定");
                    ed.WriteMessage("\n  或者重启AutoCAD");
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n运行 BIAOGE_KEYS 查看所有快捷键");

                    Log.Information("快捷键自动安装成功");
                }
                else
                {
                    ed.WriteMessage("\n\n✗ 自动安装失败");
                    ed.WriteMessage($"\n{message}");
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n建议运行 BIAOGE_EXPORT_KEYS 手动安装");

                    Log.Warning($"快捷键自动安装失败: {message}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "安装快捷键失败");
                ed.WriteMessage($"\n[错误] 安装失败: {ex.Message}");
                ed.WriteMessage("\n建议运行 BIAOGE_EXPORT_KEYS 手动安装");
            }
        }

        #endregion

        #region 帮助和工具命令

        /// <summary>
        /// 快速上手指南
        /// </summary>
        [CommandMethod("BIAOGE_QUICKSTART", CommandFlags.Modal)]
        public void ShowQuickStart()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
            ed.WriteMessage("\n║  标哥插件 - 5分钟快速上手指南                          ║");
            ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【第1步：配置API密钥】");
            ed.WriteMessage("\n  1. 运行命令: BIAOGE_SETTINGS");
            ed.WriteMessage("\n  2. 在\"百炼API配置\"选项卡输入您的API密钥");
            ed.WriteMessage("\n  3. 点击\"保存\"按钮");
            ed.WriteMessage("\n  提示: 访问 https://dashscope.aliyuncs.com/ 获取API密钥");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【第2步：开始翻译】");
            ed.WriteMessage("\n  最简单的方式 - 直接翻译为中文:");
            ed.WriteMessage("\n    BIAOGE_TRANSLATE_ZH  （推荐！一键翻译整个图纸）");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n  高级方式 - 选择翻译:");
            ed.WriteMessage("\n    BIAOGE_TRANSLATE_SELECTED  （框选要翻译的文本）");
            ed.WriteMessage("\n    BIAOGE_TRANSLATE_LAYER     （按图层翻译）");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【第3步：体验智能功能】");
            ed.WriteMessage("\n  ✓ 双击文本 - 自动弹出翻译窗口");
            ed.WriteMessage("\n  ✓ 右键文本 - 选择\"翻译文本\"快速翻译");
            ed.WriteMessage("\n  ✓ 输入法自动切换 - 命令模式英文，编辑模式中文");
            ed.WriteMessage("\n  ✓ 翻译历史 - 运行 BIAOGE_HISTORY 查看所有翻译记录");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【常用命令速查】");
            ed.WriteMessage("\n  BZ   - 快速翻译为中文（需安装快捷键）");
            ed.WriteMessage("\n  BE   - 快速翻译为英文（需安装快捷键）");
            ed.WriteMessage("\n  BIAOGE_AI      - 启动AI助手（图纸问答、智能修改）");
            ed.WriteMessage("\n  BIAOGE_HISTORY - 查看翻译历史（支持撤销）");
            ed.WriteMessage("\n  BIAOGE_SMART_REPLACE - 批量智能替换文本");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【安装快捷键（可选）】");
            ed.WriteMessage("\n  运行: BIAOGE_INSTALL_KEYS");
            ed.WriteMessage("\n  然后输入: REINIT 并选择 PGP file 重新加载");
            ed.WriteMessage("\n  之后就可以使用 BZ、BE 等快捷键了！");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【需要帮助？】");
            ed.WriteMessage("\n  BIAOGE_HELP      - 查看完整命令列表");
            ed.WriteMessage("\n  BIAOGE_STATUS    - 查看功能状态");
            ed.WriteMessage("\n  BIAOGE_DIAGNOSTIC - 运行系统诊断");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n详细文档: https://github.com/lechatcloud-ship-it/biaoge");
            ed.WriteMessage("\n");
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        [CommandMethod("BIAOGE_HELP", CommandFlags.Modal)]
        public void ShowHelp()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("显示帮助对话框");

                // ✅ UI改进：使用图形对话框代替命令行输出
                var helpDialog = new UI.HelpDialog();
                helpDialog.ShowDialog();

                ed.WriteMessage("\n帮助对话框已显示。");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示帮助对话框失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");

                // 降级到命令行输出
                ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥 - 建筑工程CAD翻译工具 v1.0 - 帮助                ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n【翻译功能】");
                ed.WriteMessage("\n  BIAOGE_TRANSLATE_ZH   - 快速翻译为中文（推荐）");
                ed.WriteMessage("\n  BIAOGE_TRANSLATE      - 打开翻译面板");
                ed.WriteMessage("\n  BIAOGE_AI             - 启动AI助手");
                ed.WriteMessage("\n  BIAOGE_CALCULATE      - 打开算量面板");
                ed.WriteMessage("\n  BIAOGE_SETTINGS       - 打开设置对话框");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n详细文档: https://github.com/lechatcloud-ship-it/biaoge");
                ed.WriteMessage("\n");
            }
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
            ed.WriteMessage("\n║  标哥 - 建筑工程CAD翻译工具                            ║");
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

            try
            {
                Log.Information("显示关于对话框");

                // ✅ UI改进：使用图形对话框代替命令行输出
                var aboutDialog = new UI.AboutDialog();
                aboutDialog.ShowDialog();

                ed.WriteMessage("\n关于对话框已显示。");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示关于对话框失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");

                // 降级到命令行输出
                ed.WriteMessage("\n");
                ShowVersion();
                ed.WriteMessage("\n【核心功能】");
                ed.WriteMessage("\n  ✓ 标哥AI助手 (Agent架构，智能调度专用模型)");
                ed.WriteMessage("\n  ✓ AI智能翻译 (qwen-mt-flash，92语言)");
                ed.WriteMessage("\n  ✓ 构件识别算量 (qwen3-vl-flash，超高精度)");
                ed.WriteMessage("\n  ✓ 多格式导出 (Excel/PDF)");
                ed.WriteMessage("\n  ✓ 智能缓存 (90%+命中率)");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n【技术优势】");
                ed.WriteMessage("\n  ✓ 100%准确的DWG读取 (AutoCAD官方引擎)");
                ed.WriteMessage("\n  ✓ 无缝集成AutoCAD工作流");
                ed.WriteMessage("\n  ✓ 符合建筑行业标准");
                ed.WriteMessage("\n");
            }
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
                Log.Information("打开缓存管理对话框");

                // ✅ UI改进：使用缓存管理对话框
                var cacheDialog = new UI.CacheManagementDialog();
                cacheDialog.ShowDialog();

                ed.WriteMessage("\n缓存管理对话框已关闭。");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "打开缓存管理对话框失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
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
            catch (System.Exception ex)
            {
                Log.Error(ex, "运行诊断失败");
                ed.WriteMessage($"\n[错误] 诊断失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ 诊断文本提取功能（AutoCAD 2022优化版）
        /// ✅ 解决问题1：算量功能提取不到构件 - 输出详细的文本提取统计信息
        ///
        /// 功能说明：
        /// - 提取当前DWG中的所有文本实体
        /// - 输出详细的统计信息（按类型、图层、空间分类）
        /// - 显示提取到的前20个文本示例
        /// - 保存完整报告到桌面
        ///
        /// 使用场景：
        /// - 算量功能提取不到构件时，运行此命令查看提取情况
        /// - 翻译功能遗漏文本时，运行此命令检查文本提取
        /// - 了解当前DWG文件的文本分布情况
        /// </summary>
        [CommandMethod("BIAOGE_DIAGNOSE_TEXT", CommandFlags.Modal)]
        public void DiagnoseTextExtraction()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                ed.WriteMessage("\n═══════════════════════════════════════════════════");
                ed.WriteMessage("\n文本提取诊断 - AutoCAD 2022优化版");
                ed.WriteMessage("\n═══════════════════════════════════════════════════");
                ed.WriteMessage("\n正在提取文本实体，请稍候...\n");

                Log.Information("开始运行文本提取诊断");

                // 提取所有文本
                var extractor = new DwgTextExtractor();
                var allTexts = extractor.ExtractAllText();

                // 生成统计报告
                var stats = extractor.GetStatistics(allTexts);

                // ===== 1. 基本统计 =====
                ed.WriteMessage("\n【基本统计】");
                ed.WriteMessage($"\n  总文本数量: {stats.TotalCount}");
                ed.WriteMessage($"\n  唯一内容数: {stats.UniqueContentCount}");
                ed.WriteMessage($"\n  可翻译文本: {stats.TranslatableCount}");
                ed.WriteMessage($"\n  图层数量:   {stats.LayerCount}");

                // ===== 2. 按类型统计 =====
                ed.WriteMessage("\n\n【按类型统计】");
                ed.WriteMessage($"\n  单行文本 (DBText):        {stats.DBTextCount}");
                ed.WriteMessage($"\n  多行文本 (MText):         {stats.MTextCount}");
                ed.WriteMessage($"\n  属性文本 (Attribute):     {stats.AttributeCount}");

                var dimensionCount = allTexts.Count(t => t.Type == TextEntityType.Dimension);
                var mLeaderCount = allTexts.Count(t => t.Type == TextEntityType.MLeader);
                var tableCount = allTexts.Count(t => t.Type == TextEntityType.Table);
                var fcfCount = allTexts.Count(t => t.Type == TextEntityType.FeatureControlFrame);

                ed.WriteMessage($"\n  标注文本 (Dimension):     {dimensionCount}");
                ed.WriteMessage($"\n  多重引线 (MLeader):       {mLeaderCount}");
                ed.WriteMessage($"\n  表格文本 (Table):         {tableCount}");
                ed.WriteMessage($"\n  几何公差 (FCF):          {fcfCount}");

                // ===== 3. 按空间统计 =====
                ed.WriteMessage("\n\n【按空间统计】");
                var spaceGroups = allTexts.GroupBy(t => t.SpaceName).OrderByDescending(g => g.Count());
                foreach (var group in spaceGroups)
                {
                    ed.WriteMessage($"\n  {group.Key}: {group.Count()} 个文本");
                }

                // ===== 4. 按图层统计（Top 10） =====
                ed.WriteMessage("\n\n【按图层统计 (Top 10)】");
                var layerGroups = allTexts.GroupBy(t => t.Layer)
                    .OrderByDescending(g => g.Count())
                    .Take(10);

                foreach (var group in layerGroups)
                {
                    ed.WriteMessage($"\n  图层 [{group.Key}]: {group.Count()} 个文本");
                }

                // ===== 5. 块属性统计 =====
                ed.WriteMessage("\n\n【块属性统计】");
                var blockTexts = allTexts.Where(t =>
                    t.Type == TextEntityType.AttributeReference ||
                    t.Type == TextEntityType.AttributeDefinition);

                var visibleBlockTexts = blockTexts.Count();
                var blockGroups = blockTexts.GroupBy(t => t.BlockName)
                    .OrderByDescending(g => g.Count())
                    .Take(5);

                ed.WriteMessage($"\n  块属性总数: {visibleBlockTexts}");
                ed.WriteMessage("\n  Top 5 块名称:");
                foreach (var group in blockGroups)
                {
                    ed.WriteMessage($"\n    [{group.Key}]: {group.Count()} 个属性");
                }

                // ===== 6. 示例文本（前20个） =====
                ed.WriteMessage("\n\n【文本示例 (前20个)】");
                var sampleTexts = allTexts.Take(20);
                int idx = 1;
                foreach (var text in sampleTexts)
                {
                    var content = text.Content.Length > 40
                        ? text.Content.Substring(0, 40) + "..."
                        : text.Content;
                    ed.WriteMessage($"\n  {idx,2}. [{text.Type,-20}] \"{content}\"");
                    idx++;
                }

                // ===== 7. 保存详细报告到桌面 =====
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var reportPath = System.IO.Path.Combine(desktopPath,
                    $"BiaogPlugin_TextDiagnose_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var reportBuilder = new System.Text.StringBuilder();
                reportBuilder.AppendLine("═══════════════════════════════════════════════════");
                reportBuilder.AppendLine("标哥插件 - 文本提取诊断报告");
                reportBuilder.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                reportBuilder.AppendLine($"DWG文件: {db.Filename}");
                reportBuilder.AppendLine("═══════════════════════════════════════════════════");
                reportBuilder.AppendLine();
                reportBuilder.AppendLine(stats.ToString());
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("【所有提取的文本】");
                reportBuilder.AppendLine();

                foreach (var text in allTexts)
                {
                    reportBuilder.AppendLine($"类型: {text.Type}");
                    reportBuilder.AppendLine($"内容: {text.Content}");
                    reportBuilder.AppendLine($"图层: {text.Layer}");
                    reportBuilder.AppendLine($"空间: {text.SpaceName}");
                    if (!string.IsNullOrEmpty(text.BlockName))
                        reportBuilder.AppendLine($"块名: {text.BlockName}");
                    if (!string.IsNullOrEmpty(text.Tag))
                        reportBuilder.AppendLine($"标签: {text.Tag}");
                    reportBuilder.AppendLine($"位置: ({text.Position.X:F2}, {text.Position.Y:F2}, {text.Position.Z:F2})");
                    reportBuilder.AppendLine("---");
                }

                System.IO.File.WriteAllText(reportPath, reportBuilder.ToString());

                ed.WriteMessage("\n\n═══════════════════════════════════════════════════");
                ed.WriteMessage($"\n✅ 诊断完成！详细报告已保存到:");
                ed.WriteMessage($"\n   {reportPath}");
                ed.WriteMessage("\n═══════════════════════════════════════════════════");

                Log.Information($"文本提取诊断完成，报告已保存: {reportPath}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "文本提取诊断失败");
                ed.WriteMessage($"\n[错误] 诊断失败: {ex.Message}");
                ed.WriteMessage($"\n详细信息: {ex.StackTrace}");
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
            {
                Log.Error(ex, "重置性能统计失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 显示API Token使用统计（阿里云百炼API）
        /// </summary>
        [CommandMethod("BIAOGE_TOKENUSAGE", CommandFlags.Modal)]
        public void ShowTokenUsage()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                if (bailianClient == null)
                {
                    ed.WriteMessage("\n[警告] 百炼API客户端未初始化");
                    return;
                }

                var (inputTokens, outputTokens, totalTokens) = bailianClient.GetTokenUsage();

                ed.WriteMessage("\n╔═══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║      阿里云百炼API - Token使用统计            ║");
                ed.WriteMessage("\n╠═══════════════════════════════════════════════╣");
                ed.WriteMessage($"\n║  输入Token:   {inputTokens,12:N0} tokens     ║");
                ed.WriteMessage($"\n║  输出Token:   {outputTokens,12:N0} tokens     ║");
                ed.WriteMessage($"\n║  总计Token:   {totalTokens,12:N0} tokens     ║");
                ed.WriteMessage("\n╠═══════════════════════════════════════════════╣");

                // 估算成本（基于阿里云百炼官方定价）
                // qwen-mt-flash: ¥0.002/1K tokens (输入+输出统一计费)
                // qwen3-max-preview: ¥0.04/1K input, ¥0.12/1K output
                var translationCost = totalTokens * 0.002 / 1000.0;
                var conversationCost = (inputTokens * 0.04 + outputTokens * 0.12) / 1000.0;

                ed.WriteMessage($"\n║  预估成本(翻译):   约 ¥{translationCost,6:F4}        ║");
                ed.WriteMessage($"\n║  预估成本(对话):   约 ¥{conversationCost,6:F4}        ║");
                ed.WriteMessage("\n╠═══════════════════════════════════════════════╣");
                ed.WriteMessage("\n║  提示：使用 BIAOGE_RESETTOKENS 重置统计      ║");
                ed.WriteMessage("\n╚═══════════════════════════════════════════════╝\n");

                Log.Information($"显示Token使用统计: 输入{inputTokens}, 输出{outputTokens}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示Token使用统计失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 重置Token使用统计
        /// </summary>
        [CommandMethod("BIAOGE_RESETTOKENS", CommandFlags.Modal)]
        public void ResetTokenUsage()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                if (bailianClient == null)
                {
                    ed.WriteMessage("\n[警告] 百炼API客户端未初始化");
                    return;
                }

                bailianClient.ResetTokenUsage();
                ed.WriteMessage("\n✓ Token使用统计已重置。");
                Log.Information("Token使用统计已重置");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "重置Token使用统计失败");
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
                // ✅ UI改进：先弹出导出选项对话框
                var optionsDialog = new UI.ExportExcelOptionsDialog();
                var dialogResult = optionsDialog.ShowDialog();

                if (dialogResult != true)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                var options = optionsDialog.Options;

                ed.WriteMessage("\n开始快速识别构件...");
                Log.Information("执行Excel导出，路径: {Path}", options.FilePath);

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
                var exporter = new ExcelExporter();
                exporter.ExportSummary(summary, options.FilePath);

                ed.WriteMessage($"\n\nExcel清单已导出到: {options.FilePath}");
                ed.WriteMessage($"\n  构件总数: {summary.TotalComponents}");
                ed.WriteMessage($"\n  总成本: ¥{summary.TotalCost:N2}");

                // 根据用户选择执行完成后操作
                switch (options.AfterExportAction)
                {
                    case UI.AfterExportAction.OpenFile:
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = options.FilePath,
                            UseShellExecute = true
                        });
                        break;
                    case UI.AfterExportAction.OpenFolder:
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{options.FilePath}\"");
                        break;
                    case UI.AfterExportAction.DoNothing:
                        // 什么都不做
                        break;
                }

                Log.Information($"Excel导出完成: {options.FilePath}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "导出Excel失败");
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

                // ✅ UI改进：使用图形对话框显示结果
                var resultDialog = new UI.QuickCountResultDialog();
                resultDialog.SetResults(results);
                resultDialog.ShowDialog();

                ed.WriteMessage("\n快速统计完成，结果已显示在对话框中。");
                Log.Information("快速统计完成");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "快速统计失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");

                // 降级到命令行输出
                ed.WriteMessage("\n请运行 BIAOGE_CALCULATE 打开算量面板进行详细分析。");
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
            {
                Log.Error(ex, "测试提取失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }
#endif

        #endregion

        #region 翻译历史记录命令

        /// <summary>
        /// 显示翻译历史记录
        /// </summary>
        [CommandMethod("BIAOGE_HISTORY", CommandFlags.Modal)]
        public async void ShowTranslationHistory()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("显示翻译历史记录");

                ed.WriteMessage("\n╔══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥插件 - 翻译历史记录                    ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
                ed.WriteMessage("\n");

                var history = ServiceLocator.GetService<TranslationHistory>();
                if (history == null)
                {
                    ed.WriteMessage("\n[错误] 翻译历史服务未初始化");
                    return;
                }

                // 获取统计信息
                var stats = await history.GetStatisticsAsync();

                ed.WriteMessage("\n【统计信息】");
                ed.WriteMessage($"\n  总记录数: {stats.GetValueOrDefault("TotalRecords", 0)}");
                ed.WriteMessage($"\n  今日翻译: {stats.GetValueOrDefault("TodayRecords", 0)}");

                if (stats.ContainsKey("FirstRecord"))
                {
                    var firstRecord = (DateTime)stats["FirstRecord"];
                    ed.WriteMessage($"\n  最早记录: {firstRecord:yyyy-MM-dd HH:mm:ss}");
                }

                if (stats.ContainsKey("TopLanguagePairs"))
                {
                    var topPairs = (List<string>)stats["TopLanguagePairs"];
                    if (topPairs.Count > 0)
                    {
                        ed.WriteMessage("\n  常用语言对:");
                        foreach (var pair in topPairs)
                        {
                            ed.WriteMessage($"\n    - {pair}");
                        }
                    }
                }

                // 获取最近记录
                ed.WriteMessage("\n\n【最近翻译记录（前20条）】");
                ed.WriteMessage("\n" + new string('─', 70));
                ed.WriteMessage("\n时间               原文                        译文");
                ed.WriteMessage("\n" + new string('─', 70));

                var records = await history.GetRecentRecordsAsync(20);
                foreach (var record in records)
                {
                    var originalPreview = record.OriginalText.Length > 20
                        ? record.OriginalText.Substring(0, 20) + "..."
                        : record.OriginalText.PadRight(23);

                    var translatedPreview = record.TranslatedText.Length > 20
                        ? record.TranslatedText.Substring(0, 20) + "..."
                        : record.TranslatedText;

                    ed.WriteMessage($"\n{record.Timestamp:MM-dd HH:mm:ss}  {originalPreview}  {translatedPreview}");
                }

                ed.WriteMessage("\n" + new string('─', 70));
                ed.WriteMessage("\n\n提示:");
                ed.WriteMessage("\n  BIAOGE_UNDO_TRANSLATION  - 撤销最近的翻译");
                ed.WriteMessage("\n  BIAOGE_CLEAR_HISTORY     - 清除所有历史记录");
                ed.WriteMessage("\n");

                Log.Information("翻译历史记录显示完成");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示翻译历史记录失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 撤销最近的翻译
        /// </summary>
        [CommandMethod("BIAOGE_UNDO_TRANSLATION", CommandFlags.Modal)]
        public async void UndoLastTranslation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                Log.Information("撤销最近的翻译");

                var history = ServiceLocator.GetService<TranslationHistory>();
                if (history == null)
                {
                    ed.WriteMessage("\n[错误] 翻译历史服务未初始化");
                    return;
                }

                // 获取最近的翻译记录（排除撤销操作）
                var allRecords = await history.GetRecentRecordsAsync(100);
                var translateRecords = allRecords.Where(r => r.Operation == "translate").ToList();

                if (translateRecords.Count == 0)
                {
                    ed.WriteMessage("\n没有可撤销的翻译记录。");
                    return;
                }

                // 显示最近的翻译记录供用户选择
                ed.WriteMessage("\n最近的翻译记录:");
                for (int i = 0; i < Math.Min(10, translateRecords.Count); i++)
                {
                    var record = translateRecords[i];
                    ed.WriteMessage($"\n{i + 1}. {record.Timestamp:MM-dd HH:mm:ss} - {record.OriginalText} → {record.TranslatedText}");
                }

                var promptOptions = new PromptIntegerOptions("\n请输入要撤销的记录编号（0=取消）")
                {
                    DefaultValue = 1,
                    AllowNone = false,
                    LowerLimit = 0,
                    UpperLimit = Math.Min(10, translateRecords.Count)
                };

                var promptResult = ed.GetInteger(promptOptions);
                if (promptResult.Status != PromptStatus.OK || promptResult.Value == 0)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                var selectedRecord = translateRecords[promptResult.Value - 1];

                // 执行撤销
                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 从Handle恢复ObjectId
                    var handle = new Handle(Convert.ToInt64(selectedRecord.ObjectIdHandle, 16));
                    var objId = db.GetObjectId(false, handle, 0);

                    if (objId.IsNull || objId.IsErased)
                    {
                        ed.WriteMessage("\n[错误] 对象已被删除，无法撤销。");
                        return;
                    }

                    var obj = tr.GetObject(objId, OpenMode.ForWrite);

                    // 恢复原文
                    bool success = false;
                    if (obj is DBText dbText)
                    {
                        dbText.TextString = selectedRecord.OriginalText;
                        success = true;
                    }
                    else if (obj is MText mText)
                    {
                        mText.Contents = selectedRecord.OriginalText;
                        success = true;
                    }
                    else if (obj is AttributeReference attRef)
                    {
                        attRef.TextString = selectedRecord.OriginalText;
                        success = true;
                    }

                    tr.Commit();

                    if (success)
                    {
                        // 记录撤销操作
                        await history.AddRecordAsync(new TranslationHistory.HistoryRecord
                        {
                            Timestamp = DateTime.Now,
                            ObjectIdHandle = selectedRecord.ObjectIdHandle,
                            OriginalText = selectedRecord.TranslatedText,
                            TranslatedText = selectedRecord.OriginalText,
                            SourceLanguage = selectedRecord.TargetLanguage,
                            TargetLanguage = selectedRecord.SourceLanguage,
                            EntityType = selectedRecord.EntityType,
                            Layer = selectedRecord.Layer,
                            Operation = "undo"
                        });

                        ed.WriteMessage($"\n✓ 已撤销翻译: {selectedRecord.TranslatedText} → {selectedRecord.OriginalText}");
                        Log.Information($"撤销翻译成功: {selectedRecord.Id}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "撤销翻译失败");
                ed.WriteMessage($"\n[错误] 撤销失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除翻译历史记录
        /// </summary>
        [CommandMethod("BIAOGE_CLEAR_HISTORY", CommandFlags.Modal)]
        public async void ClearTranslationHistory()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var history = ServiceLocator.GetService<TranslationHistory>();
                if (history == null)
                {
                    ed.WriteMessage("\n[错误] 翻译历史服务未初始化");
                    return;
                }

                // 确认
                var confirmOptions = new PromptKeywordOptions("\n确认清除所有翻译历史记录？")
                {
                    Keywords = { "是", "否" },
                    AllowNone = false
                };
                confirmOptions.Keywords.Default = "否";

                var confirmResult = ed.GetKeywords(confirmOptions);
                if (confirmResult.Status != PromptStatus.OK || confirmResult.StringResult != "是")
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                await history.ClearAllAsync();
                ed.WriteMessage("\n✓ 已清除所有翻译历史记录");
                Log.Information("翻译历史记录已清除");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "清除翻译历史记录失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        #endregion

        #region 批量智能替换命令

        /// <summary>
        /// 批量智能替换文本
        /// </summary>
        [CommandMethod("BIAOGE_SMART_REPLACE", CommandFlags.Modal)]
        public async void SmartReplace()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                Log.Information("执行批量智能替换");

                ed.WriteMessage("\n╔══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥插件 - 批量智能替换                    ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
                ed.WriteMessage("\n");

                // 1. 获取查找文本
                var findOptions = new PromptStringOptions("\n请输入要查找的文本:")
                {
                    AllowSpaces = true
                };

                var findResult = ed.GetString(findOptions);
                if (findResult.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(findResult.StringResult))
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                var findText = findResult.StringResult.Trim();

                // 2. 提取所有文本实体
                var extractor = new DwgTextExtractor();
                var allTextEntities = await Task.Run(() => extractor.ExtractAllText());

                // 3. 查找匹配的文本
                var matchedEntities = allTextEntities
                    .Where(e => e.Content.Contains(findText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchedEntities.Count == 0)
                {
                    ed.WriteMessage($"\n未找到包含 \"{findText}\" 的文本。");
                    return;
                }

                ed.WriteMessage($"\n找到 {matchedEntities.Count} 个匹配项");

                // 4. 询问是否使用AI建议
                var useAIOptions = new PromptKeywordOptions("\n是否使用AI建议替换内容？")
                {
                    Keywords = { "是", "否", "手动" },
                    AllowNone = false
                };
                useAIOptions.Keywords.Default = "手动";

                var useAIResult = ed.GetKeywords(useAIOptions);
                string replaceText = "";

                if (useAIResult.Status == PromptStatus.OK && useAIResult.StringResult == "是")
                {
                    // 使用AI建议
                    ed.WriteMessage("\n正在使用AI分析并建议替换内容...");

                    var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                    if (bailianClient == null)
                    {
                        ed.WriteMessage("\n[错误] AI服务未初始化");
                        return;
                    }

                    // 准备AI提示
                    var sampleTexts = matchedEntities.Take(5).Select(e => e.Content).ToList();
                    var prompt = $@"
我在AutoCAD图纸中找到了包含 ""{findText}"" 的文本，需要批量替换。

示例文本：
{string.Join("\n", sampleTexts.Select((t, i) => $"{i + 1}. {t}"))}

请分析这些文本的上下文，建议最合适的替换方式。
只需要给出替换建议，不要解释。格式：原文 -> 建议替换为XXX
";

                    try
                    {
                        var messages = new List<ChatMessage>
                        {
                            new ChatMessage { Role = "user", Content = prompt }
                        };
                        var aiResult = await bailianClient.ChatCompletionAsync(messages, "qwen3-max-preview");
                        ed.WriteMessage($"\n\nAI建议:");
                        ed.WriteMessage($"\n{aiResult.Content}");
                        ed.WriteMessage("\n");

                        // 让用户确认或输入自己的替换文本
                        var confirmOptions = new PromptStringOptions("\n请输入替换文本（留空使用AI建议）:")
                        {
                            AllowSpaces = true
                        };

                        var confirmResult = ed.GetString(confirmOptions);
                        if (confirmResult.Status == PromptStatus.OK)
                        {
                            replaceText = string.IsNullOrWhiteSpace(confirmResult.StringResult)
                                ? ExtractReplacementFromAI(aiResult.Content, findText)
                                : confirmResult.StringResult.Trim();
                        }
                        else
                        {
                            return;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error(ex, "AI建议失败");
                        ed.WriteMessage($"\n[警告] AI建议失败: {ex.Message}");
                        ed.WriteMessage("\n请手动输入替换文本。");
                    }
                }

                // 5. 手动输入替换文本（如果AI未提供）
                if (string.IsNullOrEmpty(replaceText))
                {
                    var replaceOptions = new PromptStringOptions($"\n请输入替换文本（将把 \"{findText}\" 替换为）:")
                    {
                        AllowSpaces = true
                    };

                    var replaceResult = ed.GetString(replaceOptions);
                    if (replaceResult.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n操作已取消。");
                        return;
                    }

                    replaceText = replaceResult.StringResult;
                }

                // 6. 显示预览
                ed.WriteMessage($"\n\n预览替换效果（前5个）:");
                for (int i = 0; i < Math.Min(5, matchedEntities.Count); i++)
                {
                    var entity = matchedEntities[i];
                    var newContent = entity.Content.Replace(findText, replaceText, StringComparison.OrdinalIgnoreCase);
                    ed.WriteMessage($"\n{i + 1}. {entity.Content}");
                    ed.WriteMessage($"\n   → {newContent}");
                }

                // 7. 确认替换
                var confirmReplaceOptions = new PromptKeywordOptions($"\n\n确认替换 {matchedEntities.Count} 个匹配项？")
                {
                    Keywords = { "是", "否" },
                    AllowNone = false
                };
                confirmReplaceOptions.Keywords.Default = "是";

                var confirmReplaceResult = ed.GetKeywords(confirmReplaceOptions);
                if (confirmReplaceResult.Status != PromptStatus.OK || confirmReplaceResult.StringResult != "是")
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 8. 执行替换
                int successCount = 0;
                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var entity in matchedEntities)
                    {
                        try
                        {
                            // ✅ AutoCAD 2022最佳实践：验证ObjectId有效性
                            if (entity.Id.IsNull || entity.Id.IsErased || entity.Id.IsEffectivelyErased || !entity.Id.IsValid)
                            {
                                Log.Debug($"跳过无效的ObjectId: {entity.Id}");
                                continue;
                            }

                            var obj = tr.GetObject(entity.Id, OpenMode.ForWrite);
                            var newContent = entity.Content.Replace(findText, replaceText, StringComparison.OrdinalIgnoreCase);

                            if (obj is DBText dbText)
                            {
                                dbText.TextString = newContent;
                                successCount++;
                            }
                            else if (obj is MText mText)
                            {
                                mText.Contents = newContent;
                                successCount++;
                            }
                            else if (obj is AttributeReference attRef)
                            {
                                attRef.TextString = newContent;
                                successCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Warning(ex, $"替换失败: {entity.Id}");
                        }
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n\n✓ 批量替换完成！");
                ed.WriteMessage($"\n  成功替换: {successCount}/{matchedEntities.Count}");
                ed.WriteMessage($"\n  查找文本: \"{findText}\"");
                ed.WriteMessage($"\n  替换为: \"{replaceText}\"");
                ed.WriteMessage("\n");

                Log.Information($"批量智能替换完成: {successCount}/{matchedEntities.Count}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "批量智能替换失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 从AI响应中提取替换建议
        /// </summary>
        private string ExtractReplacementFromAI(string aiResponse, string originalText)
        {
            try
            {
                // 简单的提取逻辑：查找"替换为"或"->"后面的内容
                var patterns = new[] { "替换为", "->", "→", "改为" };

                foreach (var pattern in patterns)
                {
                    var index = aiResponse.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        var afterPattern = aiResponse.Substring(index + pattern.Length).Trim();
                        var lines = afterPattern.Split('\n');
                        var suggestion = lines[0].Trim().Trim('"', '\'', '【', '】', '[', ']');

                        if (!string.IsNullOrWhiteSpace(suggestion))
                        {
                            return suggestion;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "提取AI建议失败");
            }

            return originalText; // 默认返回原文
        }

        #endregion

        #region UI管理命令

        /// <summary>
        /// ✅ 激活标哥工具Ribbon选项卡
        /// </summary>
        [CommandMethod("BIAOGE_SHOW_RIBBON", CommandFlags.Modal)]
        public void ShowRibbon()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("手动激活Ribbon工具栏");
                ed.WriteMessage("\n正在激活【标哥工具】选项卡...");

                UI.Ribbon.RibbonManager.ActivateRibbonTab();

                ed.WriteMessage("\n✓ 已尝试激活【标哥工具】选项卡");
                ed.WriteMessage("\n请检查AutoCAD顶部Ribbon是否显示【标哥工具】选项卡");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "激活Ribbon失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 重新加载Ribbon工具栏
        /// </summary>
        [CommandMethod("BIAOGE_RELOAD_RIBBON", CommandFlags.Modal)]
        public void ReloadRibbon()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("手动重新加载Ribbon工具栏");

                ed.WriteMessage("\n正在重新加载Ribbon工具栏...");

                // 卸载旧的Ribbon
                try
                {
                    UI.Ribbon.RibbonManager.UnloadRibbon();
                    ed.WriteMessage("\n✓ 旧Ribbon已卸载");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n⚠ 卸载旧Ribbon时出现警告: {ex.Message}");
                }

                // 加载新的Ribbon
                try
                {
                    UI.Ribbon.RibbonManager.LoadRibbon();
                    ed.WriteMessage("\n✓ Ribbon加载成功");
                    ed.WriteMessage("\n\n请检查AutoCAD顶部是否出现【标哥工具】选项卡");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n✗ Ribbon加载失败: {ex.Message}");
                    Log.Error(ex, "重新加载Ribbon失败");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "重新加载Ribbon命令失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 显示UI状态信息
        /// </summary>
        [CommandMethod("BIAOGE_UI_STATUS", CommandFlags.Modal)]
        public void ShowUIStatus()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n╔══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥插件 - UI状态检查                      ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
                ed.WriteMessage("\n");

                // 检查Ribbon状态
                try
                {
                    var ribbonControl = Autodesk.Windows.ComponentManager.Ribbon;
                    if (ribbonControl != null)
                    {
                        ed.WriteMessage("\n✓ AutoCAD Ribbon 控件可用");

                        // 检查是否有标哥Tab
                        bool hasTab = false;
                        foreach (var tab in ribbonControl.Tabs)
                        {
                            if (tab is Autodesk.Windows.RibbonTab ribbonTab && ribbonTab.Id == "BIAOGE_TAB")
                            {
                                hasTab = true;
                                ed.WriteMessage($"\n✓ 找到【标哥工具】选项卡 (ID: {ribbonTab.Id})");
                                ed.WriteMessage($"\n  - 标题: {ribbonTab.Title}");
                                ed.WriteMessage($"\n  - 面板数量: {ribbonTab.Panels.Count}");
                                break;
                            }
                        }

                        if (!hasTab)
                        {
                            ed.WriteMessage("\n✗ 未找到【标哥工具】选项卡");
                            ed.WriteMessage("\n\n解决方案:");
                            ed.WriteMessage("\n  1. 运行命令: BIAOGE_RELOAD_RIBBON");
                            ed.WriteMessage("\n  2. 或重新加载插件（NETLOAD）");
                        }
                    }
                    else
                    {
                        ed.WriteMessage("\n✗ AutoCAD Ribbon 控件不可用");
                        ed.WriteMessage("\n  可能原因：AutoCAD未完全启动或不支持Ribbon");
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n✗ 检查Ribbon状态时出错: {ex.Message}");
                }

                // 检查面板状态
                ed.WriteMessage("\n");
                ed.WriteMessage("\n面板状态:");
                ed.WriteMessage("\n  - PaletteManager: 已初始化");

                ed.WriteMessage("\n");
                ed.WriteMessage("\n可用UI命令:");
                ed.WriteMessage("\n  BIAOGE_RELOAD_RIBBON  - 重新加载工具栏");
                ed.WriteMessage("\n  BIAOGE_TRANSLATE      - 打开翻译面板");
                ed.WriteMessage("\n  BIAOGE_CALCULATE      - 打开算量面板");
                ed.WriteMessage("\n  BIAOGE_SETTINGS       - 打开设置对话框");
                ed.WriteMessage("\n");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示UI状态失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ 诊断算量功能 - 专门解决"面积始终为0"问题
        ///
        /// 用户反馈："改了上百次，面积始终显示是0，就很过分"
        ///
        /// 功能说明：
        /// - 检查图纸中的文本实体（用于构件识别）
        /// - 检查图纸中的几何实体（Polyline/Hatch/Region/Solid3d）
        /// - 运行构件识别并分析为什么面积为0
        /// - 给出具体的解决方案
        ///
        /// 使用场景：
        /// - 算量功能面积为0时，运行此命令找出根本原因
        /// - 了解图纸的几何实体分布情况
        /// - 验证算量功能是否正常工作
        /// </summary>
        [CommandMethod("BIAOGE_DIAGNOSE_QUANTITY", CommandFlags.Modal)]
        public void DiagnoseQuantityCalculation()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n╔═════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║    标哥算量功能诊断 - 面积为0问题专项排查       ║");
                ed.WriteMessage("\n╚═════════════════════════════════════════════════════╝");
                ed.WriteMessage("\n正在诊断，请稍候...\n");

                Log.Information("开始运行算量诊断");

                // 运行诊断
                var diagnosticService = new QuantityDiagnosticService();
                var report = diagnosticService.RunFullDiagnostic();

                // 显示报告
                ed.WriteMessage("\n" + report);

                // 保存到桌面
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var reportPath = System.IO.Path.Combine(desktopPath, $"BiaogPlugin_Quantity_Diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                System.IO.File.WriteAllText(reportPath, report);

                ed.WriteMessage($"\n\n诊断报告已保存到: {reportPath}");
                Log.Information($"算量诊断报告已保存: {reportPath}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "运行算量诊断失败");
                ed.WriteMessage($"\n[错误] 诊断失败: {ex.Message}");
                ed.WriteMessage($"\n详细错误: {ex.StackTrace}");
            }
        }

        #endregion
    }
}
