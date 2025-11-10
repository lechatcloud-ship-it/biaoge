using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace BiaogeCSharp.Controls;

/// <summary>
/// Toast通知类型
/// </summary>
public enum ToastType
{
    Success,
    Warning,
    Error,
    Info
}

/// <summary>
/// 现代化Toast通知控件
/// 支持Success/Warning/Error/Info四种类型
/// 自动消失和手动关闭
/// </summary>
public partial class ToastNotification : UserControl
{
    private Border? _toastBorder;
    private Border? _iconBorder;
    private TextBlock? _iconText;
    private TextBlock? _titleText;
    private TextBlock? _messageText;
    private Button? _closeButton;

    public ToastNotification()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _toastBorder = this.FindControl<Border>("ToastBorder");
        _iconBorder = this.FindControl<Border>("IconBorder");
        _iconText = this.FindControl<TextBlock>("IconText");
        _titleText = this.FindControl<TextBlock>("TitleText");
        _messageText = this.FindControl<TextBlock>("MessageText");
        _closeButton = this.FindControl<Button>("CloseButton");

        if (_closeButton != null)
        {
            _closeButton.Click += (s, e) => Close();
        }
    }

    /// <summary>
    /// 显示成功通知
    /// </summary>
    public static async Task ShowSuccess(string title, string message, int durationMs = 3000)
    {
        await Show(ToastType.Success, title, message, durationMs);
    }

    /// <summary>
    /// 显示警告通知
    /// </summary>
    public static async Task ShowWarning(string title, string message, int durationMs = 4000)
    {
        await Show(ToastType.Warning, title, message, durationMs);
    }

    /// <summary>
    /// 显示错误通知
    /// </summary>
    public static async Task ShowError(string title, string message, int durationMs = 5000)
    {
        await Show(ToastType.Error, title, message, durationMs);
    }

    /// <summary>
    /// 显示信息通知
    /// </summary>
    public static async Task ShowInfo(string title, string message, int durationMs = 3000)
    {
        await Show(ToastType.Info, title, message, durationMs);
    }

    /// <summary>
    /// 显示Toast通知
    /// </summary>
    private static async Task Show(ToastType type, string title, string message, int durationMs)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var toast = new ToastNotification();
            toast.Configure(type, title, message);

            // 获取主窗口
            var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null) return;

            // 查找或创建Toast容器
            var toastContainer = mainWindow.FindControl<Panel>("ToastContainer");
            if (toastContainer == null)
            {
                // 如果没有ToastContainer，创建一个临时的覆盖层
                // 这需要主窗口支持覆盖层
                return;
            }

            // 添加到容器
            toastContainer.Children.Add(toast);

            // 淡入动画
            toast.Opacity = 0;
            toast.RenderTransform = new TranslateTransform(0, -20);
            await Task.Delay(50);
            toast.Opacity = 1;
            toast.RenderTransform = new TranslateTransform(0, 0);

            // 自动关闭
            if (durationMs > 0)
            {
                await Task.Delay(durationMs);
                await toast.Close();
            }
        });
    }

    /// <summary>
    /// 配置Toast样式
    /// </summary>
    private void Configure(ToastType type, string title, string message)
    {
        if (_titleText != null)
            _titleText.Text = title;

        if (_messageText != null)
            _messageText.Text = message;

        if (_iconBorder == null || _iconText == null) return;

        switch (type)
        {
            case ToastType.Success:
                _iconBorder.Background = new SolidColorBrush(Color.Parse("#00D47E"));
                _iconText.Text = "✓";
                _iconText.Foreground = Brushes.White;
                break;

            case ToastType.Warning:
                _iconBorder.Background = new SolidColorBrush(Color.Parse("#FFB900"));
                _iconText.Text = "⚠";
                _iconText.Foreground = Brushes.White;
                break;

            case ToastType.Error:
                _iconBorder.Background = new SolidColorBrush(Color.Parse("#E81123"));
                _iconText.Text = "✕";
                _iconText.Foreground = Brushes.White;
                break;

            case ToastType.Info:
                _iconBorder.Background = new SolidColorBrush(Color.Parse("#00B4D8"));
                _iconText.Text = "ℹ";
                _iconText.Foreground = Brushes.White;
                break;
        }
    }

    /// <summary>
    /// 关闭Toast
    /// </summary>
    private async Task Close()
    {
        // 淡出动画
        this.Opacity = 0;
        this.RenderTransform = new TranslateTransform(0, -20);
        await Task.Delay(250);

        // 从容器中移除
        if (Parent is Panel panel)
        {
            panel.Children.Remove(this);
        }
    }
}
