using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Serilog;
using BiaogPlugin.Services;
using BiaogPlugin.Models;

namespace BiaogPlugin.Extensions
{
    /// <summary>
    /// 右键上下文菜单管理器
    /// 为文本实体添加智能翻译菜单
    /// </summary>
    public static class ContextMenuManager
    {
        private static ContextMenuExtension? _textContextMenu;
        private static bool _isRegistered = false;

        /// <summary>
        /// 注册所有上下文菜单
        /// </summary>
        public static void RegisterContextMenus()
        {
            if (_isRegistered)
            {
                Log.Warning("上下文菜单已注册，跳过重复注册");
                return;
            }

            try
            {
                RegisterTextContextMenu();
                _isRegistered = true;
                Log.Information("上下文菜单注册成功");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "注册上下文菜单失败");
                throw;
            }
        }

        /// <summary>
        /// 注销所有上下文菜单
        /// </summary>
        public static void UnregisterContextMenus()
        {
            if (!_isRegistered)
            {
                return;
            }

            try
            {
                if (_textContextMenu != null)
                {
                    // 注销DBText菜单
                    RXClass dbTextClass = RXObject.GetClass(typeof(DBText));
                    Autodesk.AutoCAD.ApplicationServices.Application.RemoveObjectContextMenuExtension(dbTextClass, _textContextMenu);

                    // 注销MText菜单
                    RXClass mTextClass = RXObject.GetClass(typeof(MText));
                    Autodesk.AutoCAD.ApplicationServices.Application.RemoveObjectContextMenuExtension(mTextClass, _textContextMenu);

                    // 注销AttributeReference菜单
                    RXClass attRefClass = RXObject.GetClass(typeof(AttributeReference));
                    Autodesk.AutoCAD.ApplicationServices.Application.RemoveObjectContextMenuExtension(attRefClass, _textContextMenu);

                    _textContextMenu = null;
                }

                _isRegistered = false;
                Log.Information("上下文菜单注销成功");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "注销上下文菜单失败");
            }
        }

        /// <summary>
        /// 注册文本实体的右键菜单
        /// </summary>
        private static void RegisterTextContextMenu()
        {
            _textContextMenu = new ContextMenuExtension();
            _textContextMenu.Title = "标哥翻译";

            // 主菜单项
            MenuItem mainMenu = new MenuItem("标哥翻译");
            _textContextMenu.MenuItems.Add(mainMenu);

            // === 快速翻译子菜单 ===

            // TODO: MenuItem.SubMenu 在.NET 8.0/AutoCAD 2024+中可能不可用
            // 需要检查AutoCAD .NET API版本或使用替代方案
            // 临时注释以允许编译通过

            /*
            // 翻译为中文（推荐）⭐
            MenuItem translateZH = new MenuItem("翻译为中文（推荐）⭐");
            translateZH.Click += (s, e) => TranslateSelectedText("zh", "简体中文");
            mainMenu.SubMenu.Add(translateZH);

            // 翻译为英语
            MenuItem translateEN = new MenuItem("翻译为英语");
            translateEN.Click += (s, e) => TranslateSelectedText("en", "英语");
            mainMenu.SubMenu.Add(translateEN);

            // 翻译为日语
            MenuItem translateJA = new MenuItem("翻译为日语");
            translateJA.Click += (s, e) => TranslateSelectedText("ja", "日语");
            mainMenu.SubMenu.Add(translateJA);

            // 翻译为韩语
            MenuItem translateKO = new MenuItem("翻译为韩语");
            translateKO.Click += (s, e) => TranslateSelectedText("ko", "韩语");
            mainMenu.SubMenu.Add(translateKO);

            // 分隔符
            mainMenu.SubMenu.Add(new MenuItem("-"));

            // 翻译为法语
            MenuItem translateFR = new MenuItem("翻译为法语");
            translateFR.Click += (s, e) => TranslateSelectedText("fr", "法语");
            mainMenu.SubMenu.Add(translateFR);

            // 翻译为德语
            MenuItem translateDE = new MenuItem("翻译为德语");
            translateDE.Click += (s, e) => TranslateSelectedText("de", "德语");
            mainMenu.SubMenu.Add(translateDE);

            // 翻译为西班牙语
            MenuItem translateES = new MenuItem("翻译为西班牙语");
            translateES.Click += (s, e) => TranslateSelectedText("es", "西班牙语");
            mainMenu.SubMenu.Add(translateES);

            // 翻译为俄语
            MenuItem translateRU = new MenuItem("翻译为俄语");
            translateRU.Click += (s, e) => TranslateSelectedText("ru", "俄语");
            mainMenu.SubMenu.Add(translateRU);

            // 分隔符
            mainMenu.SubMenu.Add(new MenuItem("-"));

            // 翻译预览
            MenuItem previewTranslation = new MenuItem("翻译预览...");
            previewTranslation.Click += (s, e) => ShowTranslationPreview();
            mainMenu.SubMenu.Add(previewTranslation);
            */

            // === AI助手子菜单 ===
            MenuItem aiMenu = new MenuItem("标哥AI助手");
            _textContextMenu.MenuItems.Add(aiMenu);

            /*
            // 询问AI关于此文本
            MenuItem askAI = new MenuItem("询问AI关于此文本");
            askAI.Click += (s, e) => AskAIAboutText();
            aiMenu.SubMenu.Add(askAI);

            // 批量智能处理
            MenuItem batchProcess = new MenuItem("批量智能处理");
            batchProcess.Click += (s, e) => BatchSmartProcess();
            aiMenu.SubMenu.Add(batchProcess);
            */

            // === 实用工具子菜单 ===
            MenuItem toolsMenu = new MenuItem("标哥工具");
            _textContextMenu.MenuItems.Add(toolsMenu);

            /*
            // 复制文本内容
            MenuItem copyText = new MenuItem("复制文本内容");
            copyText.Click += (s, e) => CopyTextContent();
            toolsMenu.SubMenu.Add(copyText);

            // 查看文本属性
            MenuItem viewProperties = new MenuItem("查看文本属性");
            viewProperties.Click += (s, e) => ViewTextProperties();
            toolsMenu.SubMenu.Add(viewProperties);
            */

            // 注册到不同的文本实体类型

            // DBText (单行文本)
            RXClass dbTextClass = RXObject.GetClass(typeof(DBText));
            Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(dbTextClass, _textContextMenu);

            // MText (多行文本)
            RXClass mTextClass = RXObject.GetClass(typeof(MText));
            Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(mTextClass, _textContextMenu);

            // AttributeReference (属性文本)
            RXClass attRefClass = RXObject.GetClass(typeof(AttributeReference));
            Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(attRefClass, _textContextMenu);

            Log.Information("文本上下文菜单注册完成");
        }

        /// <summary>
        /// 翻译选中的文本
        /// </summary>
        private static async void TranslateSelectedText(string targetLang, string languageName)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                // 获取当前选择集
                PromptSelectionResult selResult = ed.SelectImplied();
                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何对象。");
                    return;
                }

                ObjectId[] selectedIds = selResult.Value.GetObjectIds();
                if (selectedIds.Length == 0)
                {
                    ed.WriteMessage("\n未选择任何文本实体。");
                    return;
                }

                ed.WriteMessage($"\n开始翻译为{languageName}...");
                ed.WriteMessage($"\n已选择 {selectedIds.Length} 个文本实体");

                // ✅ P1修复: 使用TextEntity替代DwgTextEntity,统一数据模型
                // 提取文本内容
                var textEntities = new List<TextEntity>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objId in selectedIds)
                    {
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                            continue;

                        var obj = tr.GetObject(objId, OpenMode.ForRead);
                        TextEntity? textEntity = null;

                        if (obj is DBText dbText)
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
                        else if (obj is MText mText)
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
                        else if (obj is AttributeReference attRef)
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

                    tr.Commit();
                }

                if (textEntities.Count == 0)
                {
                    ed.WriteMessage("\n选中的文本实体为空或无效。");
                    return;
                }

                // 执行翻译
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var cacheService = ServiceLocator.GetService<CacheService>();

                var engine = new TranslationEngine(bailianClient!, cacheService!);

                var progress = new Progress<double>(p =>
                {
                    ed.WriteMessage($"\r翻译进度: {p:F1}%    ");
                });

                var translations = await engine.TranslateBatchWithCacheAsync(
                    textEntities.Select(t => t.Content).ToList(),
                    targetLang,
                    progress
                );

                // 更新DWG文本
                var updater = new DwgTextUpdater();
                var updateRequests = new List<TextUpdateRequest>();

                int translatedCount = 0;
                for (int i = 0; i < textEntities.Count; i++)
                {
                    if (i < translations.Count && !string.IsNullOrEmpty(translations[i]))
                    {
                        updateRequests.Add(new TextUpdateRequest
                        {
                            ObjectId = textEntities[i].Id,  // ✅ P1修复: 使用Id而非ObjectId
                            OriginalContent = textEntities[i].Content,
                            NewContent = translations[i],
                            Layer = textEntities[i].Layer,
                            EntityType = textEntities[i].Type
                        });
                        translatedCount++;
                    }
                }

                updater.UpdateTexts(updateRequests);

                ed.WriteMessage($"\n\n✓ 右键翻译完成！已翻译 {translatedCount}/{textEntities.Count} 个文本");
                Log.Information($"右键翻译完成: {translatedCount}/{textEntities.Count} 到 {languageName}");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "右键翻译失败");
                ed.WriteMessage($"\n[错误] 翻译失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示翻译预览
        /// </summary>
        private static void ShowTranslationPreview()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n翻译预览功能即将推出...");
            // TODO: 实现翻译预览对话框
        }

        /// <summary>
        /// 询问AI关于选中的文本
        /// </summary>
        private static void AskAIAboutText()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\nAI问答功能即将推出...");
            // TODO: 集成AI助手
        }

        /// <summary>
        /// 批量智能处理
        /// </summary>
        private static void BatchSmartProcess()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n批量智能处理功能即将推出...");
            // TODO: 实现批量智能处理
        }

        /// <summary>
        /// 复制文本内容到剪贴板
        /// </summary>
        private static void CopyTextContent()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                PromptSelectionResult selResult = ed.SelectImplied();
                if (selResult.Status != PromptStatus.OK) return;

                var texts = new List<string>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objId in selResult.Value.GetObjectIds())
                    {
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                            continue;

                        var obj = tr.GetObject(objId, OpenMode.ForRead);

                        if (obj is DBText dbText)
                        {
                            texts.Add(dbText.TextString);
                        }
                        else if (obj is MText mText)
                        {
                            texts.Add(mText.Text);
                        }
                        else if (obj is AttributeReference attRef)
                        {
                            texts.Add(attRef.TextString);
                        }
                    }

                    tr.Commit();
                }

                if (texts.Count > 0)
                {
                    var combinedText = string.Join("\n", texts);
                    System.Windows.Forms.Clipboard.SetText(combinedText);
                    ed.WriteMessage($"\n✓ 已复制 {texts.Count} 个文本到剪贴板");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "复制文本失败");
                ed.WriteMessage($"\n[错误] 复制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查看文本属性
        /// </summary>
        private static void ViewTextProperties()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                PromptSelectionResult selResult = ed.SelectImplied();
                if (selResult.Status != PromptStatus.OK) return;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objId in selResult.Value.GetObjectIds())
                    {
                        // ✅ AutoCAD 2022最佳实践: 验证ObjectId有效性
                        if (objId.IsNull || objId.IsErased || objId.IsEffectivelyErased || !objId.IsValid)
                            continue;

                        var obj = tr.GetObject(objId, OpenMode.ForRead);

                        ed.WriteMessage("\n" + new string('─', 60));

                        if (obj is DBText dbText)
                        {
                            ed.WriteMessage("\n【单行文本属性】");
                            ed.WriteMessage($"\n  内容: {dbText.TextString}");
                            ed.WriteMessage($"\n  图层: {dbText.Layer}");
                            ed.WriteMessage($"\n  位置: ({dbText.Position.X:F2}, {dbText.Position.Y:F2}, {dbText.Position.Z:F2})");
                            ed.WriteMessage($"\n  高度: {dbText.Height}");
                            ed.WriteMessage($"\n  旋转角: {dbText.Rotation * (180 / Math.PI):F2}°");
                            ed.WriteMessage($"\n  文字样式: {dbText.TextStyleName}");
                        }
                        else if (obj is MText mText)
                        {
                            ed.WriteMessage("\n【多行文本属性】");
                            ed.WriteMessage($"\n  内容: {mText.Text}");
                            ed.WriteMessage($"\n  图层: {mText.Layer}");
                            ed.WriteMessage($"\n  位置: ({mText.Location.X:F2}, {mText.Location.Y:F2}, {mText.Location.Z:F2})");
                            ed.WriteMessage($"\n  文字高度: {mText.TextHeight}");
                            ed.WriteMessage($"\n  宽度: {mText.Width}");
                            ed.WriteMessage($"\n  文字样式: {mText.TextStyleName}");
                        }
                        else if (obj is AttributeReference attRef)
                        {
                            ed.WriteMessage("\n【属性文本】");
                            ed.WriteMessage($"\n  标记: {attRef.Tag}");
                            ed.WriteMessage($"\n  内容: {attRef.TextString}");
                            ed.WriteMessage($"\n  图层: {attRef.Layer}");
                            ed.WriteMessage($"\n  位置: ({attRef.Position.X:F2}, {attRef.Position.Y:F2}, {attRef.Position.Z:F2})");
                            ed.WriteMessage($"\n  高度: {attRef.Height}");
                        }
                    }

                    ed.WriteMessage("\n" + new string('─', 60));
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "查看属性失败");
                ed.WriteMessage($"\n[错误] 查看属性失败: {ex.Message}");
            }
        }
    }
}
