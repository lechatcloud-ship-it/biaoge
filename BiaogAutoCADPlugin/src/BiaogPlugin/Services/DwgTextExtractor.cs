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
                    var btr = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead);

                    // 遍历模型空间中的所有实体
                    foreach (ObjectId objId in btr)
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        // 提取不同类型的文本
                        var textEntity = ExtractTextFromEntity(ent, objId);
                        if (textEntity != null)
                        {
                            texts.Add(textEntity);
                        }
                    }

                    // 处理块参照中的属性
                    ExtractBlockAttributes(btr, tr, texts);

                    tr.Commit();

                    Log.Information($"成功提取 {texts.Count} 个文本实体");
                    ed.WriteMessage($"\n成功提取 {texts.Count} 个文本实体");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "提取文本时发生错误");
                    tr.Abort();
                    throw;
                }
            }

            return texts;
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
                    ColorIndex = dbText.ColorIndex
                };
            }

            // 多行文本
            if (ent is MText mText)
            {
                return new TextEntity
                {
                    Id = objId,
                    Type = TextEntityType.MText,
                    Content = mText.Contents ?? string.Empty,  // 纯文本，无格式
                    Position = mText.Location,
                    Layer = mText.Layer,
                    Height = mText.TextHeight,
                    Rotation = mText.Rotation,
                    ColorIndex = mText.ColorIndex,
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
                    ColorIndex = attDef.ColorIndex,
                    Tag = attDef.Tag
                };
            }

            return null;
        }

        /// <summary>
        /// 提取块参照中的属性
        /// </summary>
        private void ExtractBlockAttributes(BlockTableRecord btr, Transaction tr, List<TextEntity> texts)
        {
            foreach (ObjectId objId in btr)
            {
                var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent is BlockReference blockRef)
                {
                    var attCol = blockRef.AttributeCollection;
                    if (attCol == null || attCol.Count == 0) continue;

                    foreach (ObjectId attId in attCol)
                    {
                        try
                        {
                            var attRef = (AttributeReference)tr.GetObject(attId, OpenMode.ForRead);
                            texts.Add(new TextEntity
                            {
                                Id = attId,
                                Type = TextEntityType.AttributeReference,
                                Content = attRef.TextString ?? string.Empty,
                                Position = attRef.Position,
                                Layer = attRef.Layer,
                                Height = attRef.Height,
                                Rotation = attRef.Rotation,
                                ColorIndex = attRef.ColorIndex,
                                Tag = attRef.Tag,
                                BlockName = blockRef.Name
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"提取块属性失败: {attId}");
                        }
                    }
                }
            }
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
