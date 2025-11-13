using System;
using System.Collections.Generic;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// 翻译进度信息
    /// </summary>
    public class TranslationProgress
    {
        /// <summary>
        /// 当前阶段描述
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// 附加消息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 已处理数量
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// 总数量
        /// </summary>
        public int TotalCount { get; set; }

        public override string ToString()
        {
            return $"{Stage}: {Percentage}% ({ProcessedCount}/{TotalCount})";
        }
    }

    /// <summary>
    /// 翻译结果
    /// </summary>
    public class TranslationResult
    {
        /// <summary>
        /// 原文
        /// </summary>
        public string SourceText { get; set; } = string.Empty;

        /// <summary>
        /// 译文
        /// </summary>
        public string TranslatedText { get; set; } = string.Empty;

        /// <summary>
        /// 目标语言
        /// </summary>
        public string TargetLanguage { get; set; } = string.Empty;

        /// <summary>
        /// 是否从缓存获取
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// 翻译时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 质量评分（0-1）
        /// </summary>
        public double QualityScore { get; set; } = 1.0;

        public override string ToString()
        {
            return $"{SourceText} → {TranslatedText} ({TargetLanguage})";
        }
    }

    /// <summary>
    /// 批量翻译统计信息
    /// </summary>
    public class TranslationStatistics
    {
        /// <summary>
        /// 总文本数
        /// </summary>
        public int TotalTextCount { get; set; }

        /// <summary>
        /// 唯一文本数
        /// </summary>
        public int UniqueTextCount { get; set; }

        /// <summary>
        /// 缓存命中数
        /// </summary>
        public int CacheHitCount { get; set; }

        /// <summary>
        /// API调用次数
        /// </summary>
        public int ApiCallCount { get; set; }

        /// <summary>
        /// 翻译成功数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 翻译失败数
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 总耗时（秒）
        /// </summary>
        public double TotalSeconds { get; set; }

        /// <summary>
        /// 缓存命中率（百分比）
        /// </summary>
        public double CacheHitRate =>
            UniqueTextCount > 0 ? (double)CacheHitCount / UniqueTextCount * 100 : 0;

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate =>
            TotalTextCount > 0 ? (double)SuccessCount / TotalTextCount * 100 : 0;

        /// <summary>
        /// 平均速度（文本/秒）
        /// </summary>
        public double AverageSpeed =>
            TotalSeconds > 0 ? TotalTextCount / TotalSeconds : 0;

        public override string ToString()
        {
            return $"总计: {TotalTextCount}, " +
                   $"唯一: {UniqueTextCount}, " +
                   $"缓存命中: {CacheHitCount} ({CacheHitRate:F1}%), " +
                   $"API调用: {ApiCallCount}, " +
                   $"成功: {SuccessCount} ({SuccessRate:F1}%), " +
                   $"耗时: {TotalSeconds:F2}s, " +
                   $"速度: {AverageSpeed:F1} 文本/秒";
        }
    }

    /// <summary>
    /// 缓存条目
    /// </summary>
    public class CacheEntry
    {
        public long Id { get; set; }
        public string SourceText { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastAccessedAt { get; set; } = DateTime.Now;
        public int AccessCount { get; set; } = 1;
        public string Hash { get; set; } = string.Empty;
    }

    /// <summary>
    /// 支持的语言
    /// </summary>
    public static class SupportedLanguages
    {
        public static readonly List<LanguageOption> Languages = new List<LanguageOption>
        {
            new LanguageOption { Code = "en", Name = "英语", NativeName = "English" },
            new LanguageOption { Code = "ja", Name = "日语", NativeName = "日本語" },
            new LanguageOption { Code = "ko", Name = "韩语", NativeName = "한국어" },
            new LanguageOption { Code = "fr", Name = "法语", NativeName = "Français" },
            new LanguageOption { Code = "de", Name = "德语", NativeName = "Deutsch" },
            new LanguageOption { Code = "es", Name = "西班牙语", NativeName = "Español" },
            new LanguageOption { Code = "ru", Name = "俄语", NativeName = "Русский" },
            new LanguageOption { Code = "zh", Name = "中文", NativeName = "中文" }
        };

        public static LanguageOption? GetLanguage(string code)
        {
            return Languages.Find(l => l.Code == code);
        }
    }

    /// <summary>
    /// 语言选项
    /// </summary>
    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;

        public string DisplayName => $"{Name} ({NativeName})";

        public override string ToString() => DisplayName;
    }
}
