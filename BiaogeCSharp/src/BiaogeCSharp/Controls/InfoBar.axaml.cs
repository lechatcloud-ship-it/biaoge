using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace BiaogeCSharp.Controls;

/// <summary>
/// 信息条控件 - 对应Python版本的InfoBar
/// 用于显示成功、警告、错误等通知消息
/// </summary>
public partial class InfoBar : UserControl
{
    private Border _rootBorder;
    private TextBlock _iconTextBlock;
    private TextBlock _titleTextBlock;
    private TextBlock _messageTextBlock;
    private Button _closeButton;

    public enum InfoBarSeverity
    {
        Success,
        Warning,
        Error,
        Info
    }

    public static readonly StyledProperty<InfoBarSeverity> SeverityProperty =
        AvaloniaProperty.Register<InfoBar, InfoBarSeverity>(nameof(Severity), InfoBarSeverity.Info);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InfoBar, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<InfoBar, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<InfoBar, int>(nameof(Duration), 3000);

    public InfoBarSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public event EventHandler? Closed;

    public InfoBar()
    {
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeControls()
    {
        _rootBorder = this.FindControl<Border>("RootBorder")!;
        _iconTextBlock = this.FindControl<TextBlock>("IconTextBlock")!;
        _titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock")!;
        _messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock")!;
        _closeButton = this.FindControl<Button>("CloseButton")!;

        this.GetObservable(SeverityProperty).Subscribe(UpdateSeverityStyle);
        this.GetObservable(TitleProperty).Subscribe(title => _titleTextBlock.Text = title);
        this.GetObservable(MessageProperty).Subscribe(message => _messageTextBlock.Text = message);
        this.GetObservable(DurationProperty).Subscribe(StartAutoCloseTimer);
    }

    private void UpdateSeverityStyle(InfoBarSeverity severity)
    {
        var (background, border, foreground, icon) = severity switch
        {
            InfoBarSeverity.Success => ("#0F7B0F", "#107C10", "#FFFFFF", "✓"),
            InfoBarSeverity.Warning => ("#9D5D00", "#FDE300", "#FFFFFF", "⚠"),
            InfoBarSeverity.Error => ("#C42B1C", "#E81123", "#FFFFFF", "✕"),
            _ => ("#005A9E", "#0078D4", "#FFFFFF", "ℹ")
        };

        _rootBorder.Background = Brush.Parse(background);
        _rootBorder.BorderBrush = Brush.Parse(border);
        _iconTextBlock.Foreground = Brush.Parse(foreground);
        _titleTextBlock.Foreground = Brush.Parse(foreground);
        _messageTextBlock.Foreground = Brush.Parse(foreground);
        _closeButton.Foreground = Brush.Parse(foreground);
        _iconTextBlock.Text = icon;
    }

    private void StartAutoCloseTimer(int duration)
    {
        if (duration > 0)
        {
            DispatcherTimer.RunOnce(() => Close(), TimeSpan.FromMilliseconds(duration));
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Close()
    {
        Closed?.Invoke(this, EventArgs.Empty);
        IsVisible = false;
    }

    /// <summary>
    /// 显示成功消息
    /// </summary>
    public static InfoBar ShowSuccess(string title, string message, int duration = 3000)
    {
        return Show(InfoBarSeverity.Success, title, message, duration);
    }

    /// <summary>
    /// 显示警告消息
    /// </summary>
    public static InfoBar ShowWarning(string title, string message, int duration = 3000)
    {
        return Show(InfoBarSeverity.Warning, title, message, duration);
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    public static InfoBar ShowError(string title, string message, int duration = 5000)
    {
        return Show(InfoBarSeverity.Error, title, message, duration);
    }

    /// <summary>
    /// 显示信息消息
    /// </summary>
    public static InfoBar ShowInfo(string title, string message, int duration = 3000)
    {
        return Show(InfoBarSeverity.Info, title, message, duration);
    }

    private static InfoBar Show(InfoBarSeverity severity, string title, string message, int duration)
    {
        var infoBar = new InfoBar
        {
            Severity = severity,
            Title = title,
            Message = message,
            Duration = duration
        };
        return infoBar;
    }
}
