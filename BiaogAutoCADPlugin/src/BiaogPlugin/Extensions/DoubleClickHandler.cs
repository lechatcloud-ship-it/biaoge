using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;
using BiaogPlugin.Services;
using BiaogPlugin.UI;

namespace BiaogPlugin.Extensions
{
    /// <summary>
    /// 双击事件处理器
    /// 监听双击文本实体事件，触发快速翻译弹窗
    /// </summary>
    public static class DoubleClickHandler
    {
        private static DocumentCollection? _docs;
        private static bool _isEnabled = false;
        private static DateTime _lastClickTime = DateTime.MinValue;
        private static ObjectId _lastClickedObjectId = ObjectId.Null;
        private static readonly object _clickLock = new object(); // 线程安全保护
        private const int DoubleClickInterval = 500; // 毫秒

        /// <summary>
        /// 启用双击翻译功能
        /// </summary>
        public static void Enable()
        {
            if (_isEnabled)
            {
                Log.Warning("双击翻译已启用，跳过重复启用");
                return;
            }

            try
            {
                _docs = Application.DocumentManager;
                _docs.DocumentActivated += OnDocumentActivated;

                // 为当前活动文档注册事件
                var doc = _docs.MdiActiveDocument;
                if (doc != null)
                {
                    RegisterDocumentEvents(doc);
                }

                _isEnabled = true;
                Log.Information("双击翻译功能已启用");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "启用双击翻译失败");
                throw;
            }
        }

        /// <summary>
        /// 禁用双击翻译功能
        /// </summary>
        public static void Disable()
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                if (_docs != null)
                {
                    _docs.DocumentActivated -= OnDocumentActivated;

                    // 为所有文档注销事件
                    foreach (Document doc in _docs)
                    {
                        UnregisterDocumentEvents(doc);
                    }
                }

                _isEnabled = false;
                Log.Information("双击翻译功能已禁用");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "禁用双击翻译失败");
            }
        }

        /// <summary>
        /// 文档激活事件
        /// </summary>
        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            try
            {
                if (e.Document != null)
                {
                    RegisterDocumentEvents(e.Document);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "注册文档事件失败");
            }
        }

        /// <summary>
        /// 注册文档事件
        /// </summary>
        private static void RegisterDocumentEvents(Document doc)
        {
            try
            {
                // 注销旧事件（避免重复）
                doc.ImpliedSelectionChanged -= OnImpliedSelectionChanged;

                // 注册新事件
                doc.ImpliedSelectionChanged += OnImpliedSelectionChanged;

                Log.Debug($"已为文档 {doc.Name} 注册双击事件");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, $"注册文档 {doc.Name} 事件失败");
            }
        }

        /// <summary>
        /// 注销文档事件
        /// </summary>
        private static void UnregisterDocumentEvents(Document doc)
        {
            try
            {
                doc.ImpliedSelectionChanged -= OnImpliedSelectionChanged;
                Log.Debug($"已为文档 {doc.Name} 注销双击事件");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, $"注销文档 {doc.Name} 事件失败");
            }
        }

        /// <summary>
        /// 选择改变事件 - 检测双击
        /// </summary>
        private static void OnImpliedSelectionChanged(object sender, EventArgs e)
        {
            try
            {
                // 检查是否启用双击翻译
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager == null || !configManager.Config.Translation.EnableDoubleClickTranslation)
                {
                    return;
                }

                var doc = sender as Document;
                if (doc == null) return;

                var db = doc.Database;
                var ed = doc.Editor;

                // 获取当前选择
                var selection = ed.SelectImplied();
                if (selection.Status != PromptStatus.OK)
                {
                    return;
                }

                var objIds = selection.Value.GetObjectIds();
                if (objIds.Length != 1)
                {
                    return; // 只处理单个对象
                }

                var currentObjectId = objIds[0];

                // ✅ 线程安全：使用lock保护静态字段读写，防止多文档并发竞态条件
                bool isDoubleClick = false;
                lock (_clickLock)
                {
                    // 检测双击（两次点击同一对象，时间间隔小于阈值）
                    var now = DateTime.Now;
                    var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;

                    if (currentObjectId == _lastClickedObjectId && timeSinceLastClick < DoubleClickInterval)
                    {
                        // 检测到双击
                        isDoubleClick = true;

                        // 重置状态
                        _lastClickTime = DateTime.MinValue;
                        _lastClickedObjectId = ObjectId.Null;
                    }
                    else
                    {
                        // 记录单击
                        _lastClickTime = now;
                        _lastClickedObjectId = currentObjectId;
                    }
                }

                // ✅ 在锁外处理双击，避免长时间持锁导致性能问题
                if (isDoubleClick)
                {
                    Log.Debug($"检测到双击文本实体: {currentObjectId}");
                    HandleDoubleClick(doc, currentObjectId);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "处理选择改变事件失败");
            }
        }

        /// <summary>
        /// 处理双击事件
        /// </summary>
        private static void HandleDoubleClick(Document doc, ObjectId objId)
        {
            try
            {
                var db = doc.Database;
                var ed = doc.Editor;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                    if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                    {
                        Log.Debug("双击的ObjectId无效或已删除");
                        return;
                    }

                    var obj = tr.GetObject(objId, OpenMode.ForRead);
                    string? textContent = null;

                    // 提取文本内容
                    if (obj is DBText dbText)
                    {
                        textContent = dbText.TextString;
                    }
                    else if (obj is MText mText)
                    {
                        textContent = mText.Text;
                    }
                    else if (obj is AttributeReference attRef)
                    {
                        textContent = attRef.TextString;
                    }

                    tr.Commit();

                    if (string.IsNullOrWhiteSpace(textContent))
                    {
                        Log.Debug("双击的文本实体内容为空");
                        return;
                    }

                    // 显示快速翻译弹窗
                    ShowQuickTranslatePopup(objId, textContent);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "处理双击失败");
                doc.Editor.WriteMessage($"\n[错误] 快速翻译失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示快速翻译弹窗
        /// </summary>
        private static void ShowQuickTranslatePopup(ObjectId textObjectId, string originalText)
        {
            try
            {
                Log.Information($"显示快速翻译弹窗: {originalText.Substring(0, Math.Min(30, originalText.Length))}...");

                // 在UI线程上创建和显示窗口
                var popup = new QuickTranslatePopup(textObjectId, originalText);
                popup.Show();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "显示快速翻译弹窗失败");
            }
        }

        /// <summary>
        /// 检查是否启用
        /// </summary>
        public static bool IsEnabled => _isEnabled;
    }
}
