using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 图层翻译服务
    /// 提供按图层选择性翻译文本的功能
    /// </summary>
    public class LayerTranslationService
    {
        /// <summary>
        /// 图层信息
        /// </summary>
        public class LayerInfo
        {
            public string LayerName { get; set; } = "";
            public int TextCount { get; set; }
            public string ColorName { get; set; } = "";
            public bool IsLocked { get; set; }
            public bool IsOff { get; set; }
            public bool IsFrozen { get; set; }
        }

        /// <summary>
        /// 获取所有图层及其文本统计信息
        /// </summary>
        public static List<LayerInfo> GetAllLayersWithTextCount()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var layers = new List<LayerInfo>();

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 获取图层表
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    // 统计每个图层的文本数量
                    var layerTextCounts = new Dictionary<string, int>();

                    // 遍历所有BlockTableRecord
                    var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in blockTable)
                    {
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (btrId.IsNull || btrId.IsErased || btrId.IsEffectivelyErased || !btrId.IsValid)
                            continue;

                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                        foreach (ObjectId objId in btr)
                        {
                            // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                            if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                                continue;

                            var obj = tr.GetObject(objId, OpenMode.ForRead);
                            string? layerName = null;

                            if (obj is DBText dbText)
                            {
                                layerName = dbText.Layer;
                            }
                            else if (obj is MText mText)
                            {
                                layerName = mText.Layer;
                            }
                            else if (obj is AttributeReference attRef)
                            {
                                layerName = attRef.Layer;
                            }

                            if (!string.IsNullOrEmpty(layerName))
                            {
                                if (!layerTextCounts.ContainsKey(layerName))
                                {
                                    layerTextCounts[layerName] = 0;
                                }
                                layerTextCounts[layerName]++;
                            }
                        }
                    }

                    // 构建图层信息列表
                    foreach (ObjectId layerId in layerTable)
                    {
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (layerId.IsNull || layerId.IsErased || layerId.IsEffectivelyErased || !layerId.IsValid)
                            continue;

                        var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);

                        var layerInfo = new LayerInfo
                        {
                            LayerName = layer.Name,
                            TextCount = layerTextCounts.ContainsKey(layer.Name) ? layerTextCounts[layer.Name] : 0,
                            ColorName = layer.Color.ColorNameForDisplay,
                            IsLocked = layer.IsLocked,
                            IsOff = layer.IsOff,
                            IsFrozen = layer.IsFrozen
                        };

                        layers.Add(layerInfo);
                    }

                    tr.Commit();
                }

                // 按文本数量降序排序
                layers = layers.OrderByDescending(l => l.TextCount).ToList();

                Log.Information($"获取图层信息: 共 {layers.Count} 个图层");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "获取图层信息失败");
                throw;
            }

            return layers;
        }

        /// <summary>
        /// ✅ P1修复: 从指定图层提取文本实体,使用统一的TextEntity模型
        /// </summary>
        public static List<TextEntity> ExtractTextFromLayers(List<string> layerNames)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var textEntities = new List<TextEntity>();

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 遍历所有BlockTableRecord
                    var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    foreach (ObjectId btrId in blockTable)
                    {
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (btrId.IsNull || btrId.IsErased || btrId.IsEffectivelyErased || !btrId.IsValid)
                            continue;

                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                        foreach (ObjectId objId in btr)
                        {
                            // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                            if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                                continue;

                            var obj = tr.GetObject(objId, OpenMode.ForRead);
                            TextEntity? textEntity = null;

                            if (obj is DBText dbText && layerNames.Contains(dbText.Layer))
                            {
                                textEntity = new TextEntity
                                {
                                    Id = objId,
                                    Type = TextEntityType.DBText,
                                    Content = dbText.TextString,
                                    Position = dbText.Position,
                                    Layer = dbText.Layer,
                                    Height = dbText.Height,
                                    Rotation = dbText.Rotation,
                                    ColorIndex = (short)dbText.ColorIndex
                                };
                            }
                            else if (obj is MText mText && layerNames.Contains(mText.Layer))
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
                            else if (obj is AttributeReference attRef && layerNames.Contains(attRef.Layer))
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
                    }

                    tr.Commit();
                }

                Log.Information($"从图层提取文本: {layerNames.Count} 个图层，{textEntities.Count} 个文本");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "从图层提取文本失败");
                throw;
            }

            return textEntities;
        }

        /// <summary>
        /// 翻译指定图层的所有文本
        /// </summary>
        public static async Task<TranslationStatistics> TranslateLayerTexts(
            List<string> layerNames,
            string targetLanguage,
            IProgress<TranslationProgress>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. 提取文本
                var textEntities = ExtractTextFromLayers(layerNames);

                if (textEntities.Count == 0)
                {
                    Log.Warning($"选中的图层没有文本实体");
                    return new TranslationStatistics
                    {
                        TotalTextCount = 0,
                        UniqueTextCount = 0,
                        SuccessCount = 0,
                        FailureCount = 0
                    };
                }

                // 2. 执行翻译
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var configManager = ServiceLocator.GetService<ConfigManager>();
                var cacheService = ServiceLocator.GetService<CacheService>();

                var engine = new TranslationEngine(bailianClient!, cacheService!);

                // 转换进度回调
                var apiProgress = new Progress<double>(p =>
                {
                    progress?.Report(new TranslationProgress
                    {
                        Stage = "翻译中",
                        Percentage = (int)p,
                        ProcessedCount = (int)(textEntities.Count * p / 100.0),
                        TotalCount = textEntities.Count
                    });
                });

                var translations = await engine.TranslateBatchWithCacheAsync(
                    textEntities.Select(t => t.Content).ToList(),
                    targetLanguage,
                    apiProgress,
                    cancellationToken
                );

                // 3. 更新DWG - 构建更新请求
                var updater = new DwgTextUpdater();
                var updateRequests = new List<TextUpdateRequest>();

                for (int i = 0; i < textEntities.Count && i < translations.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.IsNullOrEmpty(translations[i]))
                    {
                        updateRequests.Add(new TextUpdateRequest
                        {
                            ObjectId = textEntities[i].Id,
                            OriginalContent = textEntities[i].Content,
                            NewContent = translations[i]
                        });
                    }
                }

                var updateResult = updater.UpdateTexts(updateRequests);

                // 记录翻译历史
                if (configManager != null && configManager.Config.Translation.EnableHistory)
                {
                    var history = ServiceLocator.GetService<TranslationHistory>();
                    if (history != null)
                    {
                        var historyRecords = new List<TranslationHistory.HistoryRecord>();
                        for (int i = 0; i < textEntities.Count && i < translations.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!string.IsNullOrEmpty(translations[i]))
                            {
                                historyRecords.Add(new TranslationHistory.HistoryRecord
                                {
                                    Timestamp = DateTime.Now,
                                    ObjectIdHandle = textEntities[i].Id.Handle.ToString(),
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
                            Log.Debug($"已记录 {historyRecords.Count} 条图层翻译历史");
                        }
                    }
                }

                // 4. 生成统计信息
                var stats = new TranslationStatistics
                {
                    TotalTextCount = textEntities.Count,
                    UniqueTextCount = textEntities.Select(t => t.Content).Distinct().Count(),
                    SuccessCount = updateResult.SuccessCount,
                    FailureCount = updateResult.FailCount,
                    ApiCallCount = (int)Math.Ceiling(textEntities.Count / 50.0)
                };

                Log.Information($"图层翻译完成: {stats}");

                return stats;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "图层翻译失败");
                throw;
            }
        }
    }
}
