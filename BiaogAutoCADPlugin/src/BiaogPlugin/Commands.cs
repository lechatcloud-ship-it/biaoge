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
    /// 标哥插件的AutoCAD命令集
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

                // 提取选中文本实体的内容
                var textEntities = new List<DwgTextEntity>();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in selectedIds)
                    {
                        var obj = tr.GetObject(objId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                        DwgTextEntity? textEntity = null;

                        if (obj is Autodesk.AutoCAD.DatabaseServices.DBText dbText)
                        {
                            textEntity = new DwgTextEntity
                            {
                                ObjectId = objId,
                                Content = dbText.TextString,
                                Type = "DBText",
                                Layer = dbText.Layer,
                                Position = new System.Numerics.Vector3(
                                    (float)dbText.Position.X,
                                    (float)dbText.Position.Y,
                                    (float)dbText.Position.Z
                                )
                            };
                        }
                        else if (obj is Autodesk.AutoCAD.DatabaseServices.MText mText)
                        {
                            textEntity = new DwgTextEntity
                            {
                                ObjectId = objId,
                                Content = mText.Text,
                                Type = "MText",
                                Layer = mText.Layer,
                                Position = new System.Numerics.Vector3(
                                    (float)mText.Location.X,
                                    (float)mText.Location.Y,
                                    (float)mText.Location.Z
                                )
                            };
                        }
                        else if (obj is Autodesk.AutoCAD.DatabaseServices.AttributeReference attRef)
                        {
                            textEntity = new DwgTextEntity
                            {
                                ObjectId = objId,
                                Content = attRef.TextString,
                                Type = "AttributeReference",
                                Layer = attRef.Layer,
                                Position = new System.Numerics.Vector3(
                                    (float)attRef.Position.X,
                                    (float)attRef.Position.Y,
                                    (float)attRef.Position.Z
                                )
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

                // 更新DWG文本
                var updater = new DwgTextUpdater();
                var updateMap = new Dictionary<Autodesk.AutoCAD.DatabaseServices.ObjectId, string>();

                for (int i = 0; i < textEntities.Count; i++)
                {
                    if (i < translations.Count && !string.IsNullOrEmpty(translations[i]))
                    {
                        updateMap[textEntities[i].ObjectId] = translations[i];
                        translatedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                updater.UpdateTexts(updateMap);

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
                                    ObjectIdHandle = textEntities[i].ObjectId.Handle.ToString(),
                                    OriginalText = textEntities[i].Content,
                                    TranslatedText = translations[i],
                                    SourceLanguage = "auto",
                                    TargetLanguage = targetLanguage,
                                    EntityType = textEntities[i].Type,
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

                ed.WriteMessage("\n╔══════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥插件 - 图层翻译功能                    ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
                ed.WriteMessage("\n");

                // 1. 获取所有图层及文本统计
                ed.WriteMessage("\n正在分析图层...");
                var layers = LayerTranslationService.GetAllLayersWithTextCount();

                if (layers.Count == 0)
                {
                    ed.WriteMessage("\n图纸中没有图层。");
                    return;
                }

                // 2. 显示图层列表
                ed.WriteMessage($"\n\n图层列表（共 {layers.Count} 个图层）：");
                ed.WriteMessage("\n" + new string('─', 70));
                ed.WriteMessage("\n序号  图层名称                     文本数量  颜色        状态");
                ed.WriteMessage("\n" + new string('─', 70));

                int index = 1;
                foreach (var layer in layers.Take(20)) // 只显示前20个
                {
                    var status = "";
                    if (layer.IsLocked) status += "锁定 ";
                    if (layer.IsOff) status += "关闭 ";
                    if (layer.IsFrozen) status += "冻结 ";
                    if (string.IsNullOrEmpty(status)) status = "正常";

                    ed.WriteMessage($"\n{index,4}  {layer.LayerName,-28} {layer.TextCount,8}  {layer.ColorName,-10} {status}");
                    index++;
                }

                if (layers.Count > 20)
                {
                    ed.WriteMessage($"\n... 还有 {layers.Count - 20} 个图层（未显示）");
                }

                ed.WriteMessage("\n" + new string('─', 70));

                // 3. 提示用户输入图层名称
                ed.WriteMessage("\n\n请输入要翻译的图层名称（多个图层用逗号分隔）：");
                ed.WriteMessage("\n提示：");
                ed.WriteMessage("\n  - 输入 'all' 翻译所有图层");
                ed.WriteMessage("\n  - 输入图层名称，例如: 墙体,门窗");
                ed.WriteMessage("\n  - 输入 '*文字*' 翻译包含'文字'的所有图层");
                ed.WriteMessage("\n");

                var layerInputResult = ed.GetString("\n图层名称: ");
                if (layerInputResult.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(layerInputResult.StringResult))
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                var layerInput = layerInputResult.StringResult.Trim();

                // 4. 解析图层选择
                List<string> selectedLayers;

                if (layerInput.ToLower() == "all")
                {
                    selectedLayers = layers.Where(l => l.TextCount > 0).Select(l => l.LayerName).ToList();
                }
                else if (layerInput.StartsWith("*") && layerInput.EndsWith("*"))
                {
                    var keyword = layerInput.Trim('*');
                    selectedLayers = layers
                        .Where(l => l.LayerName.Contains(keyword) && l.TextCount > 0)
                        .Select(l => l.LayerName)
                        .ToList();
                }
                else
                {
                    selectedLayers = layerInput.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }

                if (selectedLayers.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何图层或选择的图层不存在。");
                    return;
                }

                // 统计选中图层的文本数量
                int totalTexts = layers
                    .Where(l => selectedLayers.Contains(l.LayerName))
                    .Sum(l => l.TextCount);

                ed.WriteMessage($"\n\n已选择 {selectedLayers.Count} 个图层，共 {totalTexts} 个文本实体");
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
        public async void StartAIAssistant()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                Log.Information("启动AI助手");

                ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║  标哥AI助手 - 智能Agent架构（qwen3-max-preview）      ║");
                ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n正在初始化Agent系统...");
                ed.WriteMessage("\n  ✓ 核心Agent: qwen3-max-preview（思考模式融合）");
                ed.WriteMessage("\n  ✓ 翻译工具: qwen-mt-flash（92语言，术语定制）");
                ed.WriteMessage("\n  ✓ 代码工具: qwen3-coder-flash（仓库级别理解）");
                ed.WriteMessage("\n  ✓ 视觉工具: qwen3-vl-flash（空间感知+2D/3D定位）");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n正在分析当前图纸...");

                // 初始化服务 - 使用统一的Bailian客户端
                var configManager = ServiceLocator.GetService<ConfigManager>();
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var contextManager = new DrawingContextManager();
                var aiService = new AIAssistantService(bailianClient!, configManager!, contextManager);

                ed.WriteMessage("\n图纸分析完成！Agent已就绪，可智能调用专用模型完成任务。");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n示例任务：");
                ed.WriteMessage("\n  - 帮我翻译图纸中的\"外墙\"为英文（自动调用qwen-mt-flash）");
                ed.WriteMessage("\n  - 将所有的\"C30\"修改为\"C35\"（自动调用qwen3-coder-flash）");
                ed.WriteMessage("\n  - 识别图纸中的梁构件（自动调用qwen3-vl-flash）");
                ed.WriteMessage("\n  - 这张图纸有哪些图层？（直接查询图纸上下文）");
                ed.WriteMessage("\n");
                ed.WriteMessage("\n输入 'exit' 退出，输入 'clear' 清除历史，输入 'deep' 启用深度思考");
                ed.WriteMessage("\n" + new string('─', 60));

                bool deepThinking = false;

                // 对话循环
                while (true)
                {
                    ed.WriteMessage("\n\n您: ");
                    var userInput = await Task.Run(() =>
                    {
                        var result = ed.GetString(new PromptStringOptions(""));
                        return result.Status == PromptStatus.OK ? result.StringResult : null;
                    });

                    if (string.IsNullOrWhiteSpace(userInput))
                        continue;

                    // 处理命令
                    if (userInput.ToLower() == "exit")
                    {
                        ed.WriteMessage("\n再见！感谢使用标哥AI助手。");
                        break;
                    }
                    else if (userInput.ToLower() == "clear")
                    {
                        aiService.ClearHistory();
                        ed.WriteMessage("\n对话历史已清除。");
                        continue;
                    }
                    else if (userInput.ToLower() == "deep")
                    {
                        deepThinking = !deepThinking;
                        ed.WriteMessage($"\n深度思考模式: {(deepThinking ? "已启用 🧠" : "已关闭")}");
                        continue;
                    }

                    // AI回复
                    ed.WriteMessage("\n\n标哥AI: ");

                    var response = await aiService.ChatStreamAsync(
                        userInput,
                        deepThinking,
                        chunk => ed.WriteMessage(chunk) // 流式输出到命令行
                    );

                    if (!response.Success)
                    {
                        ed.WriteMessage($"\n[错误] {response.Error}");
                    }

                    ed.WriteMessage("\n" + new string('─', 60));
                }

                Log.Information("AI助手会话结束");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AI助手启动失败");
                ed.WriteMessage($"\n[错误] {ex.Message}");
                ed.WriteMessage("\n请确保已在设置中配置百炼API密钥（BIAOGE_SETTINGS）");
            }
        }

        #endregion

        #region 快捷键管理命令

        /// <summary>
        /// 显示快捷键配置指南
        /// </summary>
        [CommandMethod("BIAOGE_KEYS", CommandFlags.Modal)]
        public void ShowKeybindings()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var guide = KeybindingsManager.GetKeybindingsGuide();
                ed.WriteMessage("\n" + guide);

                Log.Information("显示快捷键配置指南");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "显示快捷键指南失败");
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
            catch (Exception ex)
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
            catch (Exception ex)
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

            ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
            ed.WriteMessage("\n║  标哥 - 建筑工程CAD翻译工具 v1.0 - 帮助                ║");
            ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【翻译功能】");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE           - 打开翻译面板（全图翻译）");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE_SELECTED  - 框选翻译（仅翻译选中文本）");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE_ZH        - 快速翻译为中文（推荐）");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE_EN        - 快速翻译为英语");
            ed.WriteMessage("\n  BIAOGE_TRANSLATE_LAYER     - 按图层选择性翻译");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【高级功能 - Phase 3】");
            ed.WriteMessage("\n  BIAOGE_HISTORY             - 查看翻译历史记录和统计");
            ed.WriteMessage("\n  BIAOGE_UNDO_TRANSLATION    - 撤销最近的翻译操作");
            ed.WriteMessage("\n  BIAOGE_CLEAR_HISTORY       - 清除所有翻译历史");
            ed.WriteMessage("\n  BIAOGE_SMART_REPLACE       - 批量智能替换（支持AI建议）");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【算量功能】");
            ed.WriteMessage("\n  BIAOGE_CALCULATE      - 打开算量面板");
            ed.WriteMessage("\n  BIAOGE_EXPORTEXCEL    - 快速导出Excel清单");
            ed.WriteMessage("\n  BIAOGE_QUICKCOUNT     - 快速统计构件数量");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【AI助手】");
            ed.WriteMessage("\n  BIAOGE_AI             - 启动标哥AI助手（智能Agent架构）");
            ed.WriteMessage("\n                          核心: qwen3-max-preview（思考模式融合）");
            ed.WriteMessage("\n                          智能调用: 翻译/代码/视觉专用模型");
            ed.WriteMessage("\n                          支持: 深度思考、流式输出、工具调用");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【用户体验增强 - Phase 2】");
            ed.WriteMessage("\n  双击文本翻译       - 双击文本实体快速翻译");
            ed.WriteMessage("\n  智能输入法切换     - 命令模式自动切换英文/中文");
            ed.WriteMessage("\n  右键菜单翻译       - 右键文本实体快速翻译");
            ed.WriteMessage("\n  Ribbon工具栏       - 专业的工具栏界面");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【设置与工具】");
            ed.WriteMessage("\n  BIAOGE_SETTINGS       - 打开设置对话框");
            ed.WriteMessage("\n  BIAOGE_STATUS         - 显示功能状态");
            ed.WriteMessage("\n  BIAOGE_TOGGLE_DOUBLECLICK  - 切换双击翻译");
            ed.WriteMessage("\n  BIAOGE_TOGGLE_IME     - 切换智能输入法");
            ed.WriteMessage("\n  BIAOGE_ABOUT          - 关于插件");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【快捷键】");
            ed.WriteMessage("\n  BIAOGE_KEYS           - 显示快捷键配置指南");
            ed.WriteMessage("\n  BIAOGE_EXPORT_KEYS    - 导出快捷键配置到桌面");
            ed.WriteMessage("\n  BIAOGE_INSTALL_KEYS   - 自动安装快捷键");
            ed.WriteMessage("\n");
            ed.WriteMessage("\n【诊断工具】");
            ed.WriteMessage("\n  BIAOGE_HELP           - 显示此帮助信息");
            ed.WriteMessage("\n  BIAOGE_VERSION        - 显示版本信息");
            ed.WriteMessage("\n  BIAOGE_CLEARCACHE     - 清除翻译缓存");
            ed.WriteMessage("\n  BIAOGE_DIAGNOSTIC     - 运行系统诊断");
            ed.WriteMessage("\n  BIAOGE_PERFORMANCE    - 显示性能报告");
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
                        var aiResponse = await bailianClient.ChatAsync(prompt, "qwen3-max-preview");
                        ed.WriteMessage($"\n\nAI建议:");
                        ed.WriteMessage($"\n{aiResponse}");
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
                                ? ExtractReplacementFromAI(aiResponse, findText)
                                : confirmResult.StringResult.Trim();
                        }
                        else
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
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
                            var obj = tr.GetObject(entity.ObjectId, OpenMode.ForWrite);
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
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"替换失败: {entity.ObjectId}");
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
            catch (Exception ex)
            {
                Log.Warning(ex, "提取AI建议失败");
            }

            return originalText; // 默认返回原文
        }

        #endregion
    }
}
