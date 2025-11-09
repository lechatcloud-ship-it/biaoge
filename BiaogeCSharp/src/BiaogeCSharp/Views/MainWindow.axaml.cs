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
        _mainNavigation = this.FindControl<NavigationView>("MainNavigation")!;
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        InitializeNavigation(viewModel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeNavigation(MainWindowViewModel viewModel)
    {
        // 创建页面并设置DataContext
        var homePage = new HomePage { DataContext = viewModel };
        var translationPage = new TranslationPage { DataContext = viewModel.TranslationViewModel };
        var calculationPage = new CalculationPage { DataContext = viewModel.CalculationViewModel };
        var exportPage = new ExportPage { DataContext = viewModel.ExportViewModel };

        // 添加顶部导航项
        _mainNavigation.AddTopNavigationItem("主页", "🏠", homePage);
        _mainNavigation.AddTopNavigationItem("翻译", "🌐", translationPage);
        _mainNavigation.AddTopNavigationItem("算量", "📊", calculationPage);
        _mainNavigation.AddTopNavigationItem("导出", "📤", exportPage);

        // 添加底部导航项
        // _mainNavigation.AddBottomNavigationItem("设置", "⚙", new SettingsPage());
    }
}
