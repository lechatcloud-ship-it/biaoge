using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;
using BiaogPlugin.Services;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// AI助手对话面板
    /// </summary>
    public partial class AIPalette : UserControl
    {
        private AIAssistantService? _aiService;
        private ConfigManager? _configManager;
        private BailianApiClient? _bailianClient;
        private DrawingContextManager? _contextManager;
        private bool _isProcessing = false;

        // ✅ 会话管理
        private SessionManager? _sessionManager;

        // ✅ 流式Markdown渲染支持
        private DispatcherTimer? _markdownUpdateTimer;
        private StringBuilder _streamingContent = new StringBuilder();
        private RichTextBox? _currentStreamingTarget = null;
        private bool _isStreaming = false;
        private DateTime _lastMarkdownUpdate = DateTime.MinValue;

        // ✅ 性能优化：Markdown渲染缓存
        private readonly System.Collections.Generic.Dictionary<string, FlowDocument> _markdownCache =
            new System.Collections.Generic.Dictionary<string, FlowDocument>();
        private const int MaxCacheSize = 50; // 最多缓存50条消息的渲染结果
        private const int MaxChatHistoryItems = 100; // 聊天历史最多保留100条消息

        public AIPalette()
        {
            InitializeComponent();
            Loaded += AIPalette_Loaded;
            Unloaded += AIPalette_Unloaded; // ✅ 商业级最佳实践：订阅Unloaded事件清理资源

            // ✅ 修复：添加焦点管理，防止输入跳转到CAD命令行
            InputTextBox.GotFocus += InputTextBox_GotFocus;
            InputTextBox.LostFocus += InputTextBox_LostFocus;
            InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;

            // ✅ 关键：捕获所有文本输入事件，防止中文输入法字符传递到AutoCAD
            InputTextBox.PreviewTextInput += InputTextBox_PreviewTextInput;
            InputTextBox.TextInput += InputTextBox_TextInput;

            // ✅ 关键修复：鼠标单击立即获取焦点，无需双击
            InputTextBox.PreviewMouseDown += InputTextBox_PreviewMouseDown;
            InputTextBox.MouseDown += InputTextBox_MouseDown;

            // ✅ 初始化Markdown更新定时器（每150ms更新一次，平衡流畅度和性能）
            _markdownUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _markdownUpdateTimer.Tick += MarkdownUpdateTimer_Tick;
        }

        /// <summary>
        /// 面板加载时初始化服务
        /// </summary>
        private void AIPalette_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("=== AIPalette_Loaded 开始执行 ===");

                // 从ServiceLocator获取服务
                _configManager = ServiceLocator.GetService<ConfigManager>();
                _bailianClient = ServiceLocator.GetService<BailianApiClient>();
                _contextManager = new DrawingContextManager();

                if (_bailianClient != null && _configManager != null)
                {
                    _aiService = new AIAssistantService(_bailianClient, _configManager, _contextManager);
                    Log.Information("AI助手服务初始化成功");
                }
                else
                {
                    AddSystemMessage("❌ 错误：服务初始化失败，请检查API密钥配置（BIAOGE_SETTINGS）");
                    SendButton.IsEnabled = false;
                }

                // ✅ 初始化会话管理器
                _sessionManager = new SessionManager();
                _sessionManager.SessionChanged += OnSessionChanged;
                _sessionManager.SessionsUpdated += OnSessionsUpdated;

                // ✅ 加载当前会话到UI
                LoadCurrentSession();

                Log.Information("会话管理器初始化成功");
                Log.Information("=== AIPalette_Loaded 完成 ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ AIPalette_Loaded 异常");
                try
                {
                    AddSystemMessage($"❌ 初始化失败：{ex.Message}");
                    AddSystemMessage($"详细错误：{ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        AddSystemMessage($"内部异常：{ex.InnerException.Message}");
                    }
                }
                catch
                {
                    Log.Error("无法添加错误消息到UI");
                }

                if (SendButton != null)
                {
                    SendButton.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// ✅ 商业级最佳实践: UserControl卸载时清理所有资源，防止内存泄漏
        /// 参考: Microsoft WPF Best Practices - "Memory Management in WPF"
        /// https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-object-behavior
        /// </summary>
        private void AIPalette_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. ✅ 关键修复：取消SessionManager事件订阅（防止SessionManager持有AIPalette引用）
                if (_sessionManager != null)
                {
                    _sessionManager.SessionChanged -= OnSessionChanged;
                    _sessionManager.SessionsUpdated -= OnSessionsUpdated;
                    Log.Debug("SessionManager事件已取消订阅");
                }

                // 2. ✅ 停止并释放DispatcherTimer（防止Timer持续运行）
                if (_markdownUpdateTimer != null)
                {
                    _markdownUpdateTimer.Stop();
                    _markdownUpdateTimer.Tick -= MarkdownUpdateTimer_Tick;
                    _markdownUpdateTimer = null;
                    Log.Debug("DispatcherTimer已释放");
                }

                // 3. ✅ 取消所有输入框事件订阅
                InputTextBox.GotFocus -= InputTextBox_GotFocus;
                InputTextBox.LostFocus -= InputTextBox_LostFocus;
                InputTextBox.PreviewKeyDown -= InputTextBox_PreviewKeyDown;
                InputTextBox.PreviewTextInput -= InputTextBox_PreviewTextInput;
                InputTextBox.TextInput -= InputTextBox_TextInput;
                InputTextBox.PreviewMouseDown -= InputTextBox_PreviewMouseDown;
                InputTextBox.MouseDown -= InputTextBox_MouseDown;
                Log.Debug("输入框事件已取消订阅");

                // 4. ✅ 清理Markdown缓存字典（释放FlowDocument对象）
                _markdownCache.Clear();
                Log.Debug("Markdown缓存已清除");

                // 5. ✅ 取消Loaded事件订阅
                Loaded -= AIPalette_Loaded;

                Log.Information("AIPalette资源清理完成，防止内存泄漏");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AIPalette资源清理失败");
            }
        }

        /// <summary>
        /// 发送按钮点击
        /// </summary>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        /// <summary>
        /// ✅ 关键修复：PreviewMouseDown - 在鼠标按下时立即获取焦点
        /// 使用AutoCAD官方Window.Focus()方法解决焦点跳转问题
        /// 参考：AutoCAD DevBlog - "Use of Window.Focus in AutoCAD 2014"
        /// </summary>
        private void InputTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 如果TextBox还没有焦点，立即获取焦点
                if (!InputTextBox.IsFocused)
                {
                    Log.Debug("鼠标按下，设置焦点到输入框");

                    // ✅ 修复焦点跳转：使用WPF标准焦点方法，配合PaletteSet.KeepFocus=true
                    // 不调用doc.Window.Focus()，避免焦点在PaletteSet和AutoCAD之间跳转
                    Keyboard.Focus(InputTextBox);
                    InputTextBox.Focus();

                    // ✅ 不设置e.Handled，让MouseDown事件继续传递
                    // 这样TextBox能正确处理光标位置
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理PreviewMouseDown失败");
            }
        }

        /// <summary>
        /// ✅ MouseDown - 确保焦点已经设置
        /// 使用AutoCAD官方Window.Focus()方法
        /// </summary>
        private void InputTextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 再次确保焦点在TextBox上
                if (!InputTextBox.IsFocused)
                {
                    Log.Debug("MouseDown事件，确保焦点在输入框");

                    // ✅ 修复焦点跳转：使用WPF标准焦点方法，配合PaletteSet.KeepFocus=true
                    Keyboard.Focus(InputTextBox);
                    InputTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理MouseDown失败");
            }
        }

        /// <summary>
        /// ✅ 修复问题7：输入框获得焦点时 - 不再强制保持焦点
        /// 允许用户自由切换到AutoCAD命令行
        /// </summary>
        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // ❌ 删除：不再使用Dispatcher强制获取焦点
                // Dispatcher.BeginInvoke(new Action(() =>
                // {
                //     if (!InputTextBox.IsFocused)
                //     {
                //         Keyboard.Focus(InputTextBox);
                //         InputTextBox.Focus();
                //     }
                // }), DispatcherPriority.Input);

                Log.Debug("AI助手输入框获得焦点");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理输入框焦点失败");
            }
        }

        /// <summary>
        /// 输入框失去焦点时 - 仅记录日志，不再强制抢回焦点
        /// </summary>
        private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // ✅ 修复：不再强制抢回焦点，允许用户点击按钮
                // 只在AutoCAD窗口获得焦点时才需要担心
                Log.Debug("AI助手输入框失去焦点");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理输入框失去焦点失败");
            }
        }

        /// <summary>
        /// ✅ 关键修复：捕获文本输入，防止中文字符传递到AutoCAD
        /// </summary>
        private void InputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 标记事件已处理，防止传播到AutoCAD命令行
            // 但让TextBox正常接收输入
            e.Handled = false;

            // ❌ 删除：不再强制获取焦点，允许用户切换到AutoCAD
            // if (!InputTextBox.IsFocused)
            // {
            //     Keyboard.Focus(InputTextBox);
            //     InputTextBox.Focus();
            // }
        }

        /// <summary>
        /// ✅ 文本输入事件：确保所有字符（包括中文）都留在TextBox中
        /// </summary>
        private void InputTextBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            // 标记事件已处理，完全阻止传播到AutoCAD
            e.Handled = true;

            Log.Debug($"输入文本: {e.Text}");
        }

        /// <summary>
        /// ✅ 修复问题7：预处理按键，但不强制获取焦点
        /// </summary>
        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // ❌ 删除：不再强制获取焦点
            // if (!InputTextBox.IsFocused)
            // {
            //     Keyboard.Focus(InputTextBox);
            //     InputTextBox.Focus();
            // }

            // 除了Tab键（用于切换焦点）和Escape键（可能需要取消操作）
            // 其他所有按键都在TextBox内部处理，不传播到AutoCAD
            if (e.Key != Key.Tab && e.Key != Key.Escape)
            {
                // 不设置Handled，让TextBox正常处理按键
                e.Handled = false;
            }

            // ✅ 改进的快捷键逻辑（更符合聊天应用习惯）
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // Ctrl+Enter → 插入换行（让TextBox正常处理）
                    e.Handled = false;
                }
                else
                {
                    // 单独Enter → 发送消息
                    e.Handled = true;
                    _ = SendMessageAsync();
                }
            }
        }

        /// <summary>
        /// 输入框按键处理（Enter发送，Ctrl+Enter换行）
        /// </summary>
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // 已在PreviewKeyDown中处理，这里作为备用
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Control)
            {
                e.Handled = true;
                _ = SendMessageAsync();
            }
        }

        /// <summary>
        /// 输入框文本变化（启用/禁用发送按钮）
        /// </summary>
        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputTextBox.Text) && !_isProcessing;
        }

        /// <summary>
        /// 清除对话历史
        /// </summary>
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清除所有对话历史吗？",
                "确认清除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _aiService?.ClearHistory();
                ChatHistoryPanel.Children.Clear();

                // ✅ 性能优化：清除Markdown渲染缓存
                _markdownCache.Clear();
                Log.Debug("Markdown渲染缓存已清除");

                // 重新添加欢迎消息
                AddWelcomeMessage();

                Log.Information("对话历史已清除");
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        private async Task SendMessageAsync()
        {
            if (_aiService == null || _isProcessing)
                return;

            string userInput = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(userInput))
                return;

            try
            {
                _isProcessing = true;
                SendButton.IsEnabled = false;
                StatusText.Text = "正在思考...";

                // 显示用户消息
                AddUserMessage(userInput);

                // 清空输入框
                InputTextBox.Clear();

                // ✅ 启发式深度思考检测：如果1.5秒内没收到chunk，显示"深度思考中..."
                bool hasReceivedFirstChunk = false;
                var thinkingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1500)
                };
                thinkingTimer.Tick += (s, args) =>
                {
                    if (!hasReceivedFirstChunk)
                    {
                        StatusText.Text = "🧠 深度思考中...";
                        Log.Debug("检测到可能的深度思考（1.5秒未收到响应）");
                    }
                    thinkingTimer.Stop();
                };
                thinkingTimer.Start();

                // ✅ 正文框延迟创建：只在收到第一个内容chunk时创建
                Border? aiMessageBorder = null;
                RichTextBox? aiRichTextBox = null;
                StreamingMarkdownRenderer? contentRenderer = null;
                string fullResponse = "";

                // ✅ OpenAI SDK流式输出 - 保持流式功能不变
                // OpenAI SDK的await foreach保留SynchronizationContext，回调已在UI线程执行
                var response = await _aiService.ChatStreamAsync(
                    userMessage: userInput,
                    onContentChunk: chunk =>
                    {
                        try
                        {
                            // ✅ 收到第一个内容chunk时，动态创建正文框
                            if (aiMessageBorder == null)
                            {
                                hasReceivedFirstChunk = true;
                                thinkingTimer.Stop(); // 停止检测

                                aiMessageBorder = CreateStreamingAIMessagePlaceholder();
                                ChatHistoryPanel.Children.Add(aiMessageBorder);
                                ScrollToBottom();

                                aiRichTextBox = FindAIRichTextBox(aiMessageBorder);
                                if (aiRichTextBox != null)
                                {
                                    contentRenderer = new StreamingMarkdownRenderer(aiRichTextBox);
                                }

                                // ✅ 收到第一个chunk时，改为"正在回复..."
                                StatusText.Text = "正在回复...";
                            }

                            fullResponse += chunk;
                            // ✅ 直接调用 - OpenAI SDK已保证UI线程安全
                            contentRenderer?.AppendChunk(chunk);
                            ScrollToBottom();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "内容流式更新失败");
                        }
                    }
                );

                // ✅ 完成流式输出，强制最终更新
                Dispatcher.Invoke(() =>
                {
                    contentRenderer?.Complete();
                    ScrollToBottom();
                });

                if (!response.Success)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // ✅ 如果失败前没有收到任何内容chunk，需要先创建正文框
                        if (aiMessageBorder == null)
                        {
                            aiMessageBorder = CreateStreamingAIMessagePlaceholder();
                            ChatHistoryPanel.Children.Add(aiMessageBorder);
                            aiRichTextBox = FindAIRichTextBox(aiMessageBorder);
                        }

                        if (aiRichTextBox != null)
                        {
                            // 显示错误信息
                            var errorDoc = new FlowDocument();
                            errorDoc.Blocks.Add(new Paragraph(new Run($"❌ 错误：{response.Error}")
                            {
                                Foreground = Brushes.Red,
                                FontSize = 13
                            }));
                            aiRichTextBox.Document = errorDoc;
                        }
                        ScrollToBottom();
                    });
                    Log.Error($"AI助手错误: {response.Error}");
                }
                else
                {
                    // ✅ 流式输出已完成，Markdown已实时渲染
                    Log.Information($"AI回复完成，共{fullResponse.Length}字符");
                }

                StatusText.Text = "就绪";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "发送消息失败");
                AddSystemMessage($"❌ 错误：{ex.Message}");
                StatusText.Text = "发生错误";
            }
            finally
            {
                _isProcessing = false;
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputTextBox.Text);

                // ✅ v1.0.7修复：每次对话后自动保存会话历史到本地
                SaveCurrentSessionMessages();

                // ✅ 性能优化：修剪聊天历史，防止内存占用过高
                TrimChatHistory();

                // ✅ 确保焦点回到输入框，准备下一次输入
                // 修复焦点跳转：使用WPF标准焦点方法，配合PaletteSet.KeepFocus=true
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 使用WPF标准焦点方法，不调用doc.Window.Focus()
                        Keyboard.Focus(InputTextBox);
                        InputTextBox.Focus();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "恢复InputTextBox焦点失败");
                    }
                }), DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// 添加用户消息
        /// </summary>
        private void AddUserMessage(string message)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)), // 蓝色
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(40, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };

            border.Child = textBlock;
            ChatHistoryPanel.Children.Add(border);
            ScrollToBottom();
        }

        /// <summary>
        /// 创建AI消息占位符
        /// </summary>
        private Border CreateAIMessagePlaceholder()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), // 深灰色
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 40, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // ✅ 使用StackPanel包裹，以便后续添加Expander
            var stackPanel = new StackPanel();

            var textBlock = new TextBlock
            {
                Text = "思考中...",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };

            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 查找AI消息的TextBlock（从StackPanel中查找）
        /// </summary>
        private TextBlock? FindAITextBlock(Border border)
        {
            // ✅ 从StackPanel中查找第一个TextBlock
            if (border.Child is StackPanel panel && panel.Children.Count > 0)
            {
                return panel.Children[0] as TextBlock;
            }

            // 向后兼容：直接是TextBlock的情况
            return border.Child as TextBlock;
        }

        /// <summary>
        /// 添加系统消息
        /// </summary>
        private void AddSystemMessage(string message)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };

            border.Child = textBlock;
            ChatHistoryPanel.Children.Add(border);
            ScrollToBottom();
        }

        /// <summary>
        /// 添加欢迎消息
        /// </summary>
        private void AddWelcomeMessage()
        {
            // 欢迎消息已在XAML中定义，这里不需要额外添加
        }

        /// <summary>
        /// 滚动到底部
        /// </summary>
        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToBottom();
        }

        /// <summary>
        /// 修剪聊天历史，防止内存占用过高
        /// </summary>
        private void TrimChatHistory()
        {
            // 如果消息数超过限制，移除最旧的消息
            while (ChatHistoryPanel.Children.Count > MaxChatHistoryItems)
            {
                var oldestChild = ChatHistoryPanel.Children[0];

                // 释放资源
                if (oldestChild is Border border && border.Child is RichTextBox rtb)
                {
                    rtb.Document = null; // 释放FlowDocument
                }

                ChatHistoryPanel.Children.RemoveAt(0);
                Log.Debug($"移除最旧消息，当前消息数: {ChatHistoryPanel.Children.Count}");
            }
        }

        #region 会话管理

        /// <summary>
        /// 会话按钮点击 - 打开会话管理菜单
        /// </summary>
        private void SessionMenuButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 更新会话列表
                UpdateSessionList();

                // 切换Popup显示状态
                SessionPopup.IsOpen = !SessionPopup.IsOpen;
                Log.Information($"会话管理菜单 {(SessionPopup.IsOpen ? "打开" : "关闭")}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开会话管理菜单失败");
                AddSystemMessage($"❌ 打开会话菜单失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 新建会话
        /// </summary>
        private void NewSessionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sessionManager == null)
                {
                    Log.Warning("SessionManager未初始化");
                    return;
                }

                // 保存当前会话
                SaveCurrentSessionMessages();

                // 创建新会话
                var newSession = _sessionManager.CreateNewSession();
                Log.Information($"创建新会话: {newSession.Id}");

                // 清空UI并显示欢迎消息
                ClearChatHistory();

                // 关闭Popup
                SessionPopup.IsOpen = false;

                AddSystemMessage($"✅ 已创建新会话");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "创建新会话失败");
                AddSystemMessage($"❌ 创建新会话失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 会话列表选择变化
        /// </summary>
        private void SessionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (SessionListBox.SelectedItem is not Models.ChatSession selectedSession)
                    return;

                if (_sessionManager == null)
                    return;

                // 如果选中的是当前会话，不做处理
                if (selectedSession.Id == _sessionManager.CurrentSession?.Id)
                    return;

                // 保存当前会话
                SaveCurrentSessionMessages();

                // 切换会话
                _sessionManager.SwitchToSession(selectedSession.Id);
                Log.Information($"切换到会话: {selectedSession.Title}");

                // 加载会话消息到UI
                LoadCurrentSession();

                // 关闭Popup
                SessionPopup.IsOpen = false;

                AddSystemMessage($"✅ 已切换到会话：{selectedSession.Title}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换会话失败");
                AddSystemMessage($"❌ 切换会话失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 删除会话
        /// </summary>
        private void DeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button button)
                    return;

                if (button.Tag is not string sessionId)
                    return;

                if (_sessionManager == null)
                    return;

                // 确认删除
                var session = _sessionManager.Sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session == null)
                    return;

                // 不允许删除最后一个会话
                if (_sessionManager.Sessions.Count <= 1)
                {
                    AddSystemMessage("⚠️ 至少保留一个会话");
                    return;
                }

                // 删除会话
                _sessionManager.DeleteSession(sessionId);
                Log.Information($"删除会话: {session.Title}");

                // 更新列表
                UpdateSessionList();

                AddSystemMessage($"✅ 已删除会话：{session.Title}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除会话失败");
                AddSystemMessage($"❌ 删除会话失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 会话切换事件
        /// </summary>
        private void OnSessionChanged(object? sender, Models.ChatSession session)
        {
            Dispatcher.Invoke(() =>
            {
                Log.Information($"会话已切换: {session.Title}");
                // 会话已在LoadCurrentSession中加载，这里不需要额外操作
            });
        }

        /// <summary>
        /// 会话列表更新事件
        /// </summary>
        private void OnSessionsUpdated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateSessionList();
            });
        }

        /// <summary>
        /// 更新会话列表显示
        /// </summary>
        private void UpdateSessionList()
        {
            try
            {
                if (_sessionManager == null)
                    return;

                // 更新列表
                SessionListBox.ItemsSource = null;
                SessionListBox.ItemsSource = _sessionManager.Sessions;

                // 选中当前会话
                if (_sessionManager.CurrentSession != null)
                {
                    SessionListBox.SelectedItem = _sessionManager.CurrentSession;
                }

                // 更新统计
                SessionCountText.Text = $"共 {_sessionManager.Sessions.Count} 个会话";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "更新会话列表失败");
            }
        }

        /// <summary>
        /// 加载当前会话到UI
        /// </summary>
        private void LoadCurrentSession()
        {
            try
            {
                if (_sessionManager?.CurrentSession == null)
                    return;

                var session = _sessionManager.CurrentSession;

                // 清空UI
                ClearChatHistory();

                // ✅ 恢复AI服务的历史记录（关键修复：防止切换会话后历史丢失）
                if (_aiService != null && session.Messages.Count > 0)
                {
                    _aiService.LoadHistory(session.Messages);
                    Log.Debug($"恢复AI服务历史: {session.Messages.Count}条消息");
                }

                // 加载历史消息到UI
                foreach (var message in session.Messages)
                {
                    if (message.Role == "user")
                    {
                        AddUserMessage(message.Content);
                    }
                    else if (message.Role == "assistant")
                    {
                        // 创建AI消息并直接显示Markdown渲染版本
                        var border = CreateStreamingAIMessagePlaceholder();
                        ChatHistoryPanel.Children.Add(border);

                        var richTextBox = FindAIRichTextBox(border);
                        if (richTextBox != null)
                        {
                            // 直接渲染Markdown
                            var document = MarkdownRenderer.RenderMarkdown(message.Content);
                            richTextBox.Document = document;
                        }
                    }
                }

                ScrollToBottom();
                Log.Information($"加载会话: {session.Title}, {session.Messages.Count}条消息");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载会话失败");
            }
        }

        /// <summary>
        /// 保存当前会话的消息历史
        /// </summary>
        private void SaveCurrentSessionMessages()
        {
            try
            {
                if (_sessionManager == null || _aiService == null)
                    return;

                // 从AIAssistantService获取消息历史
                var messages = _aiService.GetHistory();
                _sessionManager.UpdateCurrentSessionMessages(messages);

                Log.Debug($"保存会话消息: {messages.Count}条");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存会话消息失败");
            }
        }

        /// <summary>
        /// 清除历史按钮 - 清空当前会话
        /// </summary>
        private void ClearChatHistory()
        {
            try
            {
                // ✅ 清空聊天面板（保留欢迎消息）
                // 欢迎消息是XAML中定义的第一个子元素，从后往前删除其他所有消息
                while (ChatHistoryPanel.Children.Count > 1)
                {
                    ChatHistoryPanel.Children.RemoveAt(ChatHistoryPanel.Children.Count - 1);
                }

                // ✅ 确保欢迎消息可见（防止被误隐藏）
                if (ChatHistoryPanel.Children.Count > 0 && ChatHistoryPanel.Children[0] is UIElement welcomeMsg)
                {
                    welcomeMsg.Visibility = Visibility.Visible;
                }

                // 清空AI服务的历史
                _aiService?.ClearHistory();

                // ✅ 性能优化：清除Markdown渲染缓存
                _markdownCache.Clear();
                Log.Debug("Markdown渲染缓存已清除");

                // 清空当前会话的消息
                if (_sessionManager?.CurrentSession != null)
                {
                    _sessionManager.CurrentSession.Messages.Clear();
                    _sessionManager.SaveCurrentSession();
                }

                Log.Information($"已清空对话历史（保留欢迎消息，当前子元素数：{ChatHistoryPanel.Children.Count}）");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清空历史失败");
            }
        }

        /// <summary>
        /// 创建流式AI消息占位符（使用RichTextBox支持Markdown）
        /// </summary>
        private Border CreateStreamingAIMessagePlaceholder()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), // 深灰色
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 40, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var richTextBox = new RichTextBox
            {
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // ✅ 用户体验改进：显示"在思考中..."占位符
            // 流式内容到达时会自动替换此占位符
            var document = new FlowDocument();
            var paragraph = new Paragraph();

            // 添加思考图标和文本
            var thinkingRun = new Run("💭 在思考中...")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)), // 浅灰色
                FontStyle = FontStyles.Italic
            };
            paragraph.Inlines.Add(thinkingRun);

            document.Blocks.Add(paragraph);
            richTextBox.Document = document;

            border.Child = richTextBox;

            return border;
        }

        /// <summary>
        /// 查找AI消息的RichTextBox
        /// </summary>
        private RichTextBox? FindAIRichTextBox(Border border)
        {
            return border.Child as RichTextBox;
        }

        /// <summary>
        /// Markdown更新定时器Tick事件（替代旧的打字机效果）
        /// </summary>
        private void MarkdownUpdateTimer_Tick(object sender, EventArgs e)
        {
            // 此方法保留用于未来的扩展
            // 当前使用StreamingMarkdownRenderer内部的定时器
        }

        #endregion
    }
}
