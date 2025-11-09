using BiaogeCSharp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

/// <summary>
/// 算量页面ViewModel
/// </summary>
public partial class CalculationViewModel : ViewModelBase
{
    private readonly ILogger<CalculationViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ComponentRecognitionResult> _results = new();

    [ObservableProperty]
    private bool _isRecognizing;

    [ObservableProperty]
    private int _totalComponents;

    [ObservableProperty]
    private int _validComponents;

    [ObservableProperty]
    private decimal _totalCost;

    [ObservableProperty]
    private string _recognitionMode = "超高精度识别 (99.9999%)";

    [ObservableProperty]
    private bool _showValidOnly = true;

    [ObservableProperty]
    private bool _showLowConfidence;

    [ObservableProperty]
    private bool _showValidationErrors;

    public CalculationViewModel(ILogger<CalculationViewModel> logger)
    {
        _logger = logger;
    }

    [RelayCommand]
    private async Task StartRecognitionAsync()
    {
        IsRecognizing = true;

        try
        {
            // TODO: 实现构件识别逻辑
            _logger.LogInformation("开始构件识别: 模式={Mode}", RecognitionMode);

            // 模拟识别结果
            await Task.Delay(1000);

            Results.Clear();
            Results.Add(new ComponentRecognitionResult
            {
                Type = "C30混凝土柱",
                Quantity = 12,
                Volume = 8.64,
                Area = 0,
                Cost = 4320.00m,
                Status = "有效",
                Confidence = 0.999999
            });

            UpdateStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构件识别失败");
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        // TODO: 实现生成报告逻辑
        _logger.LogInformation("生成算量报告");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        // TODO: 实现导出Excel逻辑
        _logger.LogInformation("导出到Excel");
        await Task.CompletedTask;
    }

    private void UpdateStatistics()
    {
        TotalComponents = Results.Count;
        ValidComponents = 0;
        TotalCost = 0;

        foreach (var result in Results)
        {
            if (result.Status == "有效")
                ValidComponents++;
            TotalCost += result.Cost;
        }
    }
}
