using BiaogeCSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

/// <summary>
/// AI聊天界面ViewModel - 现代化对话界面（类似ChatGPT/Claude）
/// </summary>
public partial class ChatViewModel : ViewModelBase
{
    private readonly AIAssistant _aiAssistant;
    private readonly ILogger<ChatViewModel> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private ObservableCollection<ChatMessageItem> _messages = new();

    [ObservableProperty]
    private string _statusText = "就绪";

    public ChatViewModel(
        AIAssistant aiAssistant,
        ILogger<ChatViewModel> logger)
    {
        _aiAssistant = aiAssistant;
        _logger = logger;

        // 添加欢迎消息
        Messages.Add(new ChatMessageItem
        {
            Role = "assistant",
            Content = "你好！我是表哥建筑CAD助手，专门帮助您处理DWG图纸、翻译和算量任务。有什么我可以帮您的吗？",
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// 发送消息（流式输出）
    /// </summary>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
            return;

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        // 添加用户消息
        Messages.Add(new ChatMessageItem
        {
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.Now
        });

        // 添加AI消息占位符（用于流式更新）
        var aiMessage = new ChatMessageItem
        {
            Role = "assistant",
            Content = "",
            Timestamp = DateTime.Now,
            IsStreaming = true
        };
        Messages.Add(aiMessage);

        IsSending = true;
        IsStreaming = true;
        StatusText = "AI正在思考...";

        _cancellationTokenSource = new CancellationTokenSource();
        var fullResponse = new StringBuilder();

        try
        {
            // 使用流式API
            await foreach (var chunk in _aiAssistant.SendMessageStreamAsync(userMessage, _cancellationTokenSource.Token))
            {
                fullResponse.Append(chunk);
                aiMessage.Content = fullResponse.ToString();
            }

            aiMessage.IsStreaming = false;
            StatusText = "就绪";
            _logger.LogInformation("AI对话完成: {Length}字符", fullResponse.Length);
        }
        catch (OperationCanceledException)
        {
            aiMessage.Content += "\n\n[已取消]";
            aiMessage.IsStreaming = false;
            StatusText = "已取消";
            _logger.LogWarning("AI对话被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI对话失败");
            aiMessage.Content = $"抱歉，发生了错误：{ex.Message}";
            aiMessage.IsStreaming = false;
            StatusText = "错误";
        }
        finally
        {
            IsSending = false;
            IsStreaming = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 取消发送
    /// </summary>
    [RelayCommand]
    private void CancelSending()
    {
        _cancellationTokenSource?.Cancel();
        _logger.LogInformation("用户取消AI对话");
    }

    /// <summary>
    /// 清空聊天记录
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        _aiAssistant.ClearHistory();

        // 重新添加欢迎消息
        Messages.Add(new ChatMessageItem
        {
            Role = "assistant",
            Content = "聊天记录已清空。有什么新问题吗？",
            Timestamp = DateTime.Now
        });

        StatusText = "就绪";
        _logger.LogInformation("聊天记录已清空");
    }

    /// <summary>
    /// 重新生成最后一条回复
    /// </summary>
    [RelayCommand]
    private async Task RegenerateResponseAsync()
    {
        // 找到最后一条用户消息
        var lastUserMessage = Messages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage == null)
            return;

        // 移除最后一条AI消息（如果存在）
        var lastAiMessage = Messages.LastOrDefault(m => m.Role == "assistant");
        if (lastAiMessage != null)
        {
            Messages.Remove(lastAiMessage);
        }

        // 重新发送
        InputText = lastUserMessage.Content;
        await SendMessageAsync();
    }

    /// <summary>
    /// 复制消息内容
    /// </summary>
    [RelayCommand]
    private async Task CopyMessageAsync(ChatMessageItem message)
    {
        try
        {
            // 使用Avalonia的剪贴板API
            if (Avalonia.Application.Current?.Clipboard != null)
            {
                await Avalonia.Application.Current.Clipboard.SetTextAsync(message.Content);
                StatusText = "已复制到剪贴板";
                _logger.LogInformation("消息已复制到剪贴板");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "复制消息失败");
            StatusText = "复制失败";
        }
    }
}

/// <summary>
/// 聊天消息项（用于UI绑定）
/// </summary>
public partial class ChatMessageItem : ObservableObject
{
    [ObservableProperty]
    private string _role = string.Empty; // user, assistant

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private bool _isStreaming = false;

    /// <summary>
    /// 是否为用户消息
    /// </summary>
    public bool IsUser => Role == "user";

    /// <summary>
    /// 是否为AI消息
    /// </summary>
    public bool IsAssistant => Role == "assistant";

    /// <summary>
    /// 格式化时间
    /// </summary>
    public string FormattedTime => Timestamp.ToString("HH:mm");
}
