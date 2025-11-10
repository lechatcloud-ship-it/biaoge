using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BiaogeCSharp.Controls;
using BiaogeCSharp.ViewModels;
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
        _mainNavigation.AddTopNavigationItem("主页", "■", homePage);
        _mainNavigation.AddTopNavigationItem("翻译", "▶", translationPage);
        _mainNavigation.AddTopNavigationItem("算量", "▣", calculationPage);
        _mainNavigation.AddTopNavigationItem("导出", "▲", exportPage);
        _mainNavigation.AddTopNavigationItem("AI助手", "●", chatPage);

        // 添加底部导航项
        // _mainNavigation.AddBottomNavigationItem("设置", "⚙", new SettingsPage());
    }

    private async void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        var settingsDialog = new SettingsDialog(settingsViewModel);
        await settingsDialog.ShowDialog(this);
    }
}
