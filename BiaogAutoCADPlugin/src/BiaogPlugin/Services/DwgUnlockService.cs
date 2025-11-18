using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 图纸解锁服务 - 翻译前自动解锁所有图层、对象、XRef
    ///
    /// 核心功能：
    /// 1. 解锁所有图层（Layers）
    /// 2. 解锁所有对象（Entities）
    /// 3. XRef绑定（将外部引用转为本地块）
    /// 4. 翻译完成后可选恢复锁定状态
    ///
    /// 用户需求："如果锁定就解锁整个图纸后再翻译"
    /// </summary>
    public class DwgUnlockService
    {
        /// <summary>
        /// 解锁状态记录 - 用于翻译后恢复
        /// </summary>
        public class UnlockRecord
        {
            public List<ObjectId> UnlockedLayers { get; set; } = new List<ObjectId>();
            public List<ObjectId> UnlockedObjects { get; set; } = new List<ObjectId>();
            public List<string> BoundXRefs { get; set; } = new List<string>();
        }

        /// <summary>
        /// ✅ 核心方法：翻译前解锁整个图纸
        ///
        /// 解锁顺序：
        /// 1. 解锁所有图层（包括XRef图层）
        /// 2. 解锁所有被锁定的对象
        /// 3. 可选：绑定XRef为本地块
        ///
        /// 返回：解锁记录（用于恢复）
        /// </summary>
        public static UnlockRecord UnlockDrawingForTranslation(bool bindXRefs = false)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var record = new UnlockRecord();

            using (var docLock = doc.LockDocument())
            {
                // ✅ 修复：第一个事务 - 解锁图层和对象
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 步骤1: 解锁所有图层
                        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        foreach (ObjectId layerId in layerTable)
                        {
                            var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                            if (layer.IsLocked)
                            {
                                layer.UpgradeOpen();
                                layer.IsLocked = false;
                                record.UnlockedLayers.Add(layerId);
                                Log.Information($"解锁图层: {layer.Name}");
                            }
                        }

                        // 步骤2: 解锁所有被锁定的对象（遍历所有空间）
                        UnlockObjectsInSpace(db.ModelSpace, tr, record);
                        UnlockObjectsInSpace(db.PaperSpace, tr, record);

                        tr.Commit();
                        Log.Information($"✅ 图层和对象解锁完成: {record.UnlockedLayers.Count}个图层, {record.UnlockedObjects.Count}个对象");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "解锁图层和对象失败");
                        tr.Abort();
                        throw;
                    }
                }

                // ✅ 修复：第二个事务 - XRef绑定（独立管理）
                if (bindXRefs)
                {
                    BindAllXRefsIndependent(db, record, ed);
                }
            }

            Log.Information($"✅ 图纸解锁完成: {record.UnlockedLayers.Count}个图层, {record.UnlockedObjects.Count}个对象");

            if (bindXRefs && record.BoundXRefs.Count > 0)
            {
                ed.WriteMessage($"\n已绑定 {record.BoundXRefs.Count} 个外部引用: {string.Join(", ", record.BoundXRefs)}");
            }

            return record;
        }

        /// <summary>
        /// 解锁指定空间中的所有对象
        /// </summary>
        private static void UnlockObjectsInSpace(ObjectId spaceId, Transaction tr, UnlockRecord record)
        {
            try
            {
                var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
                foreach (ObjectId objId in space)
                {
                    try
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent != null && ent.IsWriteEnabled == false)
                        {
                            // 检测对象是否被锁定（通过尝试升级打开）
                            try
                            {
                                ent.UpgradeOpen();
                                // 如果成功升级，说明对象可写，记录解锁
                                record.UnlockedObjects.Add(objId);
                                ent.DowngradeOpen();
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception)
                            {
                                // 对象真的被锁定，无法解锁（如XRef对象）
                                Log.Debug($"对象 {objId.Handle} 无法解锁（可能是XRef）");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"检查对象锁定状态失败: {objId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"解锁空间失败: {spaceId}");
            }
        }

        /// <summary>
        /// ✅ XRef绑定 - 将外部引用转为本地块（关键功能！）
        ///
        /// 用户反馈："外部块要全面支持，外部文本也要全面支持"
        /// 解决方案：将XRef绑定为本地块后，文本就可以编辑和翻译了
        ///
        /// 绑定类型：
        /// - Bind: 绑定为独立块（推荐）
        /// - Insert: 插入并合并到当前图纸
        ///
        /// ✅ 修复：独立管理事务，不接收外部Transaction参数
        /// </summary>
        private static void BindAllXRefsIndependent(Database db, UnlockRecord record, Editor ed)
        {
            try
            {
                var xrefsToProcess = new List<ObjectId>();

                // ✅ 第一个事务：收集所有XRef块
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    foreach (ObjectId btrId in blockTable)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (btr.IsFromExternalReference)
                        {
                            xrefsToProcess.Add(btrId);
                            Log.Information($"检测到外部引用: {btr.Name}");
                        }
                    }

                    tr.Commit();
                }

                if (xrefsToProcess.Count == 0)
                {
                    Log.Information("未检测到外部引用，跳过绑定");
                    return;
                }

                // ✅ 第二个事务：绑定XRef
                using (var bindTr = db.TransactionManager.StartTransaction())
                {
                    foreach (var xrefId in xrefsToProcess)
                    {
                        try
                        {
                            var btr = (BlockTableRecord)bindTr.GetObject(xrefId, OpenMode.ForRead);
                            var xrefName = btr.Name;

                            // ✅ 使用Bind方法（而非Insert）
                            // Bind: 保留XRef块名称层次结构（推荐）
                            // Insert: 合并到当前图纸（会丢失XRef标识）
                            db.BindXrefs(new ObjectIdCollection(new[] { xrefId }), true); // true = Bind (false = Insert)

                            record.BoundXRefs.Add(xrefName);
                            Log.Information($"✅ 已绑定外部引用: {xrefName}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"绑定XRef失败: {xrefId}");
                        }
                    }
                    bindTr.Commit();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "XRef绑定过程失败");
            }
        }

        /// <summary>
        /// ✅ 翻译后恢复锁定状态（可选）
        ///
        /// 注意：XRef绑定操作不可逆，已绑定的XRef不会恢复
        /// </summary>
        public static void RestoreLockState(UnlockRecord record)
        {
            if (record == null) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var docLock = doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 恢复图层锁定
                    foreach (var layerId in record.UnlockedLayers)
                    {
                        if (layerId.IsValid && !layerId.IsErased)
                        {
                            try
                            {
                                var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                                layer.IsLocked = true;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, $"恢复图层锁定失败: {layerId}");
                            }
                        }
                    }

                    // 注意：对象锁定状态通常由图层控制，不需要单独恢复

                    tr.Commit();
                    Log.Information($"✅ 锁定状态恢复完成: {record.UnlockedLayers.Count}个图层");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "恢复锁定状态失败");
                    tr.Abort();
                }
            }
        }

        /// <summary>
        /// 检查图纸是否有锁定的内容
        /// </summary>
        public static (int lockedLayers, int xrefCount) CheckLockStatus()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            int lockedLayers = 0;
            int xrefCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 检查图层
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layerId in layerTable)
                {
                    var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                    if (layer.IsLocked) lockedLayers++;
                }

                // 检查XRef
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId in blockTable)
                {
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    if (btr.IsFromExternalReference) xrefCount++;
                }

                tr.Commit();
            }

            return (lockedLayers, xrefCount);
        }
    }
}
