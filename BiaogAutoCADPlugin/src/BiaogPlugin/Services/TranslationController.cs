using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        // 注意：以下服务需要从BiaogeCSharp项目复制实现
        // private readonly TranslationEngine _translationEngine;
        // private readonly CacheService _cacheService;

        public TranslationController()
        {
            _extractor = new DwgTextExtractor();
            _updater = new DwgTextUpdater();

            // TODO: 从ServiceLocator获取服务
            // _translationEngine = ServiceLocator.GetOrCreateService<TranslationEngine>();
            // _cacheService = ServiceLocator.GetOrCreateService<CacheService>();
        }

        /// <summary>
        /// 翻译当前DWG图纸
        /// </summary>
        /// <param name="targetLanguage">目标语言代码（如"en"、"ja"）</param>
        /// <param name="progress">进度回调</param>
        /// <returns>翻译统计信息</returns>
        public async Task<TranslationStatistics> TranslateCurrentDrawing(
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null)
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

                // TODO: 实现缓存查询
                // foreach (var text in uniqueTexts)
                // {
                //     var cached = await _cacheService.GetTranslationAsync(text, targetLanguage);
                //     if (cached != null)
                //     {
                //         translationMap[text] = cached.TranslatedText;
                //         statistics.CacheHitCount++;
                //     }
                //     else
                //     {
                //         uncachedTexts.Add(text);
                //     }
                // }

                // 临时实现：所有文本都需要翻译
                uncachedTexts = uniqueTexts;

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

                    // TODO: 实现真实的翻译调用
                    // var translations = await _translationEngine.TranslateBatchAsync(
                    //     uncachedTexts,
                    //     targetLanguage,
                    //     progress: new Progress<double>(p =>
                    //     {
                    //         progress?.Report(new TranslationProgress
                    //         {
                    //             Stage = "翻译中",
                    //             Percentage = 50 + (int)(p * 0.3)
                    //         });
                    //     })
                    // );

                    // 临时实现：模拟翻译（在真实环境中会调用百炼API）
                    var translations = SimulateTranslation(uncachedTexts, targetLanguage);

                    // 添加到翻译映射
                    for (int i = 0; i < uncachedTexts.Count; i++)
                    {
                        translationMap[uncachedTexts[i]] = translations[i];

                        // TODO: 写入缓存
                        // await _cacheService.SetTranslationAsync(
                        //     uncachedTexts[i],
                        //     targetLanguage,
                        //     translations[i]
                        // );
                    }

                    statistics.ApiCallCount = (int)Math.Ceiling(uncachedTexts.Count / 50.0);
                    statistics.SuccessCount = translationMap.Count;
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

                var updateResult = _updater.UpdateTexts(updateRequests);
                statistics.SuccessCount = updateResult.SuccessCount;
                statistics.FailureCount = updateResult.FailCount;

                Log.Information($"更新结果: {updateResult}");

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
            catch (Exception ex)
            {
                Log.Error(ex, "翻译过程中发生错误");
                ed.WriteMessage($"\n[错误] 翻译失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 模拟翻译（用于测试，真实环境会调用百炼API）
        /// </summary>
        private List<string> SimulateTranslation(List<string> texts, string targetLanguage)
        {
            Log.Warning("使用模拟翻译（真实环境需要配置百炼API密钥）");

            return texts.Select(text =>
            {
                switch (targetLanguage.ToLower())
                {
                    case "en":
                        return $"[EN] {text}";
                    case "ja":
                        return $"[JA] {text}";
                    case "ko":
                        return $"[KO] {text}";
                    default:
                        return $"[{targetLanguage.ToUpper()}] {text}";
                }
            }).ToList();
        }

        /// <summary>
        /// 翻译选定的文本
        /// </summary>
        public async Task<TranslationStatistics> TranslateSelectedTexts(
            List<TextEntity> selectedTexts,
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null)
        {
            // 与TranslateCurrentDrawing类似，但只处理选定的文本
            var statistics = new TranslationStatistics
            {
                TotalTextCount = selectedTexts.Count
            };

            // TODO: 实现选定文本翻译逻辑

            return statistics;
        }

        /// <summary>
        /// 翻译指定图层的文本
        /// </summary>
        public async Task<TranslationStatistics> TranslateLayer(
            string layerName,
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null)
        {
            var layerTexts = _extractor.ExtractTextByLayer(layerName);

            var statistics = new TranslationStatistics
            {
                TotalTextCount = layerTexts.Count
            };

            // TODO: 实现图层翻译逻辑

            return statistics;
        }

        /// <summary>
        /// 获取翻译预览（不实际更新图纸）
        /// </summary>
        public async Task<Dictionary<string, string>> GetTranslationPreview(
            List<string> texts,
            string targetLanguage)
        {
            var translationMap = new Dictionary<string, string>();

            // TODO: 实现预览逻辑

            return translationMap;
        }
    }
}
