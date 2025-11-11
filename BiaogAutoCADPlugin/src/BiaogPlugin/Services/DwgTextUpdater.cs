using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// DWG文本更新器 - 安全地批量更新文本内容
    /// 支持事务管理和错误恢复
    /// </summary>
    public class DwgTextUpdater
    {
        /// <summary>
        /// 批量更新文本内容
        /// </summary>
        /// <param name="updates">更新请求列表</param>
        /// <returns>更新结果统计</returns>
        public TextUpdateResult UpdateTexts(List<TextUpdateRequest> updates)
        {
            if (updates == null || updates.Count == 0)
            {
                Log.Warning("没有需要更新的文本");
                return new TextUpdateResult { TotalCount = 0 };
            }

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new InvalidOperationException("没有活动的文档");
            }

            var db = doc.Database;
            var ed = doc.Editor;

            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;
            var errors = new List<string>();

            // 锁定文档以防止用户交互干扰
            using (var docLock = doc.LockDocument())
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        foreach (var update in updates)
                        {
                            try
                            {
                                // 跳过内容相同的文本
                                if (update.OriginalContent == update.NewContent)
                                {
                                    skippedCount++;
                                    continue;
                                }

                                // 更新文本
                                if (UpdateSingleText(tr, update))
                                {
                                    successCount++;
                                }
                                else
                                {
                                    failCount++;
                                    errors.Add($"ObjectId: {update.ObjectId}, 原文: {update.OriginalContent}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, $"更新文本失败: {update.ObjectId}");
                                failCount++;
                                errors.Add($"ObjectId: {update.ObjectId}, 错误: {ex.Message}");
                            }
                        }

                        // 提交所有更改
                        tr.Commit();

                        var result = new TextUpdateResult
                        {
                            TotalCount = updates.Count,
                            SuccessCount = successCount,
                            FailCount = failCount,
                            SkippedCount = skippedCount,
                            Errors = errors
                        };

                        Log.Information($"文本更新完成: {result}");
                        ed.WriteMessage($"\n更新完成: 成功 {successCount}, 失败 {failCount}, 跳过 {skippedCount}");

                        // 刷新图形显示
                        if (successCount > 0)
                        {
                            ed.Regen();
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "批量更新文本时发生错误");
                        tr.Abort();
                        throw new Exception($"批量更新失败: {ex.Message}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 更新单个文本实体
        /// </summary>
        private bool UpdateSingleText(Transaction tr, TextUpdateRequest update)
        {
            try
            {
                var ent = tr.GetObject(update.ObjectId, OpenMode.ForWrite) as Entity;
                if (ent == null) return false;

                // 根据不同类型更新
                if (ent is DBText dbText)
                {
                    dbText.TextString = update.NewContent;
                    return true;
                }

                if (ent is MText mText)
                {
                    mText.Contents = update.NewContent;
                    return true;
                }

                if (ent is AttributeReference attRef)
                {
                    attRef.TextString = update.NewContent;
                    return true;
                }

                if (ent is AttributeDefinition attDef)
                {
                    attDef.TextString = update.NewContent;
                    return true;
                }

                Log.Warning($"不支持的实体类型: {ent.GetType().Name}");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"更新单个文本失败: {update.ObjectId}");
                return false;
            }
        }

        /// <summary>
        /// 更新单个文本（独立事务）
        /// </summary>
        public bool UpdateText(ObjectId objectId, string newContent)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var db = doc.Database;

            using (var docLock = doc.LockDocument())
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var ent = tr.GetObject(objectId, OpenMode.ForWrite) as Entity;
                        if (ent == null) return false;

                        bool updated = false;

                        if (ent is DBText dbText)
                        {
                            dbText.TextString = newContent;
                            updated = true;
                        }
                        else if (ent is MText mText)
                        {
                            mText.Contents = newContent;
                            updated = true;
                        }
                        else if (ent is AttributeReference attRef)
                        {
                            attRef.TextString = newContent;
                            updated = true;
                        }

                        if (updated)
                        {
                            tr.Commit();
                            doc.Editor.Regen();
                            return true;
                        }

                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "更新文本失败");
                        tr.Abort();
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// 从翻译结果构建更新请求
        /// </summary>
        public List<TextUpdateRequest> BuildUpdateRequests(
            List<TextEntity> texts,
            Dictionary<string, string> translationMap)
        {
            var requests = new List<TextUpdateRequest>();

            foreach (var text in texts)
            {
                if (translationMap.ContainsKey(text.Content))
                {
                    requests.Add(new TextUpdateRequest
                    {
                        ObjectId = text.Id,
                        OriginalContent = text.Content,
                        NewContent = translationMap[text.Content],
                        Layer = text.Layer,
                        EntityType = text.Type
                    });
                }
            }

            return requests;
        }

        /// <summary>
        /// 验证更新结果
        /// </summary>
        public bool VerifyUpdates(List<TextUpdateRequest> updates)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var db = doc.Database;
            int verifiedCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var update in updates)
                    {
                        var ent = tr.GetObject(update.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        string currentContent = null;

                        if (ent is DBText dbText)
                            currentContent = dbText.TextString;
                        else if (ent is MText mText)
                            currentContent = mText.Contents;
                        else if (ent is AttributeReference attRef)
                            currentContent = attRef.TextString;

                        if (currentContent == update.NewContent)
                        {
                            verifiedCount++;
                        }
                    }

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "验证更新结果失败");
                    tr.Abort();
                    return false;
                }
            }

            bool allVerified = verifiedCount == updates.Count;
            Log.Information($"更新验证: {verifiedCount}/{updates.Count} 正确");
            return allVerified;
        }
    }

    /// <summary>
    /// 文本更新结果
    /// </summary>
    public class TextUpdateResult
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public bool IsSuccess => FailCount == 0;
        public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;

        public override string ToString()
        {
            return $"总计: {TotalCount}, 成功: {SuccessCount}, 失败: {FailCount}, 跳过: {SkippedCount}, 成功率: {SuccessRate:F1}%";
        }
    }
}
