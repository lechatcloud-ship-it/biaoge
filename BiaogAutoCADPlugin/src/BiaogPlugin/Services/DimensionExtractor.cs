using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// ✅ P2修复：AutoCAD Dimension实体提取器 - 直接从几何数据获取精确尺寸
    ///
    /// 核心优势：
    /// 1. 从Dimension.Measurement属性获取AutoCAD计算的精确测量值
    /// 2. 不依赖文本解析，避免格式化文本带来的误差
    /// 3. ✅ 支持所有9种Dimension派生类（Aligned, Rotated, Diametric, Radial, Ordinate, etc.）
    /// 4. 提取完整的几何信息（延伸线点、圆心、半径等）
    ///
    /// 修复：补全OrdinateDimension（坐标标注）支持，完善机械制图兼容性
    /// 基于AutoCAD .NET API 2025最佳实践
    /// </summary>
    public class DimensionExtractor
    {
        /// <summary>
        /// ✅ 提取当前DWG中的所有Dimension实体及其精确几何数据
        /// </summary>
        /// <returns>Dimension数据列表</returns>
        public List<DimensionData> ExtractAllDimensions()
        {
            var dimensions = new List<DimensionData>();
            var doc = Application.DocumentManager.MdiActiveDocument;

            if (doc == null)
            {
                Log.Warning("没有活动的文档");
                return dimensions;
            }

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Log.Debug("═══════════════════════════════════════════════════");
                    Log.Debug("开始提取Dimension实体 - 精确几何数据提取模式");
                    Log.Debug("═══════════════════════════════════════════════════");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    // 1. 提取模型空间中的Dimension
                    int beforeCount = dimensions.Count;
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead);
                    ExtractFromBlockTableRecord(modelSpace, tr, dimensions, "ModelSpace");
                    Log.Debug($"[步骤1] 模型空间提取: {dimensions.Count - beforeCount} 个Dimension");

                    // 2. 提取所有图纸空间（布局）中的Dimension
                    beforeCount = dimensions.Count;
                    var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                    int layoutCount = 0;
                    foreach (DBDictionaryEntry entry in layoutDict)
                    {
                        if (entry.Key == "Model") continue;

                        var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                        var layoutBtr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                        int layoutBeforeCount = dimensions.Count;
                        ExtractFromBlockTableRecord(layoutBtr, tr, dimensions, $"Layout:{entry.Key}");
                        Log.Debug($"  - 布局[{entry.Key}]: {dimensions.Count - layoutBeforeCount} 个Dimension");
                        layoutCount++;
                    }
                    Log.Debug($"[步骤2] {layoutCount}个布局空间提取: {dimensions.Count - beforeCount} 个Dimension");

                    // 3. 提取块定义中的Dimension（某些标准图块可能包含标注）
                    beforeCount = dimensions.Count;
                    ExtractFromAllBlockDefinitions(bt, tr, dimensions);
                    Log.Debug($"[步骤3] 块定义提取: {dimensions.Count - beforeCount} 个Dimension");

                    tr.Commit();

                    Log.Information($"═══════════════════════════════════════════════════");
                    Log.Information($"✅ Dimension提取完成: 总计 {dimensions.Count} 个标注实体");
                    Log.Information($"═══════════════════════════════════════════════════");
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "提取Dimension时发生错误");
                    tr.Abort();
                    throw;
                }
            }

            return dimensions;
        }

        /// <summary>
        /// ✅ 从指定的BlockTableRecord中提取所有Dimension
        /// </summary>
        private void ExtractFromBlockTableRecord(
            BlockTableRecord btr,
            Transaction tr,
            List<DimensionData> dimensions,
            string spaceName)
        {
            foreach (ObjectId objId in btr)
            {
                // 验证ObjectId有效性
                if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                    continue;

                var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;

                // ✅ 处理Dimension及其所有派生类
                if (ent is Dimension dimension)
                {
                    var dimData = ExtractDimensionData(dimension, objId, spaceName);
                    if (dimData != null)
                    {
                        dimensions.Add(dimData);
                    }
                }

                // 递归处理块参照中的Dimension
                if (ent is BlockReference blockRef)
                {
                    ExtractFromNestedBlock(blockRef, tr, dimensions, spaceName);
                }
            }
        }

        /// <summary>
        /// ✅ P2修复：从Dimension实体提取详细几何数据（支持所有9种派生类）
        ///
        /// 修复前：只处理8种类型，遗漏OrdinateDimension
        /// 修复后：补全9种类型
        ///
        /// 参考：AutoCAD .NET API官方文档
        /// - AlignedDimension: XLine1Point, XLine2Point, DimLinePoint
        /// - RotatedDimension: XLine1Point, XLine2Point, DimLinePoint, Rotation
        /// - DiametricDimension: ChordPoint, FarChordPoint
        /// - RadialDimension: Center, ChordPoint
        /// - RadialDimensionLarge: Center, ChordPoint, JogPoint
        /// - LineAngularDimension2: XLine1Start, XLine1End, XLine2Start, XLine2End
        /// - Point3AngularDimension: CenterPoint, XLine1Point, XLine2Point
        /// - ArcDimension: XLine1Point, XLine2Point, ArcPoint
        /// - OrdinateDimension: DefiningPoint, LeaderEndPoint, UsingXAxis
        /// </summary>
        private DimensionData? ExtractDimensionData(Dimension dimension, ObjectId objId, string spaceName)
        {
            try
            {
                var dimData = new DimensionData
                {
                    Id = objId,
                    Measurement = dimension.Measurement, // ✅ 关键：AutoCAD计算的精确测量值
                    DimensionText = dimension.DimensionText ?? "",
                    Layer = dimension.Layer,
                    TextPosition = dimension.TextPosition,
                    SpaceName = spaceName
                };

                // ✅ 根据具体的Dimension子类型提取特定的几何属性
                // 使用模式匹配（C# 7.0+）进行类型判断和提取

                // 1. 对齐标注（AlignedDimension）
                if (dimension is AlignedDimension aligned)
                {
                    dimData.DimensionType = DimensionType.Aligned;
                    dimData.XLine1Point = aligned.XLine1Point;
                    dimData.XLine2Point = aligned.XLine2Point;
                    dimData.DimLinePoint = aligned.DimLinePoint;

                    Log.Debug($"提取AlignedDimension: {dimData.Measurement:F3} ({aligned.XLine1Point} → {aligned.XLine2Point})");
                }
                // 2. 旋转标注（RotatedDimension）
                else if (dimension is RotatedDimension rotated)
                {
                    dimData.DimensionType = DimensionType.Rotated;
                    dimData.XLine1Point = rotated.XLine1Point;
                    dimData.XLine2Point = rotated.XLine2Point;
                    dimData.DimLinePoint = rotated.DimLinePoint;
                    dimData.Rotation = rotated.Rotation;

                    Log.Debug($"提取RotatedDimension: {dimData.Measurement:F3} (角度: {dimData.RotationDegrees:F1}°)");
                }
                // 3. 直径标注（DiametricDimension）
                else if (dimension is DiametricDimension diametric)
                {
                    dimData.DimensionType = DimensionType.Diametric;
                    dimData.ChordPoint = diametric.ChordPoint;
                    dimData.FarChordPoint = diametric.FarChordPoint;

                    // 计算圆心（直径标注的两个点的中点）
                    var center = new Autodesk.AutoCAD.Geometry.Point3d(
                        (diametric.ChordPoint.X + diametric.FarChordPoint.X) / 2,
                        (diametric.ChordPoint.Y + diametric.FarChordPoint.Y) / 2,
                        (diametric.ChordPoint.Z + diametric.FarChordPoint.Z) / 2
                    );
                    dimData.Center = center;

                    Log.Debug($"提取DiametricDimension: Φ{dimData.Measurement:F3}");
                }
                // 4. 半径标注（RadialDimension）
                else if (dimension is RadialDimension radial)
                {
                    dimData.DimensionType = DimensionType.Radial;
                    dimData.Center = radial.Center;
                    dimData.ChordPoint = radial.ChordPoint;

                    Log.Debug($"提取RadialDimension: R{dimData.Measurement:F3}");
                }
                // 5. 大半径标注（RadialDimensionLarge）
                else if (dimension is RadialDimensionLarge radialLarge)
                {
                    dimData.DimensionType = DimensionType.RadialLarge;
                    dimData.Center = radialLarge.Center;
                    dimData.ChordPoint = radialLarge.ChordPoint;
                    // RadialDimensionLarge还有JogPoint和OverrideCenter属性，根据需要可以扩展

                    Log.Debug($"提取RadialDimensionLarge: R{dimData.Measurement:F3}");
                }
                // 6. 角度标注-两线（LineAngularDimension2）
                else if (dimension is LineAngularDimension2 lineAngular)
                {
                    dimData.DimensionType = DimensionType.LineAngular;
                    dimData.XLine1Start = lineAngular.XLine1Start;
                    dimData.XLine1End = lineAngular.XLine1End;
                    dimData.XLine2Start = lineAngular.XLine2Start;
                    dimData.XLine2End = lineAngular.XLine2End;
                    dimData.ArcPoint = lineAngular.ArcPoint;

                    Log.Debug($"提取LineAngularDimension2: {dimData.Measurement:F1}°");
                }
                // 7. 角度标注-三点（Point3AngularDimension）
                else if (dimension is Point3AngularDimension point3Angular)
                {
                    dimData.DimensionType = DimensionType.Point3Angular;
                    dimData.Center = point3Angular.CenterPoint;
                    dimData.XLine1Point = point3Angular.XLine1Point;
                    dimData.XLine2Point = point3Angular.XLine2Point;
                    dimData.ArcPoint = point3Angular.ArcPoint;

                    Log.Debug($"提取Point3AngularDimension: {dimData.Measurement:F1}°");
                }
                // 8. 弧长标注（ArcDimension）
                else if (dimension is ArcDimension arc)
                {
                    dimData.DimensionType = DimensionType.Arc;
                    dimData.XLine1Point = arc.XLine1Point;
                    dimData.XLine2Point = arc.XLine2Point;
                    dimData.ArcPoint = arc.ArcPoint;
                    dimData.Center = arc.CenterPoint;

                    Log.Debug($"提取ArcDimension: {dimData.Measurement:F3} (弧长)");
                }
                // 9. ✅ P2修复：坐标标注（OrdinateDimension）
                // 参考：https://adndevblog.typepad.com/autocad/2024/10/working-with-ucs-and-ordinate-dimensions-in-autocad-using-net-api.html
                else if (dimension is OrdinateDimension ordinate)
                {
                    dimData.DimensionType = DimensionType.Ordinate;
                    dimData.DefiningPoint = ordinate.DefiningPoint;
                    dimData.LeaderEndPoint = ordinate.LeaderEndPoint;
                    dimData.UsingXAxis = ordinate.UsingXAxis;

                    var axisLabel = ordinate.UsingXAxis ? "X" : "Y";
                    Log.Debug($"提取OrdinateDimension: {axisLabel}={dimData.Measurement:F3}");
                }
                // 基类Dimension（理论上不应该直接实例化，但保留处理）
                else
                {
                    Log.Warning($"检测到未特化的Dimension基类实例: {objId}");
                    // 使用默认值，只有基本的Measurement
                    dimData.DimensionType = DimensionType.Aligned; // 默认类型
                }

                return dimData;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, $"提取Dimension数据失败: {objId}");
                return null;
            }
        }

        /// <summary>
        /// ✅ 递归提取嵌套块中的Dimension
        /// </summary>
        private void ExtractFromNestedBlock(
            BlockReference blockRef,
            Transaction tr,
            List<DimensionData> dimensions,
            string parentSpace,
            int nestingLevel = 1,
            HashSet<ObjectId>? processedBlocks = null)
        {
            // 防止无限递归
            if (nestingLevel > 100)
            {
                Log.Warning($"嵌套深度超过100层，停止递归（可能存在循环引用）");
                return;
            }

            processedBlocks ??= new HashSet<ObjectId>();

            if (processedBlocks.Contains(blockRef.BlockTableRecord))
            {
                return;
            }
            processedBlocks.Add(blockRef.BlockTableRecord);

            try
            {
                var blockDef = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead);

                // 跳过外部参照
                if (blockDef.IsFromExternalReference)
                {
                    return;
                }

                foreach (ObjectId entityId in blockDef)
                {
                    var ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    // 提取Dimension
                    if (ent is Dimension dimension)
                    {
                        var dimData = ExtractDimensionData(dimension, entityId, parentSpace);
                        if (dimData != null)
                        {
                            dimensions.Add(dimData);
                        }
                    }
                    // 递归处理嵌套的BlockReference
                    else if (ent is BlockReference nestedBlockRef)
                    {
                        ExtractFromNestedBlock(
                            nestedBlockRef,
                            tr,
                            dimensions,
                            $"{parentSpace}:Level{nestingLevel + 1}",
                            nestingLevel + 1,
                            processedBlocks);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, $"提取嵌套块Dimension失败: {blockRef.Name}, Level={nestingLevel}");
            }
        }

        /// <summary>
        /// ✅ 提取所有块定义内部的Dimension
        /// </summary>
        private void ExtractFromAllBlockDefinitions(BlockTable bt, Transaction tr, List<DimensionData> dimensions)
        {
            var processedBlocks = new HashSet<ObjectId>();

            foreach (ObjectId btrId in bt)
            {
                var blockDef = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                // 跳过布局空间（已处理）
                if (blockDef.IsLayout)
                    continue;

                if (!processedBlocks.Add(btrId))
                    continue;

                foreach (ObjectId entityId in blockDef)
                {
                    try
                    {
                        var ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        if (ent is Dimension dimension)
                        {
                            var dimData = ExtractDimensionData(dimension, entityId, "BlockDefinition");
                            if (dimData != null)
                            {
                                dimensions.Add(dimData);
                            }
                        }
                        else if (ent is BlockReference nestedBlockRef)
                        {
                            ExtractFromNestedBlock(nestedBlockRef, tr, dimensions, "BlockDefinition");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"提取块定义Dimension失败: {entityId}");
                    }
                }
            }

            Log.Debug($"从块定义中提取Dimension，共处理 {processedBlocks.Count} 个块定义");
        }

        /// <summary>
        /// ✅ 统计Dimension类型分布
        /// </summary>
        public Dictionary<DimensionType, int> GetDimensionTypeStatistics(List<DimensionData> dimensions)
        {
            var stats = new Dictionary<DimensionType, int>();

            foreach (var dim in dimensions)
            {
                if (!stats.ContainsKey(dim.DimensionType))
                {
                    stats[dim.DimensionType] = 0;
                }
                stats[dim.DimensionType]++;
            }

            return stats;
        }

        /// <summary>
        /// ✅ 提取特定图层的Dimension
        /// </summary>
        public List<DimensionData> ExtractDimensionsByLayer(string layerName)
        {
            var allDimensions = ExtractAllDimensions();
            return allDimensions.FindAll(d => d.Layer == layerName);
        }

        /// <summary>
        /// ✅ 提取特定类型的Dimension
        /// </summary>
        public List<DimensionData> ExtractDimensionsByType(DimensionType dimensionType)
        {
            var allDimensions = ExtractAllDimensions();
            return allDimensions.FindAll(d => d.DimensionType == dimensionType);
        }
    }
}
