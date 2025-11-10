using BiaogeCSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiaogeCSharp.ViewModels;

public partial class TranslationViewModel : ViewModelBase
{
    private readonly TranslationEngine _translationEngine;
    private readonly ILogger<TranslationViewModel> _logger;

    [ObservableProperty]
    private string _targetLanguage = "en";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _statusText = "准备翻译";

    public TranslationViewModel(
        TranslationEngine translationEngine,
        ILogger<TranslationViewModel> logger)
    {
        _translationEngine = translationEngine;
        _logger = logger;
    }

    [RelayCommand]
    private async Task StartTranslationAsync()
    {
        IsTranslating = true;
        Progress = 0;
        StatusText = "正在翻译...";

        try
        {
            // TODO: 从当前文档提取文本
            var texts = new List<string> { "测试文本1", "测试文本2" };

            var progressReporter = new Progress<double>(p =>
            {
                Progress = p;
                StatusText = $"翻译中... {p:F1}%";
            });

            var results = await _translationEngine.TranslateBatchWithCacheAsync(
                texts,
                TargetLanguage,
                progress: progressReporter
            );

            StatusText = $"翻译完成：{results.Count} 条";
            _logger.LogInformation("翻译完成: {Count}", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "翻译失败");
            StatusText = $"翻译失败: {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
        }
    }
}
