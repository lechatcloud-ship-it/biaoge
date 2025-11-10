using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BiaogeCSharp.Services;
using System;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

/// <summary>
/// 导出页面ViewModel
/// </summary>
public partial class ExportViewModel : ViewModelBase
{
    private readonly ILogger<ExportViewModel> _logger;
    private readonly DocumentService _documentService;
    private readonly DwgExporter _dwgExporter;
    private readonly PdfExporter _pdfExporter;
    private readonly ExcelExporter _excelExporter;
    private readonly CalculationResultService _calculationResultService;

    [ObservableProperty]
    private string _dwgFormat = "DWG R2024";

    [ObservableProperty]
    private string _dwgEncoding = "UTF-8";

    [ObservableProperty]
    private string _dwgOutputPath = string.Empty;

    [ObservableProperty]
    private string _pdfPaperSize = "A0 (841×1189mm)";

    [ObservableProperty]
    private string _pdfQuality = "高质量 (150 DPI)";

    [ObservableProperty]
    private string _pdfOutputPath = string.Empty;

    [ObservableProperty]
    private bool _pdfEmbedFonts = true;

    [ObservableProperty]
    private string _excelTemplate = "标准工程量清单";

    [ObservableProperty]
    private string _excelOutputPath = string.Empty;

    [ObservableProperty]
    private bool _excelIncludeDetails = true;

    [ObservableProperty]
    private bool _excelIncludeConfidence = true;

    [ObservableProperty]
    private bool _excelIncludeMaterials = true;

    [ObservableProperty]
    private bool _excelIncludeCost;

    public ExportViewModel(
        ILogger<ExportViewModel> logger,
        DocumentService documentService,
        DwgExporter dwgExporter,
        PdfExporter pdfExporter,
        ExcelExporter excelExporter,
        CalculationResultService calculationResultService)
    {
        _logger = logger;
        _documentService = documentService;
        _dwgExporter = dwgExporter;
        _pdfExporter = pdfExporter;
        _excelExporter = excelExporter;
        _calculationResultService = calculationResultService;
    }

    [RelayCommand]
    private async Task ExportDwgAsync()
    {
        try
        {
            if (_documentService.CurrentDocument == null)
            {
                _logger.LogWarning("没有打开的文档");
                return;
            }

            if (string.IsNullOrEmpty(DwgOutputPath))
            {
                _logger.LogWarning("请选择输出路径");
                return;
            }

            _logger.LogInformation("导出DWG: 格式={Format}, 编码={Encoding}", DwgFormat, DwgEncoding);

            // 解析版本号
            var version = DwgFormat.Contains("R2024") ? "R2024" :
                         DwgFormat.Contains("R2018") ? "R2018" :
                         DwgFormat.Contains("R2013") ? "R2013" : "R2010";

            // 执行导出
            await _dwgExporter.ExportDwgAsync(
                _documentService.CurrentDocument,
                DwgOutputPath,
                version
            );

            _logger.LogInformation("DWG导出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DWG导出失败");
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        try
        {
            if (_documentService.CurrentDocument == null)
            {
                _logger.LogWarning("没有打开的文档");
                return;
            }

            if (string.IsNullOrEmpty(PdfOutputPath))
            {
                _logger.LogWarning("请选择输出路径");
                return;
            }

            _logger.LogInformation("导出PDF: 纸张={Paper}, 质量={Quality}", PdfPaperSize, PdfQuality);

            // 解析纸张大小
            var pageSize = PdfPaperSize.Contains("A0") ? "A0" :
                          PdfPaperSize.Contains("A1") ? "A1" :
                          PdfPaperSize.Contains("A2") ? "A2" :
                          PdfPaperSize.Contains("A3") ? "A3" : "A4";

            // 解析DPI
            var dpi = PdfQuality.Contains("300") ? 300 :
                     PdfQuality.Contains("150") ? 150 : 72;

            // 执行导出
            await _pdfExporter.ExportAsync(
                _documentService.CurrentDocument,
                PdfOutputPath,
                pageSize,
                dpi,
                PdfEmbedFonts
            );

            _logger.LogInformation("PDF导出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF导出失败");
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(ExcelOutputPath))
            {
                _logger.LogWarning("请选择输出路径");
                return;
            }

            // 检查是否有计算结果
            if (!_calculationResultService.HasResults)
            {
                _logger.LogWarning("没有可导出的算量结果，请先进行构件识别");
                return;
            }

            _logger.LogInformation("导出Excel: 模板={Template}, 构件数={Count}",
                ExcelTemplate, _calculationResultService.LatestResults.Count);

            // 从结果服务获取最新的计算结果
            var results = _calculationResultService.LatestResults.ToList();

            // 执行导出
            await _excelExporter.ExportAsync(
                results,
                ExcelOutputPath,
                ExcelIncludeDetails,
                ExcelIncludeConfidence,
                ExcelIncludeMaterials,
                ExcelIncludeCost
            );

            _logger.LogInformation("Excel导出成功: {Path}", ExcelOutputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel导出失败");
        }
    }

    [RelayCommand]
    private async Task BrowseDwgOutputAsync()
    {
        try
        {
            var mainWindow = App.Current.MainWindow;
            if (mainWindow == null) return;

            var storageProvider = mainWindow.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "保存DWG文件",
                DefaultExtension = "dwg",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("DWG文件")
                    {
                        Patterns = new[] { "*.dwg" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("DXF文件")
                    {
                        Patterns = new[] { "*.dxf" }
                    }
                }
            });

            if (file != null)
            {
                DwgOutputPath = file.Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择DWG输出路径失败");
        }
    }

    [RelayCommand]
    private async Task BrowsePdfOutputAsync()
    {
        try
        {
            var mainWindow = App.Current.MainWindow;
            if (mainWindow == null) return;

            var storageProvider = mainWindow.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "保存PDF文件",
                DefaultExtension = "pdf",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PDF文件")
                    {
                        Patterns = new[] { "*.pdf" }
                    }
                }
            });

            if (file != null)
            {
                PdfOutputPath = file.Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择PDF输出路径失败");
        }
    }

    [RelayCommand]
    private async Task BrowseExcelOutputAsync()
    {
        try
        {
            var mainWindow = App.Current.MainWindow;
            if (mainWindow == null) return;

            var storageProvider = mainWindow.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "保存Excel文件",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel文件")
                    {
                        Patterns = new[] { "*.xlsx" }
                    }
                }
            });

            if (file != null)
            {
                ExcelOutputPath = file.Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择Excel输出路径失败");
        }
    }
}
