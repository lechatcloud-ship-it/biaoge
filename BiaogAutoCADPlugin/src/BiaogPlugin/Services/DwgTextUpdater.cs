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
                            catch (System.Exception ex)
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
                    catch (System.Exception ex)
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
                // ✅ AutoCAD 2022最佳实践: 完整的ObjectId有效性检查
                if (update.ObjectId.IsNull || update.ObjectId.IsErased ||
                    update.ObjectId.IsEffectivelyErased || !update.ObjectId.IsValid)
                {
                    Log.Warning($"ObjectId {update.ObjectId.Handle} 无效或已删除，跳过更新");
                    return false;
                }

                var ent = tr.GetObject(update.ObjectId, OpenMode.ForWrite) as Entity;
                if (ent == null) return false;

                // ✅ P0修复：移除XRef预检查，直接尝试更新
                // 用户反馈："如果是只读但是为什么我双击又能编辑呢"
                // 真相：用户通过REFEDIT可以编辑XRef，API也应该尝试更新
                // 策略：直接尝试更新，如果失败会被外层try-catch捕获
                // 如果AutoCAD不允许写入，OpenMode.ForWrite本身会抛出异常

                // ✅ 关键修复：检测中文并自动切换字体
                bool containsChinese = ContainsChinese(update.NewContent);

                // 根据不同类型更新
                if (ent is DBText dbText)
                {
                    dbText.TextString = update.NewContent;

                    // ✅ 如果包含中文，切换到支持中文的字体
                    if (containsChinese)
                    {
                        EnsureChineseFontSupport(dbText, tr);
                    }
                    return true;
                }

                if (ent is MText mText)
                {
                    // ✅ P0修复：使用Contents属性设置文本内容
                    // 🐛 注意：MText.Text是只读属性，必须使用Contents
                    // 📖 官方文档：AutoCAD 2022 .NET API - MText.Text = 只读纯文本，MText.Contents = 可写含格式代码
                    // 参考：https://help.autodesk.com/view/OARX/2022/ENU MText Properties
                    mText.Contents = update.NewContent;  // 使用Contents属性赋值

                    // ✅ 如果包含中文，切换到支持中文的字体
                    if (containsChinese)
                    {
                        EnsureChineseFontSupport(mText, tr);
                    }

                    // 重置为默认文本样式
                    mText.TextStyleId = mText.Database.Textstyle;
                    return true;
                }

                if (ent is AttributeReference attRef)
                {
                    attRef.TextString = update.NewContent;

                    // ✅ 如果包含中文，切换到支持中文的字体
                    if (containsChinese)
                    {
                        EnsureChineseFontSupport(attRef, tr);
                    }
                    return true;
                }

                if (ent is AttributeDefinition attDef)
                {
                    attDef.TextString = update.NewContent;

                    // ✅ 如果包含中文，切换到支持中文的字体
                    if (containsChinese)
                    {
                        EnsureChineseFontSupport(attDef, tr);
                    }
                    return true;
                }

                // ✅ P0关键修复：添加Dimension（标注）更新支持
                // 🐛 根本问题：DwgTextExtractor提取了标注文本，但UpdateSingleText不支持更新
                // 📖 参考：AutoCAD 2025 .NET API - Dimension.DimensionText属性
                if (ent is Dimension dimension)
                {
                    try
                    {
                        // 使用DimensionText属性更新标注文本（覆盖测量值）
                        dimension.DimensionText = update.NewContent;
                        Log.Debug($"已更新Dimension文本: {update.NewContent}");
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"更新Dimension失败: {update.ObjectId}");
                        return false;
                    }
                }

                // ✅ P0关键修复：添加MLeader（多重引线）更新支持
                // 📖 参考：AutoCAD 2025 .NET API - MLeader.MText属性
                if (ent is MLeader mLeader)
                {
                    try
                    {
                        // MLeader的文本通过MText属性访问
                        if (mLeader.MText != null)
                        {
                            mLeader.MText.Contents = update.NewContent;
                            Log.Debug($"已更新MLeader文本: {update.NewContent}");
                            return true;
                        }
                        else
                        {
                            Log.Warning($"MLeader {update.ObjectId} 没有MText内容");
                            return false;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"更新MLeader失败: {update.ObjectId}");
                        return false;
                    }
                }

                // ✅ P0关键修复：添加Table（表格）更新支持
                // 📖 参考：AutoCAD 2025 .NET API - Table.Cells[row,col].TextString
                // ⚠️ 注意：update.Tag包含单元格位置信息（格式：Row{row}_Col{col}）
                if (ent is Table table)
                {
                    try
                    {
                        // 从Tag中解析行列位置（Tag格式：Row0_Col1）
                        if (update.EntityType == TextEntityType.Table && !string.IsNullOrEmpty(update.Tag))
                        {
                            var parts = update.Tag.Split('_');
                            if (parts.Length == 2 &&
                                parts[0].StartsWith("Row") &&
                                parts[1].StartsWith("Col"))
                            {
                                int row = int.Parse(parts[0].Substring(3));
                                int col = int.Parse(parts[1].Substring(3));

                                if (row < table.Rows.Count && col < table.Columns.Count)
                                {
                                    table.Cells[row, col].TextString = update.NewContent;
                                    Log.Debug($"已更新Table单元格[{row},{col}]: {update.NewContent}");
                                    return true;
                                }
                                else
                                {
                                    Log.Warning($"Table单元格索引越界: [{row},{col}]，表格大小: [{table.Rows.Count},{table.Columns.Count}]");
                                    return false;
                                }
                            }
                        }

                        Log.Warning($"Table更新失败: Tag格式错误或为空 (Tag={update.Tag})");
                        return false;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"更新Table失败: {update.ObjectId}");
                        return false;
                    }
                }

                // ✅ P0关键修复：添加FeatureControlFrame（几何公差）更新支持
                // 📖 参考：AutoCAD 2025 .NET API - FeatureControlFrame.Text属性
                if (ent is FeatureControlFrame fcf)
                {
                    try
                    {
                        fcf.Text = update.NewContent;
                        Log.Debug($"已更新FeatureControlFrame文本: {update.NewContent}");
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"更新FeatureControlFrame失败: {update.ObjectId}");
                        return false;
                    }
                }

                // ✅ P1关键修复：添加Leader（旧式引线）更新支持
                // 📖 参考：AutoCAD 2025 .NET API - Leader.Annotation属性
                // Leader的文本通过Annotation属性关联到其他实体（MText/DBText）
                if (ent is Leader leader)
                {
                    try
                    {
                        // 检查是否有关联的注释实体
                        if (!leader.Annotation.IsNull && leader.Annotation.IsValid)
                        {
                            // 获取关联的注释实体
                            var annotationEnt = tr.GetObject(leader.Annotation, OpenMode.ForWrite);

                            // 更新关联的文本实体
                            if (annotationEnt is MText annoMText)
                            {
                                annoMText.Contents = update.NewContent;
                                Log.Debug($"已更新Leader关联的MText: {update.NewContent}");
                                return true;
                            }
                            else if (annotationEnt is DBText annoDBText)
                            {
                                annoDBText.TextString = update.NewContent;
                                Log.Debug($"已更新Leader关联的DBText: {update.NewContent}");
                                return true;
                            }
                            else
                            {
                                Log.Warning($"Leader {update.ObjectId} 关联的注释类型不支持: {annotationEnt.GetType().Name}");
                                return false;
                            }
                        }
                        else
                        {
                            Log.Warning($"Leader {update.ObjectId} 没有关联的注释实体");
                            return false;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"更新Leader失败: {update.ObjectId}");
                        return false;
                    }
                }

                // ⚠️ 不支持的类型
                Log.Warning($"不支持的实体类型: {ent.GetType().Name} (ObjectId: {update.ObjectId})");
                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, $"更新单个文本失败: {update.ObjectId}");
                return false;
            }
        }

        /// <summary>
        /// ✅ 检测文本是否包含中文字符
        /// </summary>
        private bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char c in text)
            {
                // Unicode中文字符范围：4E00-9FFF（基本汉字）
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// ✅ 确保文本实体使用支持中文的字体
        /// </summary>
        private void EnsureChineseFontSupport(DBText dbText, Transaction tr)
        {
            try
            {
                var db = dbText.Database;
                var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

                // 尝试查找或创建支持中文的文本样式
                ObjectId chineseStyleId = GetOrCreateChineseTextStyle(textStyleTable, tr, db);

                if (!chineseStyleId.IsNull)
                {
                    dbText.TextStyleId = chineseStyleId;
                    Log.Debug($"已切换DBText到中文字体样式");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "切换DBText中文字体失败");
            }
        }

        /// <summary>
        /// ✅ 确保MText使用支持中文的字体
        /// </summary>
        private void EnsureChineseFontSupport(MText mText, Transaction tr)
        {
            try
            {
                var db = mText.Database;
                var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

                // 尝试查找或创建支持中文的文本样式
                ObjectId chineseStyleId = GetOrCreateChineseTextStyle(textStyleTable, tr, db);

                if (!chineseStyleId.IsNull)
                {
                    mText.TextStyleId = chineseStyleId;
                    Log.Debug($"已切换MText到中文字体样式");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "切换MText中文字体失败");
            }
        }

        /// <summary>
        /// ✅ 确保AttributeReference使用支持中文的字体
        /// </summary>
        private void EnsureChineseFontSupport(AttributeReference attRef, Transaction tr)
        {
            try
            {
                var db = attRef.Database;
                var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

                // 尝试查找或创建支持中文的文本样式
                ObjectId chineseStyleId = GetOrCreateChineseTextStyle(textStyleTable, tr, db);

                if (!chineseStyleId.IsNull)
                {
                    attRef.TextStyleId = chineseStyleId;
                    Log.Debug($"已切换AttributeReference到中文字体样式");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "切换AttributeReference中文字体失败");
            }
        }

        /// <summary>
        /// ✅ 确保AttributeDefinition使用支持中文的字体
        /// </summary>
        private void EnsureChineseFontSupport(AttributeDefinition attDef, Transaction tr)
        {
            try
            {
                var db = attDef.Database;
                var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

                // 尝试查找或创建支持中文的文本样式
                ObjectId chineseStyleId = GetOrCreateChineseTextStyle(textStyleTable, tr, db);

                if (!chineseStyleId.IsNull)
                {
                    attDef.TextStyleId = chineseStyleId;
                    Log.Debug($"已切换AttributeDefinition到中文字体样式");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "切换AttributeDefinition中文字体失败");
            }
        }

        /// <summary>
        /// ✅ 获取或创建支持中文的文本样式
        ///
        /// 优先级：
        /// 1. 使用AutoCAD标准中文字体 txt.shx + gbcbig.shx（最兼容）
        /// 2. 使用现有的中文样式
        /// 3. 创建新的中文样式
        /// </summary>
        private ObjectId GetOrCreateChineseTextStyle(TextStyleTable textStyleTable, Transaction tr, Database db)
        {
            // 1. 尝试查找现有的中文样式（常见名称）
            string[] commonChineseStyleNames = { "Chinese", "宋体", "黑体", "仿宋", "楷体", "SimSun", "SimHei" };

            foreach (var styleName in commonChineseStyleNames)
            {
                if (textStyleTable.Has(styleName))
                {
                    return textStyleTable[styleName];
                }
            }

            // 2. 如果没有找到，创建新的中文样式
            try
            {
                textStyleTable.UpgradeOpen();

                var chineseStyle = new TextStyleTableRecord
                {
                    Name = "BiaogeChinese",
                    FileName = "txt.shx",      // AutoCAD标准西文字体
                    BigFontFileName = "gbcbig.shx"  // AutoCAD标准中文大字体
                };

                ObjectId styleId = textStyleTable.Add(chineseStyle);
                tr.AddNewlyCreatedDBObject(chineseStyle, true);

                textStyleTable.DowngradeOpen();

                Log.Information("已创建新的中文字体样式: BiaogeChinese (txt.shx + gbcbig.shx)");
                return styleId;
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "创建中文字体样式失败");

                // 3. 如果创建失败，返回标准样式
                if (textStyleTable.Has("Standard"))
                {
                    return textStyleTable["Standard"];
                }

                return ObjectId.Null;
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
                        // ✅ AutoCAD 2022最佳实践: 完整的ObjectId有效性检查
                        if (objectId.IsNull || objectId.IsErased ||
                            objectId.IsEffectivelyErased || !objectId.IsValid)
                        {
                            Log.Warning($"ObjectId {objectId.Handle} 无效或已删除");
                            return false;
                        }

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
                            // ✅ 关键修复：使用Contents更新，并清理格式
                            mText.Contents = newContent;
                            // 确保文本样式正确
                            mText.TextStyleId = mText.Database.Textstyle;
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
                    catch (System.Exception ex)
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
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (update.ObjectId.IsNull || update.ObjectId.IsErased ||
                            update.ObjectId.IsEffectivelyErased || !update.ObjectId.IsValid)
                        {
                            continue;
                        }

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
                catch (System.Exception ex)
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
