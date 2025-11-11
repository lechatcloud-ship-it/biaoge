using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Serilog;
using BiaogPlugin.Services;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 快速翻译弹窗
    /// 用于单文本实体的快速翻译预览和应用
    /// </summary>
    public partial class QuickTranslatePopup : Window
    {
        private readonly ObjectId _textObjectId;
        private readonly string _originalText;
        private string _translatedText = "";
        private string _currentLanguageCode = "zh";

        /// <summary>
        /// 翻译是否已应用
        /// </summary>
        public bool IsApplied { get; private set; } = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="textObjectId">文本实体ObjectId</param>
        /// <param name="originalText">原文内容</param>
        public QuickTranslatePopup(ObjectId textObjectId, string originalText)
        {
            InitializeComponent();

            _textObjectId = textObjectId;
            _originalText = originalText;

            // 显示原文
            OriginalTextBlock.Text = originalText;

            // 自动开始翻译
            _ = TranslateAsync();
        }

        /// <summary>
        /// 窗口加载完成
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置窗口位置（鼠标附近）
            var mousePos = System.Windows.Forms.Control.MousePosition;
            Left = mousePos.X + 20;
            Top = mousePos.Y + 20;

            // 确保窗口在屏幕内
            var screen = System.Windows.Forms.Screen.FromPoint(mousePos);
            if (Left + Width > screen.WorkingArea.Right)
            {
                Left = screen.WorkingArea.Right - Width - 20;
            }
            if (Top + Height > screen.WorkingArea.Bottom)
            {
                Top = screen.WorkingArea.Bottom - Height - 20;
            }
        }

        /// <summary>
        /// 窗口鼠标按下 - 支持拖动
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // 忽略拖动异常
                }
            }
        }

        /// <summary>
        /// 语言选择改变
        /// </summary>
        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var languageCode = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(languageCode) && languageCode != _currentLanguageCode)
                {
                    _currentLanguageCode = languageCode;
                    await TranslateAsync();
                }
            }
        }

        /// <summary>
        /// 执行翻译
        /// </summary>
        private async Task TranslateAsync()
        {
            try
            {
                // 显示加载状态
                LoadingPanel.Visibility = Visibility.Visible;
                TranslatedTextBlock.Visibility = Visibility.Collapsed;
                ApplyButton.IsEnabled = false;
                StatusTextBlock.Text = "翻译中...";

                Log.Information($"快速翻译: {_originalText.Substring(0, Math.Min(50, _originalText.Length))}... -> {_currentLanguageCode}");

                // 获取服务
                var bailianClient = ServiceLocator.GetService<BailianApiClient>();
                var configManager = ServiceLocator.GetService<ConfigManager>();
                var cacheService = ServiceLocator.GetService<CacheService>();

                if (bailianClient == null || configManager == null || cacheService == null)
                {
                    throw new Exception("服务未初始化");
                }

                var engine = new TranslationEngine(bailianClient, configManager, cacheService);

                // 执行翻译
                var translations = await engine.TranslateBatchWithCacheAsync(
                    new System.Collections.Generic.List<string> { _originalText },
                    "auto",
                    _currentLanguageCode,
                    null
                );

                if (translations.Count > 0 && !string.IsNullOrEmpty(translations[0]))
                {
                    _translatedText = translations[0];
                    TranslatedTextBlock.Text = _translatedText;
                    StatusTextBlock.Text = "✓ 完成";
                    ApplyButton.IsEnabled = true;

                    Log.Information($"快速翻译成功: {_translatedText.Substring(0, Math.Min(50, _translatedText.Length))}...");
                }
                else
                {
                    throw new Exception("翻译结果为空");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "快速翻译失败");
                TranslatedTextBlock.Text = $"翻译失败: {ex.Message}";
                StatusTextBlock.Text = "✗ 失败";
                StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(196, 43, 28)
                );
            }
            finally
            {
                // 隐藏加载状态
                LoadingPanel.Visibility = Visibility.Collapsed;
                TranslatedTextBlock.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 应用翻译按钮
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_translatedText))
                {
                    MessageBox.Show("译文为空，无法应用。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 应用翻译
                var updater = new DwgTextUpdater();
                var updateMap = new System.Collections.Generic.Dictionary<ObjectId, string>
                {
                    [_textObjectId] = _translatedText
                };

                updater.UpdateTexts(updateMap);

                // 记录翻译历史
                var configManager = ServiceLocator.GetService<ConfigManager>();
                if (configManager != null && configManager.Config.Translation.EnableHistory)
                {
                    var history = ServiceLocator.GetService<TranslationHistory>();
                    if (history != null)
                    {
                        var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                        var db = doc.Database;

                        // 获取实体类型和图层信息
                        string entityType = "Unknown";
                        string layerName = "0";

                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var obj = tr.GetObject(_textObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                            if (obj is Autodesk.AutoCAD.DatabaseServices.DBText dbText)
                            {
                                entityType = "DBText";
                                layerName = dbText.Layer;
                            }
                            else if (obj is Autodesk.AutoCAD.DatabaseServices.MText mText)
                            {
                                entityType = "MText";
                                layerName = mText.Layer;
                            }
                            else if (obj is Autodesk.AutoCAD.DatabaseServices.AttributeReference attRef)
                            {
                                entityType = "AttributeReference";
                                layerName = attRef.Layer;
                            }
                            tr.Commit();
                        }

                        await history.AddRecordAsync(new TranslationHistory.HistoryRecord
                        {
                            Timestamp = DateTime.Now,
                            ObjectIdHandle = _textObjectId.Handle.ToString(),
                            OriginalText = _originalText,
                            TranslatedText = _translatedText,
                            SourceLanguage = "auto",
                            TargetLanguage = _currentLanguageCode,
                            EntityType = entityType,
                            Layer = layerName,
                            Operation = "translate"
                        });

                        Log.Debug("已记录双击翻译历史");
                    }
                }

                IsApplied = true;
                StatusTextBlock.Text = "✓ 已应用";

                Log.Information("快速翻译已应用");

                // 延迟关闭窗口
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => Close());
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用翻译失败");
                MessageBox.Show($"应用翻译失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 复制译文按钮
        /// </summary>
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_translatedText))
                {
                    Clipboard.SetText(_translatedText);
                    StatusTextBlock.Text = "✓ 已复制";
                    Log.Information("译文已复制到剪贴板");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "复制译文失败");
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
