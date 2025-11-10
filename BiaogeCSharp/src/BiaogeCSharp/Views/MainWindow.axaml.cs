using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BiaogeCSharp.Controls;
using BiaogeCSharp.ViewModels;
using Material.Icons;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BiaogeCSharp.Views;

public partial class MainWindow : Window
{
    private NavigationView _mainNavigation;
    private Button? _settingsButton;

    public MainWindow()
    {
        InitializeComponent();
        _mainNavigation = this.FindControl<NavigationView>("MainNavigation")!;
        _settingsButton = this.FindControl<Button>("SettingsButton");

        if (_settingsButton != null)
        {
            _settingsButton.Click += OnSettingsClick;
        }
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
        var chatPage = new ChatPage { DataContext = viewModel.ChatViewModel };

        // 添加顶部导航项
        _mainNavigation.AddTopNavigationItem("主页", MaterialIconKind.Home, homePage);
        _mainNavigation.AddTopNavigationItem("翻译", MaterialIconKind.Translate, translationPage);
        _mainNavigation.AddTopNavigationItem("算量", MaterialIconKind.Calculator, calculationPage);
        _mainNavigation.AddTopNavigationItem("导出", MaterialIconKind.Export, exportPage);
        _mainNavigation.AddTopNavigationItem("AI助手", MaterialIconKind.RobotOutline, chatPage);

        // 添加底部导航项
        // _mainNavigation.AddBottomNavigationItem("设置", MaterialIconKind.Cog, new SettingsPage());
    }

    private async void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        var settingsDialog = new SettingsDialog(settingsViewModel);
        await settingsDialog.ShowDialog(this);
    }
}
