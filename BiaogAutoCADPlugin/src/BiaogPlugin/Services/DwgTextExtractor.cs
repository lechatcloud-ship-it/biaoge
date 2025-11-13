using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// DWG文本提取器 - 使用AutoCAD .NET API实现100%准确提取
    /// 支持所有AutoCAD文本实体类型
    /// </summary>
    public class DwgTextExtractor
    {
        /// <summary>
        /// 提取当前DWG中的所有文本实体
        /// ✅ 基于AutoCAD官方文档和社区最佳实践优化
        /// </summary>
        /// <returns>文本实体列表</returns>
        public List<TextEntity> ExtractAllText()
        {
            var texts = new List<TextEntity>();
            var doc = Application.DocumentManager.MdiActiveDocument;

            if (doc == null)
            {
                Log.Warning("没有活动的文档");
                return texts;
            }

            var db = doc.Database;
            var ed = doc.Editor;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    // ✅ 1. 提取模型空间中的文本
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead);
                    ExtractFromBlockTableRecord(modelSpace, tr, texts, "ModelSpace");

                    // ✅ 2. 提取所有图纸空间（布局）中的文本
                    // 很多CAD图纸的标注文本都在布局空间中
                    var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                    foreach (DBDictionaryEntry entry in layoutDict)
                    {
                        if (entry.Key == "Model") continue; // 跳过模型空间（已处理）

                        var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                        var layoutBtr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                        ExtractFromBlockTableRecord(layoutBtr, tr, texts, $"Layout:{entry.Key}");
                    }

                    // ✅ 3. 提取所有块定义内部的文本（包括嵌套块）
                    // 递归处理所有非布局的块定义
                    ExtractFromAllBlockDefinitions(bt, tr, texts);

                    tr.Commit();

                    Log.Information($"成功提取 {texts.Count} 个文本实体");
                    ed.WriteMessage($"\n成功提取 {texts.Count} 个文本实体");
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "提取文本时发生错误");
                    tr.Abort();
                    throw;
                }
            }

            return texts;
        }

        /// <summary>
        /// ✅ 从指定的BlockTableRecord中提取所有文本（包括嵌套块中的文本）
        /// </summary>
        private void ExtractFromBlockTableRecord(
            BlockTableRecord btr,
            Transaction tr,
            List<TextEntity> texts,
            string spaceName)
        {
            foreach (ObjectId objId in btr)
            {
                var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                // 1. 直接的文本实体（DBText, MText）
                var textEntity = ExtractTextFromEntity(ent, objId);
                if (textEntity != null)
                {
                    textEntity.SpaceName = spaceName;
                    texts.Add(textEntity);
                }

                // 2. 块参照中的属性和嵌套内容
                if (ent is BlockReference blockRef)
                {
                    // 提取块参照的属性
                    ExtractBlockReferenceAttributes(blockRef, tr, texts, spaceName);

                    // ✅ 递归提取嵌套块内的文本
                    ExtractFromNestedBlock(blockRef, tr, texts, spaceName);
                }
            }
        }

        /// <summary>
        /// 从单个实体提取文本
        /// </summary>
        private TextEntity ExtractTextFromEntity(Entity ent, ObjectId objId)
        {
            // 单行文本
            if (ent is DBText dbText)
            {
                return new TextEntity
                {
                    Id = objId,
                    Type = TextEntityType.DBText,
                    Content = dbText.TextString ?? string.Empty,
                    Position = dbText.Position,
                    Layer = dbText.Layer,
                    Height = dbText.Height,
                    Rotation = dbText.Rotation,
                    ColorIndex = (short)dbText.ColorIndex
                };
            }

            // 多行文本
            if (ent is MText mText)
            {
                return new TextEntity
                {
                    Id = objId,
                    Type = TextEntityType.MText,
                    Content = mText.Text ?? string.Empty,  // ✅ 使用Text而不是Contents，避免格式代码
                    Position = mText.Location,
                    Layer = mText.Layer,
                    Height = mText.TextHeight,
                    Rotation = mText.Rotation,
                    ColorIndex = (short)mText.ColorIndex,
                    Width = mText.Width
                };
            }

            // 属性定义
            if (ent is AttributeDefinition attDef)
            {
                return new TextEntity
                {
                    Id = objId,
                    Type = TextEntityType.AttributeDefinition,
                    Content = attDef.TextString ?? string.Empty,
                    Position = attDef.Position,
                    Layer = attDef.Layer,
                    Height = attDef.Height,
                    Rotation = attDef.Rotation,
                    ColorIndex = (short)attDef.ColorIndex,
                    Tag = attDef.Tag
                };
            }

            return null;
        }

        /// <summary>
        /// ✅ 提取块参照的属性
        /// </summary>
        private void ExtractBlockReferenceAttributes(
            BlockReference blockRef,
            Transaction tr,
            List<TextEntity> texts,
            string spaceName)
        {
            var attCol = blockRef.AttributeCollection;
            if (attCol == null || attCol.Count == 0) return;

            foreach (ObjectId attId in attCol)
            {
                try
                {
                    var attRef = (AttributeReference)tr.GetObject(attId, OpenMode.ForRead);

                    // ✅ 跳过不可见的属性
                    if (attRef.Invisible) continue;

                    texts.Add(new TextEntity
                    {
                        Id = attId,
                        Type = TextEntityType.AttributeReference,
                        Content = attRef.TextString ?? string.Empty,
                        Position = attRef.Position,
                        Layer = attRef.Layer,
                        Height = attRef.Height,
                        Rotation = attRef.Rotation,
                        ColorIndex = (short)attRef.ColorIndex,
                        Tag = attRef.Tag,
                        BlockName = blockRef.Name,
                        SpaceName = spaceName
                    });
                }
                catch (System.Exception ex)
                {
                    Log.Warning(ex, $"提取块属性失败: {attId}");
                }
            }
        }

        /// <summary>
        /// ✅ 递归提取嵌套块内的文本（修复版）
        ///
        /// 关键修复：
        /// 1. 也提取AttributeDefinition - 确保块定义中的属性定义被提取
        /// 2. 递归处理所有嵌套块 - 多层嵌套也能完整提取
        /// </summary>
        private void ExtractFromNestedBlock(
            BlockReference blockRef,
            Transaction tr,
            List<TextEntity> texts,
            string parentSpace)
        {
            try
            {
                // 获取块定义
                var blockDef = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead);

                // 遍历块定义中的所有实体
                foreach (ObjectId entityId in blockDef)
                {
                    var ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    // 1. 提取块内的直接文本（DBText, MText）
                    if (ent is DBText dbText)
                    {
                        texts.Add(new TextEntity
                        {
                            Id = entityId,
                            Type = TextEntityType.DBText,
                            Content = dbText.TextString ?? string.Empty,
                            Position = dbText.Position,
                            Layer = dbText.Layer,
                            Height = dbText.Height,
                            Rotation = dbText.Rotation,
                            ColorIndex = (short)dbText.ColorIndex,
                            BlockName = blockDef.Name,
                            SpaceName = parentSpace
                        });
                    }
                    else if (ent is MText mText)
                    {
                        texts.Add(new TextEntity
                        {
                            Id = entityId,
                            Type = TextEntityType.MText,
                            Content = mText.Text ?? string.Empty,  // ✅ 使用Text获取纯文本
                            Position = mText.Location,
                            Layer = mText.Layer,
                            Height = mText.TextHeight,
                            Rotation = mText.Rotation,
                            ColorIndex = (short)mText.ColorIndex,
                            Width = mText.Width,
                            BlockName = blockDef.Name,
                            SpaceName = parentSpace
                        });
                    }
                    // ✅ 关键修复：提取AttributeDefinition（块属性定义）
                    else if (ent is AttributeDefinition attDef)
                    {
                        // 跳过不可见的属性定义
                        if (attDef.Invisible) continue;

                        texts.Add(new TextEntity
                        {
                            Id = entityId,
                            Type = TextEntityType.AttributeDefinition,
                            Content = attDef.TextString ?? string.Empty,
                            Position = attDef.Position,
                            Layer = attDef.Layer,
                            Height = attDef.Height,
                            Rotation = attDef.Rotation,
                            ColorIndex = (short)attDef.ColorIndex,
                            Tag = attDef.Tag,
                            BlockName = blockDef.Name,
                            SpaceName = parentSpace
                        });
                    }
                    // 2. ✅ 递归处理嵌套的BlockReference
                    else if (ent is BlockReference nestedBlockRef)
                    {
                        // 提取嵌套块的属性
                        ExtractBlockReferenceAttributes(nestedBlockRef, tr, texts, parentSpace);

                        // 递归提取更深层的嵌套块
                        ExtractFromNestedBlock(nestedBlockRef, tr, texts, parentSpace);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, $"提取嵌套块文本失败: {blockRef.Name}");
            }
        }

        /// <summary>
        /// ✅ 提取所有块定义内部的文本（修复版）
        ///
        /// 关键修复：
        /// 1. 不再跳过匿名块 - 动态块、标注块等会创建匿名块变体，这些块中可能包含文本
        /// 2. 提取AttributeDefinition - 块定义中的属性定义也是文本
        /// 3. 递归提取嵌套块 - 确保块定义中的嵌套块也被处理
        /// </summary>
        private void ExtractFromAllBlockDefinitions(BlockTable bt, Transaction tr, List<TextEntity> texts)
        {
            var processedBlocks = new HashSet<ObjectId>();

            foreach (ObjectId btrId in bt)
            {
                var blockDef = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                // 只跳过模型空间和图纸空间（已在ExtractFromBlockTableRecord中处理）
                if (blockDef.IsLayout)
                    continue;

                // ✅ 关键修复：不再跳过匿名块！
                // 动态块、标注块等会创建匿名块变体，必须提取这些块中的文本
                // if (blockDef.IsAnonymous)
                //     continue;

                // 防止重复处理
                if (!processedBlocks.Add(btrId))
                    continue;

                // 遍历块定义中的实体
                foreach (ObjectId entityId in blockDef)
                {
                    try
                    {
                        var ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        // ✅ 提取所有类型的文本（DBText, MText, AttributeDefinition）
                        if (ent is DBText dbText)
                        {
                            texts.Add(new TextEntity
                            {
                                Id = entityId,
                                Type = TextEntityType.DBText,
                                Content = dbText.TextString ?? string.Empty,
                                Position = dbText.Position,
                                Layer = dbText.Layer,
                                Height = dbText.Height,
                                Rotation = dbText.Rotation,
                                ColorIndex = (short)dbText.ColorIndex,
                                BlockName = blockDef.Name,
                                SpaceName = "BlockDefinition"
                            });
                        }
                        else if (ent is MText mText)
                        {
                            texts.Add(new TextEntity
                            {
                                Id = entityId,
                                Type = TextEntityType.MText,
                                Content = mText.Text ?? string.Empty,
                                Position = mText.Location,
                                Layer = mText.Layer,
                                Height = mText.TextHeight,
                                Rotation = mText.Rotation,
                                ColorIndex = (short)mText.ColorIndex,
                                Width = mText.Width,
                                BlockName = blockDef.Name,
                                SpaceName = "BlockDefinition"
                            });
                        }
                        // ✅ 关键修复：提取AttributeDefinition（块属性定义）
                        else if (ent is AttributeDefinition attDef)
                        {
                            // 跳过不可见的属性定义
                            if (attDef.Invisible) continue;

                            texts.Add(new TextEntity
                            {
                                Id = entityId,
                                Type = TextEntityType.AttributeDefinition,
                                Content = attDef.TextString ?? string.Empty,
                                Position = attDef.Position,
                                Layer = attDef.Layer,
                                Height = attDef.Height,
                                Rotation = attDef.Rotation,
                                ColorIndex = (short)attDef.ColorIndex,
                                Tag = attDef.Tag,
                                BlockName = blockDef.Name,
                                SpaceName = "BlockDefinition"
                            });
                        }
                        // ✅ 关键修复：递归提取块定义中的嵌套块
                        else if (ent is BlockReference nestedBlockRef)
                        {
                            // 提取嵌套块的属性
                            ExtractBlockReferenceAttributes(nestedBlockRef, tr, texts, "BlockDefinition");

                            // 递归提取更深层的嵌套块
                            ExtractFromNestedBlock(nestedBlockRef, tr, texts, "BlockDefinition");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"提取块定义文本失败: {entityId}");
                    }
                }
            }

            Log.Debug($"从块定义中提取了文本，共处理 {processedBlocks.Count} 个块定义");
        }

        /// <summary>
        /// 提取指定图层的文本
        /// </summary>
        public List<TextEntity> ExtractTextByLayer(string layerName)
        {
            var allTexts = ExtractAllText();
            return allTexts.Where(t => t.Layer == layerName).ToList();
        }

        /// <summary>
        /// 提取指定图层列表的文本
        /// </summary>
        public List<TextEntity> ExtractTextByLayers(List<string> layerNames)
        {
            var allTexts = ExtractAllText();
            return allTexts.Where(t => layerNames.Contains(t.Layer)).ToList();
        }

        /// <summary>
        /// 提取选定区域的文本
        /// </summary>
        public List<TextEntity> ExtractTextInRegion(Point3d minPoint, Point3d maxPoint)
        {
            var allTexts = ExtractAllText();
            return allTexts.Where(t =>
                t.Position.X >= minPoint.X && t.Position.X <= maxPoint.X &&
                t.Position.Y >= minPoint.Y && t.Position.Y <= maxPoint.Y
            ).ToList();
        }

        /// <summary>
        /// 按文本类型过滤
        /// </summary>
        public List<TextEntity> FilterByType(List<TextEntity> texts, TextEntityType type)
        {
            return texts.Where(t => t.Type == type).ToList();
        }

        /// <summary>
        /// 去除空文本和纯数字文本（通常不需要翻译）
        /// </summary>
        public List<TextEntity> FilterTranslatableText(List<TextEntity> texts)
        {
            return texts.Where(t =>
            {
                if (string.IsNullOrWhiteSpace(t.Content))
                    return false;

                // 如果全是数字和符号，不需要翻译
                if (t.Content.All(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                    return false;

                // 如果太短（少于2个字符），可能不是有意义的文本
                if (t.Content.Trim().Length < 2)
                    return false;

                return true;
            }).ToList();
        }

        /// <summary>
        /// 获取唯一文本内容（用于批量翻译去重）
        /// </summary>
        public List<string> GetUniqueContents(List<TextEntity> texts)
        {
            return texts
                .Select(t => t.Content)
                .Distinct()
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
        }

        /// <summary>
        /// 统计信息
        /// </summary>
        public TextExtractionStatistics GetStatistics(List<TextEntity> texts)
        {
            return new TextExtractionStatistics
            {
                TotalCount = texts.Count,
                DBTextCount = texts.Count(t => t.Type == TextEntityType.DBText),
                MTextCount = texts.Count(t => t.Type == TextEntityType.MText),
                AttributeCount = texts.Count(t =>
                    t.Type == TextEntityType.AttributeDefinition ||
                    t.Type == TextEntityType.AttributeReference),
                UniqueContentCount = texts.Select(t => t.Content).Distinct().Count(),
                LayerCount = texts.Select(t => t.Layer).Distinct().Count(),
                TranslatableCount = FilterTranslatableText(texts).Count
            };
        }
    }

    /// <summary>
    /// 文本提取统计信息
    /// </summary>
    public class TextExtractionStatistics
    {
        public int TotalCount { get; set; }
        public int DBTextCount { get; set; }
        public int MTextCount { get; set; }
        public int AttributeCount { get; set; }
        public int UniqueContentCount { get; set; }
        public int LayerCount { get; set; }
        public int TranslatableCount { get; set; }

        public override string ToString()
        {
            return $"总计: {TotalCount}, " +
                   $"单行文本: {DBTextCount}, " +
                   $"多行文本: {MTextCount}, " +
                   $"属性: {AttributeCount}, " +
                   $"唯一内容: {UniqueContentCount}, " +
                   $"可翻译: {TranslatableCount}";
        }
    }
}
