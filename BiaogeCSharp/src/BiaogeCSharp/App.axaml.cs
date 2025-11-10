using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BiaogeCSharp.Views;
using BiaogeCSharp.ViewModels;
using BiaogeCSharp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using Avalonia.Controls;

namespace BiaogeCSharp;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;
    public static new App Current => (App)Application.Current!;
    public Window? MainWindow => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置服务
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 配置
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // 日志
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // HTTP客户端
        services.AddHttpClient<BailianApiClient>();
        services.AddHttpClient<AIAssistant>();

        // 业务服务
        services.AddSingleton<AsposeDwgParser>();
        services.AddSingleton<CacheService>();
        services.AddSingleton<TranslationEngine>();
        services.AddSingleton<DwgTranslationService>();  // 核心翻译服务
        services.AddSingleton<ConfigManager>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<ComponentRecognizer>();

        // AI服务
        services.AddSingleton<AIContextManager>();
        services.AddSingleton<AIAssistant>();

        // 性能监控
        services.AddSingleton<PerformanceMonitor>();

        // 导出服务
        services.AddSingleton<DwgExporter>();
        services.AddSingleton<PdfExporter>();
        services.AddSingleton<ExcelExporter>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TranslationViewModel>();
        services.AddTransient<CalculationViewModel>();
        services.AddTransient<ExportViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ChatViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsDialog>();
        services.AddTransient<ChatPage>();
    }
}
