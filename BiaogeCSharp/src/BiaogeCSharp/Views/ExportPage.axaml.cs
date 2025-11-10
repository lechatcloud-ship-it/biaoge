using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BiaogeCSharp.Views;

public partial class ExportPage : UserControl
{
    public ExportPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
