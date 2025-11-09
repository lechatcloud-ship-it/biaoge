using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BiaogeCSharp.ViewModels;

namespace BiaogeCSharp.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        // 触发主窗口的打开文件命令
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.OpenDwgFileCommand.ExecuteAsync(null);
        }
    }
}
