using Avalonia.Platform.Storage;
using BiaogeCSharp.Models;
using BiaogeCSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AsposeDwgParser _dwgParser;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private DwgDocument? _currentDocument;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private float _zoom = 1.0f;

    [ObservableProperty]
    private ObservableCollection<LayerInfo> _layers = new();

    public TranslationViewModel TranslationViewModel { get; }
    public CalculationViewModel CalculationViewModel { get; }
    public ExportViewModel ExportViewModel { get; }

    public MainWindowViewModel(
        AsposeDwgParser dwgParser,
        TranslationViewModel translationViewModel,
        CalculationViewModel calculationViewModel,
        ExportViewModel exportViewModel,
        ILogger<MainWindowViewModel> logger)
    {
        _dwgParser = dwgParser;
        TranslationViewModel = translationViewModel;
        CalculationViewModel = calculationViewModel;
        ExportViewModel = exportViewModel;
        _logger = logger;
    }

    [RelayCommand]
    private async Task OpenDwgFileAsync()
    {
        try
        {
            var topLevel = App.Current.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "打开DWG文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DWG文件")
                    {
                        Patterns = new[] { "*.dwg", "*.dxf" }
                    }
                }
            });

            if (files.Count > 0)
            {
                await LoadDwgFileAsync(files[0].Path.LocalPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开文件失败");
            StatusText = $"打开失败: {ex.Message}";
        }
    }

    private async Task LoadDwgFileAsync(string filePath)
    {
        IsBusy = true;
        StatusText = "正在加载DWG文件...";

        try
        {
            // 异步加载
            CurrentDocument = await Task.Run(() => _dwgParser.Parse(filePath));

            // 更新图层列表
            Layers.Clear();
            foreach (var layer in CurrentDocument.Layers)
            {
                Layers.Add(layer);
            }

            StatusText = $"加载完成：{CurrentDocument.EntityCount} 个实体，{CurrentDocument.Layers.Count} 个图层";

            _logger.LogInformation("成功加载DWG文件: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载DWG文件失败: {FilePath}", filePath);
            StatusText = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(Zoom * 1.25f, 100.0f);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(Zoom / 1.25f, 0.01f);
    }

    [RelayCommand]
    private void FitToView()
    {
        Zoom = 1.0f;
    }
}
