using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BiaogeCSharp.Views;

public partial class TranslationPage : UserControl
{
    public TranslationPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
