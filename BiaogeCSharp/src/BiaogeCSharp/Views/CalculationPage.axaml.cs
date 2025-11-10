using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BiaogeCSharp.Views;

public partial class CalculationPage : UserControl
{
    public CalculationPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
