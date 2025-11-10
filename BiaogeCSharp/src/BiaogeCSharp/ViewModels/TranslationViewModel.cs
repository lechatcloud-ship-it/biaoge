using BiaogeCSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

/// <summary>
/// 翻译页面ViewModel - 核心DWG图纸翻译功能
/// </summary>
public partial class TranslationViewModel : ViewModelBase
{
    private readonly DwgTranslationService _dwgTranslationService;
    private readonly DocumentService _documentService;
    private readonly ILogger<TranslationViewModel> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    // 目标语言选项
    public ObservableCollection<LanguageOption> TargetLanguages { get; } = new()
    {
        new LanguageOption { Code = "zh", Name = "简体中文" },
        new LanguageOption { Code = "en", Name = "English" },
        new LanguageOption { Code = "ja", Name = "日本語" },
        new LanguageOption { Code = "ko", Name = "한국어" },
        new LanguageOption { Code = "fr", Name = "Français" },
        new LanguageOption { Code = "de", Name = "Deutsch" },
        new LanguageOption { Code = "es", Name = "Español" },
        new LanguageOption { Code = "ru", Name = "Русский" }
    };

    [ObservableProperty]
    private LanguageOption _selectedTargetLanguage;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private int _totalTexts;

    [ObservableProperty]
    private int _translatedTexts;

    [ObservableProperty]
    private double _cacheHitRate;

    [ObservableProperty]
    private TimeSpan _elapsedTime;

    [ObservableProperty]
    private ObservableCollection<TranslationPreviewItem> _previewItems = new();

    public TranslationViewModel(
        DwgTranslationService dwgTranslationService,
        DocumentService documentService,
        ILogger<TranslationViewModel> logger)
    {
        _dwgTranslationService = dwgTranslationService;
        _documentService = documentService;
        _logger = logger;

        // 默认选择简体中文
        _selectedTargetLanguage = TargetLanguages[0];
    }

    /// <summary>
    /// 开始翻译图纸
    /// </summary>
    [RelayCommand]
    private async Task StartTranslationAsync()
    {
        var currentDocument = _documentService.CurrentDocument;
        if (currentDocument == null)
        {
            _logger.LogWarning("没有打开的DWG文档");
            StatusText = "请先打开DWG图纸";
            return;
        }

        IsTranslating = true;
        Progress = 0;
        StatusText = "正在翻译图纸...";

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // 生成输出文件名
            var inputPath = currentDocument.FilePath;
            var directory = Path.GetDirectoryName(inputPath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            var extension = Path.GetExtension(inputPath);
            var outputPath = Path.Combine(directory, $"{fileName}_translated_{SelectedTargetLanguage.Code}{extension}");

            // 进度回调
            var progressReporter = new Progress<double>(p =>
            {
                Progress = p;
                StatusText = $"翻译中... {p:F0}%";
            });

            // 执行翻译
            var stats = await _dwgTranslationService.TranslateDwgAsync(
                inputPath,
                outputPath,
                SelectedTargetLanguage.Code,
                progressReporter,
                _cancellationTokenSource.Token
            );

            // 更新统计信息
            TotalTexts = stats.TotalTexts;
            TranslatedTexts = stats.TranslatedTexts;
            ElapsedTime = stats.Duration;

            StatusText = $"翻译完成！已保存至: {Path.GetFileName(outputPath)}";
            _logger.LogInformation(
                "翻译完成: {Translated}/{Total}条文本, 耗时{Duration:F2}秒",
                stats.TranslatedTexts,
                stats.TotalTexts,
                stats.Duration.TotalSeconds
            );
        }
        catch (OperationCanceledException)
        {
            StatusText = "翻译已取消";
            _logger.LogWarning("翻译被用户取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "翻译失败");
            StatusText = $"翻译失败: {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 取消翻译
    /// </summary>
    [RelayCommand]
    private void CancelTranslation()
    {
        _cancellationTokenSource?.Cancel();
        _logger.LogInformation("用户请求取消翻译");
    }

    /// <summary>
    /// 预览翻译（不保存文件）
    /// </summary>
    [RelayCommand]
    private async Task PreviewTranslationAsync()
    {
        var currentDocument = _documentService.CurrentDocument;
        if (currentDocument == null)
        {
            _logger.LogWarning("没有打开的DWG文档");
            StatusText = "请先打开DWG图纸";
            return;
        }

        IsTranslating = true;
        StatusText = "正在预览翻译...";
        PreviewItems.Clear();

        try
        {
            var translations = await _dwgTranslationService.PreviewTranslationAsync(
                currentDocument.FilePath,
                SelectedTargetLanguage.Code
            );

            // 显示预览结果
            foreach (var (original, translated) in translations.Take(100)) // 最多显示100条
            {
                PreviewItems.Add(new TranslationPreviewItem
                {
                    OriginalText = original,
                    TranslatedText = translated
                });
            }

            TotalTexts = translations.Count;
            StatusText = $"预览完成: {translations.Count}条翻译";
            _logger.LogInformation("预览翻译: {Count}条", translations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览翻译失败");
            StatusText = $"预览失败: {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    /// <summary>
    /// 清空翻译缓存
    /// </summary>
    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            await _dwgTranslationService.ClearCacheAsync();
            StatusText = "缓存已清空";
            _logger.LogInformation("翻译缓存已清空");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空缓存失败");
            StatusText = $"清空缓存失败: {ex.Message}";
        }
    }
}

/// <summary>
/// 语言选项
/// </summary>
public class LanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 翻译预览项
/// </summary>
public class TranslationPreviewItem
{
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
}
