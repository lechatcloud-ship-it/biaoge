using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BiaogeCSharp.ViewModels;
using System;

namespace BiaogeCSharp.Views;

public partial class ChatPage : UserControl
{
    public ChatPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 处理输入框按键事件
    /// </summary>
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter发送消息（Shift+Enter换行）
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;

            if (DataContext is ChatViewModel viewModel && !viewModel.IsSending)
            {
                // 异步执行命令
                Dispatcher.UIThread.Post(async () =>
                {
                    if (viewModel.SendMessageCommand.CanExecute(null))
                    {
                        await viewModel.SendMessageCommand.ExecuteAsync(null);

                        // 滚动到底部
                        ScrollToBottom();
                    }
                });
            }
        }
    }

    /// <summary>
    /// 滚动消息列表到底部
    /// </summary>
    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
            scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 当DataContext改变时，订阅Messages集合变化事件
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ChatViewModel viewModel)
        {
            // 当消息集合变化时，自动滚动到底部
            viewModel.Messages.CollectionChanged += (s, args) =>
            {
                ScrollToBottom();
            };
        }
    }
}
