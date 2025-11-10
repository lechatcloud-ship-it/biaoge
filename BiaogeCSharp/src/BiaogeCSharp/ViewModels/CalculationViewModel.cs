using BiaogeCSharp.Models;
using BiaogeCSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

/// <summary>
/// 算量页面ViewModel
/// </summary>
public partial class CalculationViewModel : ViewModelBase
{
    private readonly ILogger<CalculationViewModel> _logger;
    private readonly ComponentRecognizer _componentRecognizer;
    private readonly ExcelExporter _excelExporter;
    private readonly DocumentService _documentService;

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

    public CalculationViewModel(
        ComponentRecognizer componentRecognizer,
        ExcelExporter excelExporter,
        DocumentService documentService,
        ILogger<CalculationViewModel> logger)
    {
        _componentRecognizer = componentRecognizer;
        _excelExporter = excelExporter;
        _documentService = documentService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task StartRecognitionAsync()
    {
        IsRecognizing = true;

        try
        {
            _logger.LogInformation("开始构件识别: 模式={Mode}", RecognitionMode);

            var currentDocument = _documentService.CurrentDocument;
            if (currentDocument == null)
            {
                _logger.LogWarning("没有打开的DWG文档");
                return;
            }

            // 使用AI验证取决于识别模式
            bool useAiVerification = RecognitionMode.Contains("超高精度");

            // 执行构件识别
            var recognitionResults = await _componentRecognizer.RecognizeFromDocumentAsync(
                currentDocument,
                useAiVerification
            );

            // 更新结果
            Results.Clear();
            foreach (var result in recognitionResults)
            {
                Results.Add(result);
            }

            UpdateStatistics();

            _logger.LogInformation("识别完成: {Count}个构件", Results.Count);
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
        _logger.LogInformation("生成算量报告");

        try
        {
            if (!Results.Any())
            {
                _logger.LogWarning("没有识别结果可生成报告");
                return;
            }

            // TODO: 实现报告生成（可以是PDF或其他格式）
            _logger.LogInformation("报告生成功能待实现");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成报告失败");
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        _logger.LogInformation("导出到Excel");

        try
        {
            if (!Results.Any())
            {
                _logger.LogWarning("没有识别结果可导出");
                return;
            }

            // 生成默认文件名
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = $"工程量清单_{timestamp}.xlsx";

            // 导出Excel
            await _excelExporter.ExportAsync(
                Results.ToList(),
                outputPath,
                includeDetails: true,
                includeConfidence: true,
                includeMaterials: true,
                includeCost: true
            );

            _logger.LogInformation("Excel导出完成: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出Excel失败");
        }
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
