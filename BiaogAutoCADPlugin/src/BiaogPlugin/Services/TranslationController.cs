using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 翻译流程控制器
    /// 协调文本提取、翻译、缓存和更新的完整流程
    /// </summary>
    public class TranslationController
    {
        private readonly DwgTextExtractor _extractor;
        private readonly DwgTextUpdater _updater;
        private readonly TranslationEngine _translationEngine;
        private readonly CacheService _cacheService;
        private readonly ConfigManager _configManager;

        public TranslationController()
        {
            _extractor = new DwgTextExtractor();
            _updater = new DwgTextUpdater();

            // ✅ P1修复：从ServiceLocator获取服务,添加null检查避免NullReferenceException
            _translationEngine = ServiceLocator.GetService<TranslationEngine>()
                ?? throw new InvalidOperationException("TranslationEngine未在ServiceLocator中注册");
            _cacheService = ServiceLocator.GetService<CacheService>()
                ?? throw new InvalidOperationException("CacheService未在ServiceLocator中注册");
            _configManager = ServiceLocator.GetService<ConfigManager>()
                ?? throw new InvalidOperationException("ConfigManager未在ServiceLocator中注册");
        }

        /// <summary>
        /// 翻译当前DWG图纸
        /// </summary>
        /// <param name="targetLanguage">目标语言代码（如"en"、"ja"）</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>翻译统计信息</returns>
        public async Task<TranslationStatistics> TranslateCurrentDrawing(
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new InvalidOperationException("没有活动的文档");
            }

            var ed = doc.Editor;
            var stopwatch = Stopwatch.StartNew();
            var statistics = new TranslationStatistics();

            try
            {
                Log.Information($"开始翻译图纸: {doc.Name}, 目标语言: {targetLanguage}");

                // ====== 阶段0: 解锁图纸（新增！） ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "解锁图纸",
                    Percentage = 0
                });

                DwgUnlockService.UnlockRecord? unlockRecord = null;
                try
                {
                    // 检查锁定状态
                    var (lockedLayers, xrefCount) = DwgUnlockService.CheckLockStatus();
                    Log.Information($"图纸状态: {lockedLayers}个锁定图层, {xrefCount}个外部引用");

                    if (lockedLayers > 0 || xrefCount > 0)
                    {
                        ed.WriteMessage($"\n检测到 {lockedLayers} 个锁定图层, {xrefCount} 个外部引用");
                        ed.WriteMessage("\n正在解锁图纸...");

                        // ✅ 用户需求："如果锁定就解锁整个图纸后再翻译"
                        // bindXRefs参数：是否自动绑定外部引用（默认true，确保外部文本可翻译）
                        unlockRecord = DwgUnlockService.UnlockDrawingForTranslation(bindXRefs: xrefCount > 0);

                        if (unlockRecord == null)
                        {
                            Log.Warning("解锁服务返回null，跳过解锁报告");
                            ed.WriteMessage("\n⚠️ 解锁服务未返回结果");
                        }
                        else
                        {
                            if (xrefCount > 0 && unlockRecord.BoundXRefs.Count > 0)
                            {
                                ed.WriteMessage($"\n✅ 已绑定 {unlockRecord.BoundXRefs.Count} 个外部引用为本地块");
                            }
                            ed.WriteMessage($"\n✅ 图纸解锁完成: {unlockRecord.UnlockedLayers.Count}个图层已解锁");
                        }
                    }
                    else
                    {
                        Log.Information("图纸无锁定内容，跳过解锁");
                    }
                }
                catch (Exception unlockEx)
                {
                    Log.Warning(unlockEx, "解锁图纸失败，继续翻译（可能部分文本无法更新）");
                    ed.WriteMessage($"\n⚠️ 解锁失败: {unlockEx.Message}");
                }

                // ====== 阶段1: 提取文本 ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "提取文本",
                    Percentage = 5
                });

                var allTexts = _extractor.ExtractAllText();
                statistics.TotalTextCount = allTexts.Count;

                if (allTexts.Count == 0)
                {
                    ed.WriteMessage("\n警告: 未找到任何文本对象");
                    Log.Warning("图纸中没有文本对象");
                    return statistics;
                }

                Log.Information($"提取到 {allTexts.Count} 个文本对象");

                // ====== 阶段2: 过滤可翻译文本 ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "分析文本",
                    Percentage = 15
                });

                var translatableTexts = _extractor.FilterTranslatableText(allTexts);
                Log.Information($"可翻译文本: {translatableTexts.Count}");

                if (translatableTexts.Count == 0)
                {
                    ed.WriteMessage("\n警告: 没有需要翻译的文本");
                    return statistics;
                }

                // ====== 阶段3: 去重 ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "文本去重",
                    Percentage = 20
                });

                var uniqueTexts = _extractor.GetUniqueContents(translatableTexts);
                statistics.UniqueTextCount = uniqueTexts.Count;
                Log.Information($"唯一文本: {uniqueTexts.Count}");

                // ====== 阶段4: 查询缓存 ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "查询缓存",
                    Percentage = 30
                });

                var translationMap = new Dictionary<string, string>();
                var uncachedTexts = new List<string>();

                // 查询缓存
                bool useCacheEnabled = _configManager.GetBool("Translation:UseCache", true);

                if (useCacheEnabled)
                {
                    foreach (var text in uniqueTexts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var cached = await _cacheService.GetTranslationAsync(text, targetLanguage);
                        if (cached != null)
                        {
                            translationMap[text] = cached;
                            statistics.CacheHitCount++;
                        }
                        else
                        {
                            uncachedTexts.Add(text);
                        }
                    }
                }
                else
                {
                    uncachedTexts = uniqueTexts;
                }

                var cacheHitRate = uniqueTexts.Count > 0
                    ? (double)statistics.CacheHitCount / uniqueTexts.Count * 100
                    : 0;

                Log.Information($"缓存命中率: {cacheHitRate:F1}%");
                ed.WriteMessage($"\n缓存命中率: {cacheHitRate:F1}%");

                // ====== 阶段5: 批量翻译 ======
                if (uncachedTexts.Any())
                {
                    progress?.Report(new TranslationProgress
                    {
                        Stage = "调用AI翻译",
                        Percentage = 50
                    });

                    // 使用TranslationEngine进行批量翻译（自动处理缓存）
                    var apiProgress = new Progress<double>(p =>
                    {
                        progress?.Report(new TranslationProgress
                        {
                            Stage = "翻译中",
                            Percentage = 50 + (int)(p * 0.3),
                            ProcessedCount = (int)(uncachedTexts.Count * p / 100.0),
                            TotalCount = uncachedTexts.Count
                        });
                    });

                    var translations = await _translationEngine.TranslateBatchWithCacheAsync(
                        uncachedTexts,
                        targetLanguage,
                        apiProgress,
                        cancellationToken
                    );

                    // 防御性验证：确保返回结果数量正确
                    if (translations.Count != uncachedTexts.Count)
                    {
                        var errorMsg = $"翻译结果数量不匹配: 期望{uncachedTexts.Count}, 实际{translations.Count}";
                        Log.Error(errorMsg);
                        throw new InvalidOperationException(errorMsg);
                    }

                    // 添加到翻译映射
                    for (int i = 0; i < uncachedTexts.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        translationMap[uncachedTexts[i]] = translations[i];
                    }

                    statistics.ApiCallCount = (int)Math.Ceiling(uncachedTexts.Count / 50.0);
                    statistics.SuccessCount = translationMap.Count;

                    Log.Information($"批量翻译完成: {translations.Count} 条");
                }

                // ====== 阶段6: 构建更新请求 ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "准备更新",
                    Percentage = 80
                });

                var updateRequests = _updater.BuildUpdateRequests(translatableTexts, translationMap);
                Log.Information($"准备更新 {updateRequests.Count} 个文本");

                // ====== 阶段7: 更新DWG ======
                progress?.Report(new TranslationProgress
                {
                    Stage = "更新图纸",
                    Percentage = 90
                });

                Log.Information($"准备更新{updateRequests.Count}个文本实体到DWG");
                ed.WriteMessage($"\n准备更新{updateRequests.Count}个文本实体...");

                var updateResult = _updater.UpdateTexts(updateRequests);
                statistics.SuccessCount = updateResult.SuccessCount;
                statistics.FailureCount = updateResult.FailCount;

                Log.Information($"更新结果: {updateResult}");
                ed.WriteMessage($"\n✓ 成功更新: {updateResult.SuccessCount}");
                ed.WriteMessage($"\n✗ 失败: {updateResult.FailCount}");
                ed.WriteMessage($"\n○ 跳过: {updateResult.SkippedCount}");

                // 记录翻译历史
                if (_configManager.Config.Translation.EnableHistory)
                {
                    var history = ServiceLocator.GetService<TranslationHistory>();
                    if (history != null && updateResult.SuccessCount > 0)
                    {
                        var historyRecords = new List<TranslationHistory.HistoryRecord>();

                        foreach (var textEntity in translatableTexts)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (translationMap.TryGetValue(textEntity.Content, out var translatedText)
                                && !string.IsNullOrEmpty(translatedText))
                            {
                                historyRecords.Add(new TranslationHistory.HistoryRecord
                                {
                                    Timestamp = DateTime.Now,
                                    ObjectIdHandle = textEntity.Id.Handle.ToString(),
                                    OriginalText = textEntity.Content,
                                    TranslatedText = translatedText,
                                    SourceLanguage = "auto",
                                    TargetLanguage = targetLanguage,
                                    EntityType = textEntity.Type.ToString(),
                                    Layer = textEntity.Layer,
                                    Operation = "translate"
                                });
                            }
                        }

                        if (historyRecords.Count > 0)
                        {
                            await history.AddRecordsAsync(historyRecords);
                            Log.Debug($"已记录 {historyRecords.Count} 条全图翻译历史");
                        }
                    }
                }

                // ====== 完成 ======
                stopwatch.Stop();
                statistics.TotalSeconds = stopwatch.Elapsed.TotalSeconds;

                progress?.Report(new TranslationProgress
                {
                    Stage = "完成",
                    Percentage = 100
                });

                Log.Information($"翻译完成: {statistics}");
                ed.WriteMessage($"\n翻译完成！");
                ed.WriteMessage($"\n{statistics}");

                return statistics;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "翻译过程中发生错误");
                ed.WriteMessage($"\n[错误] 翻译失败: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// 翻译选定的文本
        /// </summary>
        public async Task<TranslationStatistics> TranslateSelectedTexts(
            List<TextEntity> selectedTexts,
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null)
        {
            throw new NotImplementedException(
                "选定文本翻译功能尚未实现。请使用全图翻译命令（BIAOGE_TRANSLATE_ZH 或 BIAOGE_TRANSLATE_EN）");
        }

        /// <summary>
        /// 翻译指定图层的文本
        /// </summary>
        public async Task<TranslationStatistics> TranslateLayer(
            string layerName,
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null)
        {
            throw new NotImplementedException(
                "图层翻译功能尚未实现。请使用全图翻译命令（BIAOGE_TRANSLATE_ZH 或 BIAOGE_TRANSLATE_EN）");
        }

        /// <summary>
        /// 获取翻译预览（不实际更新图纸）
        /// </summary>
        public async Task<Dictionary<string, string>> GetTranslationPreview(
            List<string> texts,
            string targetLanguage)
        {
            throw new NotImplementedException(
                "翻译预览功能尚未实现。请使用实际翻译命令进行测试");
        }
    }
}
