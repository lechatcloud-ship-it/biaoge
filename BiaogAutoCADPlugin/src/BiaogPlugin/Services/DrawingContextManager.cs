using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 图纸上下文管理器 - 提取图纸完整信息供AI助手使用
    /// </summary>
    public class DrawingContextManager
    {
        /// <summary>
        /// 获取当前图纸的完整上下文信息
        /// </summary>
        public DrawingContext GetCurrentDrawingContext()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                Log.Warning("没有打开的图纸");
                return new DrawingContext { ErrorMessage = "当前没有打开的图纸" };
            }

            var db = doc.Database;
            var context = new DrawingContext
            {
                FileName = doc.Name,
                IsSaved = !doc.IsReadOnly && !string.IsNullOrEmpty(doc.Name)
            };

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 1. 提取图层信息
                    context.Layers = ExtractLayerInfo(db, tr);

                    // 2. 提取文本实体
                    context.TextEntities = ExtractTextEntities(db, tr);

                    // 3. 提取图形实体统计
                    context.EntityStatistics = ExtractEntityStatistics(db, tr);

                    // 4. 提取图纸元数据
                    context.Metadata = ExtractMetadata(db, tr);

                    tr.Commit();
                }

                // 5. 生成可读的文本摘要
                context.Summary = GenerateSummary(context);

                Log.Information($"成功提取图纸上下文: {context.TextEntities.Count} 个文本实体, {context.Layers.Count} 个图层");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "提取图纸上下文失败");
                context.ErrorMessage = $"提取图纸信息失败: {ex.Message}";
            }

            return context;
        }

        /// <summary>
        /// 提取图层信息
        /// </summary>
        private List<LayerInfo> ExtractLayerInfo(Database db, Transaction tr)
        {
            var layers = new List<LayerInfo>();

            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId layerId in layerTable)
            {
                var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                layers.Add(new LayerInfo
                {
                    Name = layer.Name,
                    IsOn = !layer.IsOff,
                    IsFrozen = layer.IsFrozen,
                    IsLocked = layer.IsLocked,
                    Color = layer.Color.ColorIndex.ToString()
                });
            }

            return layers;
        }

        /// <summary>
        /// 提取文本实体
        /// </summary>
        private List<TextEntityInfo> ExtractTextEntities(Database db, Transaction tr)
        {
            var texts = new List<TextEntityInfo>();

            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objId in modelSpace)
            {
                var entity = tr.GetObject(objId, OpenMode.ForRead);

                if (entity is DBText dbText)
                {
                    texts.Add(new TextEntityInfo
                    {
                        Type = "DBText",
                        Content = dbText.TextString,
                        Layer = dbText.Layer,
                        Position = $"({dbText.Position.X:F2}, {dbText.Position.Y:F2})",
                        Height = dbText.Height,
                        ObjectId = objId.ToString()
                    });
                }
                else if (entity is MText mText)
                {
                    texts.Add(new TextEntityInfo
                    {
                        Type = "MText",
                        Content = mText.Contents,
                        Layer = mText.Layer,
                        Position = $"({mText.Location.X:F2}, {mText.Location.Y:F2})",
                        Height = mText.TextHeight,
                        ObjectId = objId.ToString()
                    });
                }
                else if (entity is BlockReference blockRef)
                {
                    // 提取块参照中的属性
                    foreach (ObjectId attId in blockRef.AttributeCollection)
                    {
                        var att = (AttributeReference)tr.GetObject(attId, OpenMode.ForRead);
                        texts.Add(new TextEntityInfo
                        {
                            Type = "Attribute",
                            Content = att.TextString,
                            Layer = att.Layer,
                            Position = $"({att.Position.X:F2}, {att.Position.Y:F2})",
                            Height = att.Height,
                            ObjectId = attId.ToString()
                        });
                    }
                }
            }

            return texts;
        }

        /// <summary>
        /// 提取图形实体统计
        /// </summary>
        private Dictionary<string, int> ExtractEntityStatistics(Database db, Transaction tr)
        {
            var stats = new Dictionary<string, int>();

            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objId in modelSpace)
            {
                var entity = tr.GetObject(objId, OpenMode.ForRead);
                var typeName = entity.GetType().Name;

                if (stats.ContainsKey(typeName))
                    stats[typeName]++;
                else
                    stats[typeName] = 1;
            }

            return stats;
        }

        /// <summary>
        /// 提取图纸元数据
        /// </summary>
        private Dictionary<string, string> ExtractMetadata(Database db, Transaction tr)
        {
            var metadata = new Dictionary<string, string>();

            try
            {
                var summaryInfo = db.SummaryInfo;
                if (summaryInfo != null)
                {
                    metadata["Title"] = summaryInfo.Title ?? "";
                    metadata["Subject"] = summaryInfo.Subject ?? "";
                    metadata["Author"] = summaryInfo.Author ?? "";
                    metadata["Keywords"] = summaryInfo.Keywords ?? "";
                    metadata["Comments"] = summaryInfo.Comments ?? "";
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "提取元数据失败");
            }

            // 添加数据库基本信息
            metadata["TotalEntities"] = db.ApproxNumObjects.ToString();
            metadata["Extents"] = $"{db.Extmin} to {db.Extmax}";

            return metadata;
        }

        /// <summary>
        /// 生成图纸摘要（供AI理解）
        /// </summary>
        private string GenerateSummary(DrawingContext context)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# 图纸信息摘要");
            sb.AppendLine();
            sb.AppendLine($"**文件名**: {context.FileName}");
            sb.AppendLine($"**保存状态**: {(context.IsSaved ? "已保存" : "未保存")}");
            sb.AppendLine();

            sb.AppendLine($"## 图层统计 ({context.Layers.Count} 个图层)");
            var activeLayers = context.Layers.Where(l => l.IsOn && !l.IsFrozen).ToList();
            sb.AppendLine($"- 激活图层: {activeLayers.Count}");
            sb.AppendLine($"- 关闭/冻结图层: {context.Layers.Count - activeLayers.Count}");
            sb.AppendLine();

            sb.AppendLine($"## 文本实体 ({context.TextEntities.Count} 个)");
            var byType = context.TextEntities.GroupBy(t => t.Type).ToList();
            foreach (var group in byType)
            {
                sb.AppendLine($"- {group.Key}: {group.Count()} 个");
            }
            sb.AppendLine();

            sb.AppendLine($"## 图形实体统计");
            foreach (var kvp in context.EntityStatistics.OrderByDescending(x => x.Value).Take(10))
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value} 个");
            }
            sb.AppendLine();

            sb.AppendLine($"## 常见文本内容示例");
            var sampleTexts = context.TextEntities
                .Where(t => !string.IsNullOrWhiteSpace(t.Content))
                .Take(20)
                .Select(t => $"- {t.Content} (图层: {t.Layer})")
                .ToList();
            foreach (var text in sampleTexts)
            {
                sb.AppendLine(text);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 图纸上下文信息
    /// </summary>
    public class DrawingContext
    {
        public string FileName { get; set; } = "";
        public bool IsSaved { get; set; }
        public List<LayerInfo> Layers { get; set; } = new();
        public List<TextEntityInfo> TextEntities { get; set; } = new();
        public Dictionary<string, int> EntityStatistics { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string Summary { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 图层信息
    /// </summary>
    public class LayerInfo
    {
        public string Name { get; set; } = "";
        public bool IsOn { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public string Color { get; set; } = "";
    }

    /// <summary>
    /// 文本实体信息
    /// </summary>
    public class TextEntityInfo
    {
        public string Type { get; set; } = "";
        public string Content { get; set; } = "";
        public string Layer { get; set; } = "";
        public string Position { get; set; } = "";
        public double Height { get; set; }
        public string ObjectId { get; set; } = "";
    }
}
