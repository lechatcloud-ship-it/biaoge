using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BiaogeCSharp.Controls;
using BiaogeCSharp.ViewModels;

namespace BiaogeCSharp.Views;

public partial class MainWindow : Window
{
    private NavigationView _mainNavigation;

    public MainWindow()
    {
        InitializeComponent();
        InitializeNavigation();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // 将ViewModel设置到所有页面
        if (_mainNavigation != null)
        {
            SetViewModelToPages(viewModel);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeNavigation()
    {
        _mainNavigation = this.FindControl<NavigationView>("MainNavigation")!;

        // 添加顶部导航项
        _mainNavigation.AddTopNavigationItem("主页", "🏠", new HomePage());
        _mainNavigation.AddTopNavigationItem("翻译", "🌐", new TranslationPage());
        _mainNavigation.AddTopNavigationItem("算量", "📊", new CalculationPage());
        _mainNavigation.AddTopNavigationItem("导出", "📤", new ExportPage());

        // 添加底部导航项
        // _mainNavigation.AddBottomNavigationItem("设置", "⚙", new SettingsPage());
    }

    private void SetViewModelToPages(MainWindowViewModel viewModel)
    {
        // 这里可以设置页面的DataContext
        // 每个页面会继承主窗口的ViewModel或有自己的ViewModel
    }
}
