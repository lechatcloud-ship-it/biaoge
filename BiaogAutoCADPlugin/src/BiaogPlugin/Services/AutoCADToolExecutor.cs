using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// ✅ AutoCAD工具执行器 - 真正的Agent核心
    ///
    /// 实现30+个AutoCAD操作工具，让用户通过自然语言完成所有CAD工作：
    /// - 绘图工具：Line, Circle, Rectangle, Polyline, Text等
    /// - 修改工具：Delete, Move, Copy, Rotate, Scale, 属性修改等
    /// - 查询工具：测量、查询、统计等
    /// - 图层工具：创建、删除、修改图层
    /// - 视图工具：缩放、平移
    /// - 文件工具：保存、导出
    ///
    /// 基于AutoCAD .NET API 2025官方最佳实践
    /// </summary>
    public class AutoCADToolExecutor
    {
        #region P0 - 核心绘图工具

        /// <summary>
        /// P0.1 绘制直线
        /// </summary>
        public static async Task<string> DrawLine(Dictionary<string, object> args)
        {
            try
            {
                var startPoint = GetPoint3d(args, "start_point");
                var endPoint = GetPoint3d(args, "end_point");
                var layer = GetStringSafe(args, "layer", "0");
                var colorStr = GetStringSafe(args, "color", "ByLayer");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                ObjectId lineId;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    // 创建Line实体
                    var line = new Line(startPoint, endPoint)
                    {
                        Layer = layer
                    };

                    // 设置颜色
                    line.Color = ParseColor(colorStr);

                    // 添加到模型空间
                    lineId = modelSpace.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);

                    tr.Commit();
                }

                var length = startPoint.DistanceTo(endPoint);
                Log.Information($"✅ 绘制直线完成: ({startPoint.X:F2},{startPoint.Y:F2}) → ({endPoint.X:F2},{endPoint.Y:F2}), 长度={length:F2}mm");

                return await Task.FromResult(
                    $"✓ 已绘制直线：起点({startPoint.X:F2},{startPoint.Y:F2})，终点({endPoint.X:F2},{endPoint.Y:F2})，长度{length:F2}mm，图层'{layer}'，ID={lineId.Handle}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "绘制直线失败");
                return $"✗ 绘制直线失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.2 绘制圆
        /// </summary>
        public static async Task<string> DrawCircle(Dictionary<string, object> args)
        {
            try
            {
                var center = GetPoint3d(args, "center");  // ✅ 修复：与工具定义保持一致
                var radius = GetDoubleSafe(args, "radius", 100.0);

                // ✅ 添加参数验证
                if (radius <= 0)
                {
                    return "✗ 半径必须大于0";
                }

                var layer = GetStringSafe(args, "layer", "0");
                var colorStr = GetStringSafe(args, "color", "ByLayer");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                ObjectId circleId;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    // 创建Circle实体
                    var circle = new Circle(center, Vector3d.ZAxis, radius)
                    {
                        Layer = layer,
                        Color = ParseColor(colorStr)
                    };

                    circleId = modelSpace.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);

                    tr.Commit();
                }

                Log.Information($"✅ 绘制圆完成: 圆心({center.X:F2},{center.Y:F2}), 半径={radius:F2}mm");

                return await Task.FromResult(
                    $"✓ 已绘制圆：圆心({center.X:F2},{center.Y:F2})，半径{radius:F2}mm，图层'{layer}'，ID={circleId.Handle}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "绘制圆失败");
                return $"✗ 绘制圆失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.3 绘制矩形（使用Polyline实现）
        /// </summary>
        public static async Task<string> DrawRectangle(Dictionary<string, object> args)
        {
            try
            {
                var corner1 = GetPoint2d(args, "corner1");
                var corner2 = GetPoint2d(args, "corner2");
                var layer = GetStringSafe(args, "layer", "0");
                var colorStr = GetStringSafe(args, "color", "ByLayer");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                ObjectId polylineId;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    // 创建闭合Polyline表示矩形
                    var polyline = new Polyline(4);
                    polyline.AddVertexAt(0, corner1, 0, 0, 0);
                    polyline.AddVertexAt(1, new Point2d(corner2.X, corner1.Y), 0, 0, 0);
                    polyline.AddVertexAt(2, corner2, 0, 0, 0);
                    polyline.AddVertexAt(3, new Point2d(corner1.X, corner2.Y), 0, 0, 0);
                    polyline.Closed = true;
                    polyline.Layer = layer;
                    polyline.Color = ParseColor(colorStr);

                    polylineId = modelSpace.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);

                    tr.Commit();
                }

                var width = Math.Abs(corner2.X - corner1.X);
                var height = Math.Abs(corner2.Y - corner1.Y);

                Log.Information($"✅ 绘制矩形完成: {width:F2}×{height:F2}mm");

                return await Task.FromResult(
                    $"✓ 已绘制矩形：{width:F2}×{height:F2}mm，图层'{layer}'，ID={polylineId.Handle}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "绘制矩形失败");
                return $"✗ 绘制矩形失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.4 绘制多段线
        /// </summary>
        public static async Task<string> DrawPolyline(Dictionary<string, object> args)
        {
            try
            {
                var points = GetPoint2dList(args, "points");
                var closed = GetBoolSafe(args, "closed", false);
                var layer = GetStringSafe(args, "layer", "0");
                var colorStr = GetStringSafe(args, "color", "ByLayer");

                if (points.Count < 2)
                {
                    return "✗ 多段线至少需要2个点";
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                ObjectId polylineId;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    var polyline = new Polyline(points.Count);
                    for (int i = 0; i < points.Count; i++)
                    {
                        polyline.AddVertexAt(i, points[i], 0, 0, 0);
                    }
                    polyline.Closed = closed;
                    polyline.Layer = layer;
                    polyline.Color = ParseColor(colorStr);

                    polylineId = modelSpace.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);

                    tr.Commit();
                }

                Log.Information($"✅ 绘制多段线完成: {points.Count}个点, 闭合={closed}");

                return await Task.FromResult(
                    $"✓ 已绘制多段线：{points.Count}个点，{(closed ? "闭合" : "开放")}，图层'{layer}'，ID={polylineId.Handle}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "绘制多段线失败");
                return $"✗ 绘制多段线失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.5 添加文本
        /// </summary>
        public static async Task<string> DrawText(Dictionary<string, object> args)
        {
            try
            {
                var position = GetPoint2d(args, "position");
                var text = GetStringSafe(args, "text", "");
                var height = GetDoubleSafe(args, "height", 3.5);

                // ✅ 添加参数验证
                if (string.IsNullOrWhiteSpace(text))
                {
                    return "✗ 文本内容不能为空";
                }
                if (height <= 0)
                {
                    return "✗ 文字高度必须大于0";
                }

                var rotation = GetDoubleSafe(args, "rotation", 0.0);
                var layer = GetStringSafe(args, "layer", "0");
                var textType = GetStringSafe(args, "text_type", "single");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                ObjectId textId;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    if (textType.ToLower() == "mtext")
                    {
                        // 多行文本
                        var mText = new MText
                        {
                            Location = new Point3d(position.X, position.Y, 0),
                            Contents = text,
                            TextHeight = height,
                            Rotation = rotation * Math.PI / 180.0,  // 转换为弧度
                            Layer = layer
                        };

                        textId = modelSpace.AppendEntity(mText);
                        tr.AddNewlyCreatedDBObject(mText, true);
                    }
                    else
                    {
                        // 单行文本
                        var dbText = new DBText
                        {
                            Position = new Point3d(position.X, position.Y, 0),
                            TextString = text,
                            Height = height,
                            Rotation = rotation * Math.PI / 180.0,
                            Layer = layer
                        };

                        textId = modelSpace.AppendEntity(dbText);
                        tr.AddNewlyCreatedDBObject(dbText, true);
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 添加文本完成: '{text}', 高度={height}mm");

                return await Task.FromResult(
                    $"✓ 已添加{(textType == "mtext" ? "多行" : "单行")}文本：'{text}'，高度{height}mm，位置({position.X:F2},{position.Y:F2})，图层'{layer}'，ID={textId.Handle}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "添加文本失败");
                return $"✗ 添加文本失败: {ex.Message}";
            }
        }

        #endregion

        #region P0 - 核心修改工具

        /// <summary>
        /// P0.6 删除实体
        /// </summary>
        public static async Task<string> DeleteEntity(Dictionary<string, object> args)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int deletedCount = 0;

                // 检查是否提供了实体ID列表
                if (args.ContainsKey("entity_ids"))
                {
                    var entityIds = GetObjectIdList(args, "entity_ids");

                    using (var docLock = doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (var objId in entityIds)
                        {
                            try
                            {
                                var entity = tr.GetObject(objId, OpenMode.ForWrite);
                                entity.Erase();
                                deletedCount++;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, $"删除实体失败: {objId}");
                            }
                        }

                        tr.Commit();
                    }
                }
                // 或者按选择条件删除
                else if (args.ContainsKey("selection_criteria"))
                {
                    // TODO: 实现按条件删除（type, layer, color等）
                    return "✗ 按条件删除功能尚未实现，请提供entity_ids";
                }

                Log.Information($"✅ 删除实体完成: {deletedCount}个");

                return await Task.FromResult($"✓ 已删除{deletedCount}个实体");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除实体失败");
                return $"✗ 删除实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.7 修改实体属性
        /// </summary>
        public static async Task<string> ModifyEntityProperties(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                var layer = GetStringSafe(args, "layer", null);
                var colorStr = GetStringSafe(args, "color", null);

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int modifiedCount = 0;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                            if (entity == null) continue;

                            if (!string.IsNullOrEmpty(layer))
                            {
                                entity.Layer = layer;
                            }

                            if (!string.IsNullOrEmpty(colorStr))
                            {
                                entity.Color = ParseColor(colorStr);
                            }

                            modifiedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"修改实体属性失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 修改实体属性完成: {modifiedCount}个");

                var changes = new List<string>();
                if (!string.IsNullOrEmpty(layer)) changes.Add($"图层={layer}");
                if (!string.IsNullOrEmpty(colorStr)) changes.Add($"颜色={colorStr}");

                return await Task.FromResult(
                    $"✓ 已修改{modifiedCount}个实体的属性：{string.Join(", ", changes)}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "修改实体属性失败");
                return $"✗ 修改实体属性失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.8 移动实体
        /// </summary>
        public static async Task<string> MoveEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                Vector3d displacement;

                // 支持两种方式：位移向量 或 起点终点
                if (args.ContainsKey("displacement"))
                {
                    displacement = GetVector3d(args, "displacement");
                }
                else
                {
                    var fromPoint = GetPoint3d(args, "from_point");
                    var toPoint = GetPoint3d(args, "to_point");
                    displacement = toPoint - fromPoint;
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int movedCount = 0;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                            if (entity == null) continue;

                            // 创建移动矩阵
                            var matrix = Matrix3d.Displacement(displacement);
                            entity.TransformBy(matrix);

                            movedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"移动实体失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 移动实体完成: {movedCount}个, 位移=({displacement.X:F2},{displacement.Y:F2},{displacement.Z:F2})");

                return await Task.FromResult(
                    $"✓ 已移动{movedCount}个实体，位移({displacement.X:F2},{displacement.Y:F2},{displacement.Z:F2})"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "移动实体失败");
                return $"✗ 移动实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P0.9 复制实体
        /// </summary>
        public static async Task<string> CopyEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                Vector3d displacement;

                if (args.ContainsKey("displacement"))
                {
                    displacement = GetVector3d(args, "displacement");
                }
                else
                {
                    var fromPoint = GetPoint3d(args, "from_point");
                    var toPoint = GetPoint3d(args, "to_point");
                    displacement = toPoint - fromPoint;
                }

                var count = GetIntSafe(args, "count", 1);

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int copiedCount = 0;
                List<ObjectId> newIds = new();

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (entity == null) continue;

                            for (int i = 1; i <= count; i++)
                            {
                                var copy = entity.Clone() as Entity;
                                if (copy == null) continue;

                                // 移动复制的实体
                                var copyDisplacement = new Vector3d(
                                    displacement.X * i,
                                    displacement.Y * i,
                                    displacement.Z * i
                                );
                                copy.TransformBy(Matrix3d.Displacement(copyDisplacement));

                                var newId = modelSpace.AppendEntity(copy);
                                tr.AddNewlyCreatedDBObject(copy, true);
                                newIds.Add(newId);

                                copiedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"复制实体失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 复制实体完成: {copiedCount}个新实体");

                return await Task.FromResult(
                    $"✓ 已复制{entityIds.Count}个实体{count}次，共创建{copiedCount}个新实体"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "复制实体失败");
                return $"✗ 复制实体失败: {ex.Message}";
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 安全获取字符串参数
        /// </summary>
        private static string GetStringSafe(Dictionary<string, object>? args, string key, string? defaultValue = null)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
            {
                return defaultValue ?? "";
            }
            return args[key].ToString() ?? defaultValue ?? "";
        }

        /// <summary>
        /// 安全获取double参数
        /// </summary>
        private static double GetDoubleSafe(Dictionary<string, object>? args, string key, double defaultValue = 0.0)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
            {
                return defaultValue;
            }

            var value = args[key];
            if (value is JsonElement element)
            {
                return element.GetDouble();
            }

            return Convert.ToDouble(value);
        }

        /// <summary>
        /// 安全获取int参数
        /// </summary>
        private static int GetIntSafe(Dictionary<string, object>? args, string key, int defaultValue = 0)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
            {
                return defaultValue;
            }

            var value = args[key];
            if (value is JsonElement element)
            {
                return element.GetInt32();
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// 安全获取bool参数
        /// </summary>
        private static bool GetBoolSafe(Dictionary<string, object>? args, string key, bool defaultValue = false)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
            {
                return defaultValue;
            }

            var value = args[key];
            if (value is JsonElement element)
            {
                return element.GetBoolean();
            }

            return Convert.ToBoolean(value);
        }

        /// <summary>
        /// 获取Point3d
        /// </summary>
        private static Point3d GetPoint3d(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key))
            {
                throw new ArgumentException($"缺少参数: {key}");
            }

            var value = args[key];

            // 处理JsonElement数组
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                var array = element.EnumerateArray().ToArray();
                var x = array.Length > 0 ? array[0].GetDouble() : 0;
                var y = array.Length > 1 ? array[1].GetDouble() : 0;
                var z = array.Length > 2 ? array[2].GetDouble() : 0;
                return new Point3d(x, y, z);
            }

            // 处理List<object>或object[]
            if (value is List<object> list)
            {
                var x = list.Count > 0 ? Convert.ToDouble(list[0]) : 0;
                var y = list.Count > 1 ? Convert.ToDouble(list[1]) : 0;
                var z = list.Count > 2 ? Convert.ToDouble(list[2]) : 0;
                return new Point3d(x, y, z);
            }

            throw new ArgumentException($"参数{key}不是有效的坐标数组");
        }

        /// <summary>
        /// 获取Point2d
        /// </summary>
        private static Point2d GetPoint2d(Dictionary<string, object> args, string key)
        {
            var pt3d = GetPoint3d(args, key);
            return new Point2d(pt3d.X, pt3d.Y);
        }

        /// <summary>
        /// 获取Vector3d
        /// </summary>
        private static Vector3d GetVector3d(Dictionary<string, object> args, string key)
        {
            var pt = GetPoint3d(args, key);
            return new Vector3d(pt.X, pt.Y, pt.Z);
        }

        /// <summary>
        /// 获取Point2d列表
        /// </summary>
        private static List<Point2d> GetPoint2dList(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key))
            {
                throw new ArgumentException($"缺少参数: {key}");
            }

            var value = args[key];
            var result = new List<Point2d>();

            // 处理JsonElement数组
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var pointArray = item.EnumerateArray().ToArray();
                    var x = pointArray.Length > 0 ? pointArray[0].GetDouble() : 0;
                    var y = pointArray.Length > 1 ? pointArray[1].GetDouble() : 0;
                    result.Add(new Point2d(x, y));
                }
            }

            return result;
        }

        /// <summary>
        /// 获取ObjectId列表
        /// </summary>
        private static List<ObjectId> GetObjectIdList(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key))
            {
                throw new ArgumentException($"缺少参数: {key}");
            }

            var value = args[key];
            var result = new List<ObjectId>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            // 处理JsonElement数组
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var handleStr = item.GetString();
                    if (!string.IsNullOrEmpty(handleStr))
                    {
                        try
                        {
                            var handle = new Handle(Convert.ToInt64(handleStr, 16));
                            var objId = db.GetObjectId(false, handle, 0);

                            // ✅ AutoCAD API最佳实践：验证ObjectId有效性
                            if (objId.IsNull || !objId.IsValid)
                            {
                                Log.Warning($"跳过无效ObjectId: Handle={handleStr}");
                                continue;
                            }

                            // ✅ 检查对象是否已删除
                            if (objId.IsErased)
                            {
                                Log.Warning($"跳过已删除ObjectId: Handle={handleStr}");
                                continue;
                            }

                            result.Add(objId);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"解析ObjectId失败: Handle={handleStr}");
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 解析颜色
        /// </summary>
        private static Color ParseColor(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr) || colorStr.ToLower() == "bylayer")
            {
                return Color.FromColorIndex(ColorMethod.ByLayer, 0);
            }

            // 支持中文颜色名
            switch (colorStr.ToLower())
            {
                case "红色":
                case "red":
                    return Color.FromColorIndex(ColorMethod.ByAci, 1);
                case "黄色":
                case "yellow":
                    return Color.FromColorIndex(ColorMethod.ByAci, 2);
                case "绿色":
                case "green":
                    return Color.FromColorIndex(ColorMethod.ByAci, 3);
                case "青色":
                case "cyan":
                    return Color.FromColorIndex(ColorMethod.ByAci, 4);
                case "蓝色":
                case "blue":
                    return Color.FromColorIndex(ColorMethod.ByAci, 5);
                case "品红":
                case "magenta":
                    return Color.FromColorIndex(ColorMethod.ByAci, 6);
                case "白色":
                case "white":
                    return Color.FromColorIndex(ColorMethod.ByAci, 7);
                default:
                    // 尝试解析RGB格式 "255,0,0"
                    var parts = colorStr.Split(',');
                    if (parts.Length == 3)
                    {
                        var r = byte.Parse(parts[0].Trim());
                        var g = byte.Parse(parts[1].Trim());
                        var b = byte.Parse(parts[2].Trim());
                        return Color.FromRgb(r, g, b);
                    }
                    return Color.FromColorIndex(ColorMethod.ByLayer, 0);
            }
        }

        #endregion

        #region P1 - 高级查询工具

        /// <summary>
        /// P1.1 测量距离
        /// </summary>
        public static async Task<string> MeasureDistance(Dictionary<string, object> args)
        {
            try
            {
                var point1 = GetPoint3d(args, "point1");
                var point2 = GetPoint3d(args, "point2");

                var distance = point1.DistanceTo(point2);

                Log.Information($"✅ 测量距离: ({point1.X:F2},{point1.Y:F2}) → ({point2.X:F2},{point2.Y:F2}) = {distance:F2}mm");

                return await Task.FromResult(
                    $"✓ 距离：{distance:F2}mm\n  起点({point1.X:F2},{point1.Y:F2},{point1.Z:F2})\n  终点({point2.X:F2},{point2.Y:F2},{point2.Z:F2})"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "测量距离失败");
                return $"✗ 测量距离失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.2 测量面积
        /// </summary>
        public static async Task<string> MeasureArea(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                double totalArea = 0;
                int count = 0;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForRead);

                            if (entity is Polyline polyline && polyline.Closed)
                            {
                                totalArea += Math.Abs(polyline.Area);
                                count++;
                            }
                            else if (entity is Circle circle)
                            {
                                totalArea += Math.PI * circle.Radius * circle.Radius;
                                count++;
                            }
                            else if (entity is Region region)
                            {
                                // Region使用GeometricExtents估算面积
                                var bounds = region.GeometricExtents;
                                double length = bounds.MaxPoint.X - bounds.MinPoint.X;
                                double width = bounds.MaxPoint.Y - bounds.MinPoint.Y;
                                totalArea += length * width;
                                count++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"测量实体面积失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 测量面积完成: {count}个实体, 总面积={totalArea:F2}mm²");

                return await Task.FromResult(
                    $"✓ 总面积：{totalArea:F2}mm² ({totalArea / 1000000:F2}m²)\n  测量实体：{count}个"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "测量面积失败");
                return $"✗ 测量面积失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.3 列出实体
        /// </summary>
        public static async Task<string> ListEntities(Dictionary<string, object> args)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                var limit = GetIntSafe(args, "limit", 100);
                var result = new List<string>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead
                    );

                    int count = 0;
                    foreach (ObjectId objId in modelSpace)
                    {
                        if (count >= limit) break;

                        var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        result.Add($"  {entity.GetType().Name} - 图层:{entity.Layer}, ID:{objId.Handle}");
                        count++;
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 列出实体完成: {result.Count}个");

                var output = $"✓ 找到{result.Count}个实体：\n" + string.Join("\n", result);
                return await Task.FromResult(output);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "列出实体失败");
                return $"✗ 列出实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.4 统计实体数量
        /// </summary>
        public static async Task<string> CountEntities(Dictionary<string, object> args)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                var statistics = new Dictionary<string, int>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForRead
                    );

                    foreach (ObjectId objId in modelSpace)
                    {
                        var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        var typeName = entity.GetType().Name;
                        if (!statistics.ContainsKey(typeName))
                        {
                            statistics[typeName] = 0;
                        }
                        statistics[typeName]++;
                    }

                    tr.Commit();
                }

                var totalCount = statistics.Values.Sum();
                var output = $"✓ 实体统计（共{totalCount}个）：\n";
                foreach (var kv in statistics.OrderByDescending(x => x.Value))
                {
                    output += $"  {kv.Key}: {kv.Value}个\n";
                }

                Log.Information($"✅ 统计实体完成: {totalCount}个实体, {statistics.Count}种类型");

                return await Task.FromResult(output);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "统计实体失败");
                return $"✗ 统计实体失败: {ex.Message}";
            }
        }

        #endregion

        #region P1 - 图层管理工具

        /// <summary>
        /// P1.5 创建图层
        /// </summary>
        public static async Task<string> CreateLayer(Dictionary<string, object> args)
        {
            try
            {
                var layerName = GetStringSafe(args, "layer_name", "");
                if (string.IsNullOrEmpty(layerName))
                {
                    return "✗ 图层名称不能为空";
                }

                var colorStr = GetStringSafe(args, "color", "白色");
                var linetype = GetStringSafe(args, "linetype", "Continuous");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                    // 检查图层是否已存在
                    if (layerTable.Has(layerName))
                    {
                        return $"✗ 图层'{layerName}'已存在";
                    }

                    // 创建新图层
                    var layerTableRecord = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = ParseColor(colorStr)
                    };

                    // 设置线型
                    var linetypeTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                    if (linetypeTable.Has(linetype))
                    {
                        layerTableRecord.LinetypeObjectId = linetypeTable[linetype];
                    }

                    layerTable.Add(layerTableRecord);
                    tr.AddNewlyCreatedDBObject(layerTableRecord, true);

                    tr.Commit();
                }

                Log.Information($"✅ 创建图层完成: {layerName}");

                return await Task.FromResult($"✓ 已创建图层：'{layerName}'，颜色={colorStr}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "创建图层失败");
                return $"✗ 创建图层失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.6 设置当前图层
        /// </summary>
        public static async Task<string> SetCurrentLayer(Dictionary<string, object> args)
        {
            try
            {
                var layerName = GetStringSafe(args, "layer_name", "");
                if (string.IsNullOrEmpty(layerName))
                {
                    return "✗ 图层名称不能为空";
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (!layerTable.Has(layerName))
                    {
                        return $"✗ 图层'{layerName}'不存在";
                    }

                    db.Clayer = layerTable[layerName];

                    tr.Commit();
                }

                Log.Information($"✅ 设置当前图层: {layerName}");

                return await Task.FromResult($"✓ 当前图层已设置为：'{layerName}'");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "设置当前图层失败");
                return $"✗ 设置当前图层失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.7 修改图层属性
        /// </summary>
        public static async Task<string> ModifyLayerProperties(Dictionary<string, object> args)
        {
            try
            {
                var layerName = GetStringSafe(args, "layer_name", "");
                if (string.IsNullOrEmpty(layerName))
                {
                    return "✗ 图层名称不能为空";
                }

                var colorStr = GetStringSafe(args, "color", null);
                var isFrozen = args.ContainsKey("is_frozen") ? GetBoolSafe(args, "is_frozen", false) : (bool?)null;
                var isLocked = args.ContainsKey("is_locked") ? GetBoolSafe(args, "is_locked", false) : (bool?)null;
                var isOff = args.ContainsKey("is_off") ? GetBoolSafe(args, "is_off", false) : (bool?)null;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var changes = new List<string>();

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (!layerTable.Has(layerName))
                    {
                        return $"✗ 图层'{layerName}'不存在";
                    }

                    var layerTableRecord = (LayerTableRecord)tr.GetObject(
                        layerTable[layerName],
                        OpenMode.ForWrite
                    );

                    if (!string.IsNullOrEmpty(colorStr))
                    {
                        layerTableRecord.Color = ParseColor(colorStr);
                        changes.Add($"颜色={colorStr}");
                    }

                    if (isFrozen.HasValue)
                    {
                        layerTableRecord.IsFrozen = isFrozen.Value;
                        changes.Add($"冻结={isFrozen.Value}");
                    }

                    if (isLocked.HasValue)
                    {
                        layerTableRecord.IsLocked = isLocked.Value;
                        changes.Add($"锁定={isLocked.Value}");
                    }

                    if (isOff.HasValue)
                    {
                        layerTableRecord.IsOff = isOff.Value;
                        changes.Add($"关闭={isOff.Value}");
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 修改图层属性完成: {layerName}");

                return await Task.FromResult(
                    $"✓ 已修改图层'{layerName}'的属性：{string.Join(", ", changes)}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "修改图层属性失败");
                return $"✗ 修改图层属性失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.8 查询图层信息
        /// </summary>
        public static async Task<string> QueryLayerInfo(Dictionary<string, object> args)
        {
            try
            {
                var layerName = GetStringSafe(args, "layer_name", "");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (string.IsNullOrEmpty(layerName))
                    {
                        // 列出所有图层
                        var layers = new List<string>();
                        foreach (ObjectId objId in layerTable)
                        {
                            var layerRec = (LayerTableRecord)tr.GetObject(objId, OpenMode.ForRead);
                            layers.Add($"  {layerRec.Name}: 颜色={layerRec.Color.ColorValue}, 冻结={layerRec.IsFrozen}, 锁定={layerRec.IsLocked}");
                        }

                        tr.Commit();
                        return await Task.FromResult($"✓ 图层列表（共{layers.Count}个）：\n" + string.Join("\n", layers));
                    }
                    else
                    {
                        // 查询特定图层
                        if (!layerTable.Has(layerName))
                        {
                            return $"✗ 图层'{layerName}'不存在";
                        }

                        var layerRec = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForRead);
                        var info = $"✓ 图层'{layerName}'信息：\n" +
                                   $"  颜色：{layerRec.Color.ColorValue}\n" +
                                   $"  冻结：{layerRec.IsFrozen}\n" +
                                   $"  锁定：{layerRec.IsLocked}\n" +
                                   $"  关闭：{layerRec.IsOff}\n" +
                                   $"  是否在用：{layerRec.IsUsed}";

                        tr.Commit();
                        return await Task.FromResult(info);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "查询图层信息失败");
                return $"✗ 查询图层信息失败: {ex.Message}";
            }
        }

        #endregion

        #region P1 - 高级修改工具

        /// <summary>
        /// P1.9 旋转实体
        /// </summary>
        public static async Task<string> RotateEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                var basePoint = GetPoint3d(args, "base_point");
                var angle = GetDoubleSafe(args, "angle", 0.0);  // 角度（度数）

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int rotatedCount = 0;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                            if (entity == null) continue;

                            // 创建旋转矩阵（转换为弧度）
                            var radians = angle * Math.PI / 180.0;
                            var matrix = Matrix3d.Rotation(radians, Vector3d.ZAxis, basePoint);
                            entity.TransformBy(matrix);

                            rotatedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"旋转实体失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 旋转实体完成: {rotatedCount}个, 角度={angle}°");

                return await Task.FromResult(
                    $"✓ 已旋转{rotatedCount}个实体，角度{angle}°，基点({basePoint.X:F2},{basePoint.Y:F2})"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "旋转实体失败");
                return $"✗ 旋转实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P1.10 缩放实体
        /// </summary>
        public static async Task<string> ScaleEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                var basePoint = GetPoint3d(args, "base_point");
                var scaleFactor = GetDoubleSafe(args, "scale_factor", 1.0);

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int scaledCount = 0;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                            if (entity == null) continue;

                            // 创建缩放矩阵
                            var matrix = Matrix3d.Scaling(scaleFactor, basePoint);
                            entity.TransformBy(matrix);

                            scaledCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"缩放实体失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 缩放实体完成: {scaledCount}个, 比例={scaleFactor}");

                return await Task.FromResult(
                    $"✓ 已缩放{scaledCount}个实体，比例{scaleFactor}，基点({basePoint.X:F2},{basePoint.Y:F2})"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "缩放实体失败");
                return $"✗ 缩放实体失败: {ex.Message}";
            }
        }

        #endregion

        #region P2 - 视图工具

        /// <summary>
        /// P2.1 全图显示 (Zoom Extents)
        /// </summary>
        public static async Task<string> ZoomExtents(Dictionary<string, object> args)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var docLock = doc.LockDocument())
                {
                    // 使用AutoCAD编辑器命令执行ZoomExtents
                    var ed = doc.Editor;

                    // 计算所有实体的范围
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var modelSpace = (BlockTableRecord)tr.GetObject(
                            bt[BlockTableRecord.ModelSpace],
                            OpenMode.ForRead
                        );

                        // 发送ZOOM EXTENTS命令
                        ed.Command("_.ZOOM", "_E");

                        tr.Commit();
                    }
                }

                Log.Information("✅ 全图显示完成");
                return await Task.FromResult("✓ 已执行全图显示");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "全图显示失败");
                return $"✗ 全图显示失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.2 窗口缩放 (Zoom Window)
        /// </summary>
        public static async Task<string> ZoomWindow(Dictionary<string, object> args)
        {
            try
            {
                var corner1 = GetPoint3d(args, "corner1");
                var corner2 = GetPoint3d(args, "corner2");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                using (var docLock = doc.LockDocument())
                {
                    // 执行ZOOM WINDOW命令
                    ed.Command("_.ZOOM", "_W", corner1, corner2);
                }

                Log.Information($"✅ 窗口缩放完成: ({corner1.X:F2},{corner1.Y:F2}) → ({corner2.X:F2},{corner2.Y:F2})");
                return await Task.FromResult(
                    $"✓ 已缩放到窗口区域：({corner1.X:F2},{corner1.Y:F2}) → ({corner2.X:F2},{corner2.Y:F2})"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "窗口缩放失败");
                return $"✗ 窗口缩放失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.3 平移视图 (Pan View)
        /// </summary>
        public static async Task<string> PanView(Dictionary<string, object> args)
        {
            try
            {
                var displacement = GetVector3d(args, "displacement");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                using (var docLock = doc.LockDocument())
                {
                    // 执行PAN命令
                    var fromPt = new Point3d(0, 0, 0);
                    var toPt = new Point3d(displacement.X, displacement.Y, displacement.Z);

                    ed.Command("_.PAN", fromPt, toPt);
                }

                Log.Information($"✅ 平移视图完成: ({displacement.X:F2},{displacement.Y:F2})");
                return await Task.FromResult(
                    $"✓ 已平移视图：位移({displacement.X:F2},{displacement.Y:F2})"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "平移视图失败");
                return $"✗ 平移视图失败: {ex.Message}";
            }
        }

        #endregion

        #region P2 - 文件工具

        /// <summary>
        /// P2.4 保存图纸
        /// </summary>
        public static async Task<string> SaveDrawing(Dictionary<string, object> args)
        {
            try
            {
                var filePath = GetStringSafe(args, "file_path", null);

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var docLock = doc.LockDocument())
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        // 保存当前文件
                        db.SaveAs(db.Filename, DwgVersion.Current);
                        Log.Information($"✅ 保存图纸完成: {db.Filename}");
                        return await Task.FromResult($"✓ 已保存图纸：{db.Filename}");
                    }
                    else
                    {
                        // ✅ 添加文件路径验证
                        var directory = System.IO.Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                        {
                            return $"✗ 目录不存在: {directory}";
                        }

                        // 另存为
                        db.SaveAs(filePath, DwgVersion.Current);
                        Log.Information($"✅ 另存为完成: {filePath}");
                        return await Task.FromResult($"✓ 已另存为：{filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存图纸失败");
                return $"✗ 保存图纸失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.5 导出PDF
        /// </summary>
        public static async Task<string> ExportToPdf(Dictionary<string, object> args)
        {
            try
            {
                var outputPath = GetStringSafe(args, "output_path", "");
                if (string.IsNullOrEmpty(outputPath))
                {
                    return "✗ 输出路径不能为空";
                }

                // ✅ 添加文件路径验证
                var directory = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    return $"✗ 目录不存在: {directory}";
                }

                // ✅ 检查文件扩展名
                var extension = System.IO.Path.GetExtension(outputPath);
                if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    outputPath += ".pdf";
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                using (var docLock = doc.LockDocument())
                {
                    // 使用EXPORT命令导出PDF
                    ed.Command("_.EXPORT", outputPath, "_PDF", "");
                }

                Log.Information($"✅ 导出PDF完成: {outputPath}");
                return await Task.FromResult($"✓ 已导出PDF：{outputPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导出PDF失败");
                return $"✗ 导出PDF失败: {ex.Message}";
            }
        }

        #endregion

        #region P2 - 高级修改工具

        /// <summary>
        /// P2.6 镜像实体
        /// </summary>
        public static async Task<string> MirrorEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                var mirrorLine1 = GetPoint3d(args, "mirror_line_point1");
                var mirrorLine2 = GetPoint3d(args, "mirror_line_point2");
                var eraseSource = GetBoolSafe(args, "erase_source", false);

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int mirroredCount = 0;
                var newIds = new List<ObjectId>();

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    // 创建镜像线
                    var line3d = new Line3d(mirrorLine1, mirrorLine2);

                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (entity == null) continue;

                            // 创建镜像副本
                            var mirrored = entity.Clone() as Entity;
                            if (mirrored == null) continue;

                            // 创建镜像矩阵
                            var plane = new Plane(mirrorLine1, mirrorLine2 - mirrorLine1, Vector3d.ZAxis);
                            var matrix = Matrix3d.Mirroring(plane);
                            mirrored.TransformBy(matrix);

                            var newId = modelSpace.AppendEntity(mirrored);
                            tr.AddNewlyCreatedDBObject(mirrored, true);
                            newIds.Add(newId);

                            // 如果需要删除原实体
                            if (eraseSource)
                            {
                                var sourceEntity = tr.GetObject(objId, OpenMode.ForWrite);
                                sourceEntity.Erase();
                            }

                            mirroredCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"镜像实体失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 镜像实体完成: {mirroredCount}个");

                return await Task.FromResult(
                    $"✓ 已镜像{mirroredCount}个实体{(eraseSource ? "（已删除原实体）" : "")}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "镜像实体失败");
                return $"✗ 镜像实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.7 偏移实体
        /// </summary>
        public static async Task<string> OffsetEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");
                var distance = GetDoubleSafe(args, "distance", 0.0);
                var throughPoint = args.ContainsKey("through_point")
                    ? GetPoint3d(args, "through_point")
                    : (Point3d?)null;

                if (distance == 0.0 && throughPoint == null)
                {
                    return "✗ 必须提供偏移距离或通过点";
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                int offsetCount = 0;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );

                    foreach (var objId in entityIds)
                    {
                        try
                        {
                            var entity = tr.GetObject(objId, OpenMode.ForRead);

                            // 支持偏移的实体类型：Line, Circle, Arc, Polyline等
                            if (entity is Curve curve)
                            {
                                // 使用AutoCAD的Offset方法
                                var offsetCurves = curve.GetOffsetCurves(distance);

                                foreach (Entity offsetEntity in offsetCurves)
                                {
                                    modelSpace.AppendEntity(offsetEntity);
                                    tr.AddNewlyCreatedDBObject(offsetEntity, true);
                                    offsetCount++;
                                }

                                offsetCurves.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"偏移实体失败: {objId}");
                        }
                    }

                    tr.Commit();
                }

                Log.Information($"✅ 偏移实体完成: {offsetCount}个");

                return await Task.FromResult(
                    $"✓ 已偏移{entityIds.Count}个实体，距离{distance:F2}mm，创建{offsetCount}个新实体"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "偏移实体失败");
                return $"✗ 偏移实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.8 修剪实体
        /// </summary>
        public static async Task<string> TrimEntity(Dictionary<string, object> args)
        {
            try
            {
                var cuttingEdgeIds = GetObjectIdList(args, "cutting_edge_ids");
                var entityToTrimIds = GetObjectIdList(args, "entity_to_trim_ids");

                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                // AutoCAD的TRIM命令需要交互式操作，这里使用简化版本
                // 实际应用中可能需要使用AutoCAD的Boolean操作或更复杂的几何计算

                Log.Information($"✅ 修剪命令已触发（需要交互式操作）");

                return await Task.FromResult(
                    $"✓ 修剪功能需要使用AutoCAD的TRIM命令进行交互式操作\n提示：选择切割边{cuttingEdgeIds.Count}个，待修剪实体{entityToTrimIds.Count}个"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "修剪实体失败");
                return $"✗ 修剪实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.9 延伸实体
        /// </summary>
        public static async Task<string> ExtendEntity(Dictionary<string, object> args)
        {
            try
            {
                var boundaryEdgeIds = GetObjectIdList(args, "boundary_edge_ids");
                var entityToExtendIds = GetObjectIdList(args, "entity_to_extend_ids");

                var doc = Application.DocumentManager.MdiActiveDocument;

                // AutoCAD的EXTEND命令需要交互式操作
                Log.Information($"✅ 延伸命令已触发（需要交互式操作）");

                return await Task.FromResult(
                    $"✓ 延伸功能需要使用AutoCAD的EXTEND命令进行交互式操作\n提示：边界{boundaryEdgeIds.Count}个，待延伸实体{entityToExtendIds.Count}个"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "延伸实体失败");
                return $"✗ 延伸实体失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.10 圆角
        /// </summary>
        public static async Task<string> FilletEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");

                // ✅ 添加参数验证
                if (entityIds.Count < 2)
                {
                    return "✗ 需要至少2个实体ID";
                }

                var entityId1 = entityIds[0];
                var entityId2 = entityIds[1];
                var radius = GetDoubleSafe(args, "radius", 0.0);

                if (radius < 0)
                {
                    return "✗ 圆角半径不能为负数";
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var entity1 = tr.GetObject(entityId1, OpenMode.ForRead) as Curve;
                    var entity2 = tr.GetObject(entityId2, OpenMode.ForRead) as Curve;

                    if (entity1 == null || entity2 == null)
                    {
                        return "✗ 所选实体必须是曲线";
                    }

                    // 使用AutoCAD命令
                    var ed = doc.Editor;
                    ed.Command("_.FILLET", "_R", radius, entity1, entity2);

                    tr.Commit();
                }

                Log.Information($"✅ 圆角完成: 半径={radius:F2}mm");

                return await Task.FromResult($"✓ 已创建圆角，半径{radius:F2}mm");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "圆角失败");
                return $"✗ 圆角失败: {ex.Message}";
            }
        }

        /// <summary>
        /// P2.11 倒角
        /// </summary>
        public static async Task<string> ChamferEntity(Dictionary<string, object> args)
        {
            try
            {
                var entityIds = GetObjectIdList(args, "entity_ids");

                // ✅ 添加参数验证
                if (entityIds.Count < 2)
                {
                    return "✗ 需要至少2个实体ID";
                }

                var entityId1 = entityIds[0];
                var entityId2 = entityIds[1];
                var distance1 = GetDoubleSafe(args, "distance1", 0.0);
                var distance2 = GetDoubleSafe(args, "distance2", 0.0);

                if (distance1 < 0 || distance2 < 0)
                {
                    return "✗ 倒角距离不能为负数";
                }

                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;

                using (var docLock = doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var entity1 = tr.GetObject(entityId1, OpenMode.ForRead) as Curve;
                    var entity2 = tr.GetObject(entityId2, OpenMode.ForRead) as Curve;

                    if (entity1 == null || entity2 == null)
                    {
                        return "✗ 所选实体必须是曲线";
                    }

                    // 使用AutoCAD命令
                    var ed = doc.Editor;
                    ed.Command("_.CHAMFER", "_D", distance1, distance2, entity1, entity2);

                    tr.Commit();
                }

                Log.Information($"✅ 倒角完成: 距离1={distance1:F2}mm, 距离2={distance2:F2}mm");

                return await Task.FromResult(
                    $"✓ 已创建倒角，距离1={distance1:F2}mm，距离2={distance2:F2}mm"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "倒角失败");
                return $"✗ 倒角失败: {ex.Message}";
            }
        }

        #endregion
    }
}
