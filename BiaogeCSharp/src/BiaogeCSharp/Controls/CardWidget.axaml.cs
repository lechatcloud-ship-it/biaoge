using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BiaogeCSharp.Controls;

/// <summary>
/// 卡片控件 - 对应Python版本的CardWidget
/// 提供圆角边框、阴影效果和标题显示
/// </summary>
public partial class CardWidget : UserControl
{
    private TextBlock _titleTextBlock;
    private ContentControl _contentArea;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CardWidget, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<object> CardContentProperty =
        AvaloniaProperty.Register<CardWidget, object>(nameof(CardContent));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public CardWidget()
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
        _titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock")!;
        _contentArea = this.FindControl<ContentControl>("ContentArea")!;

        this.GetObservable(TitleProperty).Subscribe(title =>
        {
            if (_titleTextBlock != null)
            {
                _titleTextBlock.Text = title;
                _titleTextBlock.IsVisible = !string.IsNullOrEmpty(title);
            }
        });

        this.GetObservable(CardContentProperty).Subscribe(content =>
        {
            if (_contentArea != null)
            {
                _contentArea.Content = content;
            }
        });
    }
}
