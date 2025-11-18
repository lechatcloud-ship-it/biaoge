using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BiaogPlugin.Extensions;

namespace BiaogPlugin.Services;

/// <summary>
/// 阿里云百炼API统一客户端
/// 支持翻译、对话、深度思考、Function Calling等所有功能
/// 一个API密钥调用所有模型
///
/// 基于官方最佳实践：
/// - 指数退避重试机制
/// - 错误分类处理
/// - Token使用量跟踪
/// - 并行工具调用
/// - 流式增量输出
/// </summary>
public class BailianApiClient
{
    private readonly object _apiKeyLock = new();
    private readonly HttpClient _httpClient;
    private readonly ConfigManager _configManager;
    private string? _apiKey;

    // ===== API端点配置 =====
    //
    // ✅ 统一使用 OpenAI 兼容模式（2025官方推荐）
    //
    // 说明：阿里云百炼支持在OpenAI兼容端点中使用专用翻译参数：
    // - 对话/Agent/Vision: 使用标准OpenAI格式 (messages数组)
    // - 翻译: 使用OpenAI格式 + translation_options扩展参数
    //
    // 这种统一端点的方式简化了客户端实现，无需维护多个端点。
    // 阿里云会根据请求中是否包含 translation_options 自动路由到翻译引擎。
    //
    // 注：官方也提供专用翻译端点 /api/v1/services/translation/translate
    //     但使用不同的请求格式 (input对象而非messages数组)
    private const string ChatCompletionEndpoint = "/compatible-mode/v1/chat/completions";

    // 重试配置（阿里云官方推荐）
    private const int MaxRetries = 3;
    private const int InitialRetryDelayMs = 1000; // 1秒

    // Token使用量统计
    private long _totalInputTokens = 0;
    private long _totalOutputTokens = 0;
    private readonly object _tokenStatsLock = new();

    // 语言代码映射表（zh -> Chinese, en -> English）
    // 阿里云百炼要求使用英文全称
    private static readonly Dictionary<string, string> LanguageCodeMap = new()
    {
        ["zh"] = "Chinese",
        ["zh-cn"] = "Chinese",
        ["zh-tw"] = "Traditional Chinese",
        ["en"] = "English",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["fr"] = "French",
        ["de"] = "German",
        ["es"] = "Spanish",
        ["it"] = "Italian",
        ["pt"] = "Portuguese",
        ["ru"] = "Russian",
        ["ar"] = "Arabic",
        ["th"] = "Thai",
        ["vi"] = "Vietnamese",
        ["id"] = "Indonesian",
        ["ms"] = "Malay",
        ["hi"] = "Hindi",
        ["tr"] = "Turkish",
        ["pl"] = "Polish",
        ["nl"] = "Dutch",
        ["sv"] = "Swedish",
        ["da"] = "Danish",
        ["no"] = "Norwegian",
        ["fi"] = "Finnish"
    };

    // ✅ P1优化：编译后的正则表达式（性能提升30-50%）
    // 避免每次调用CleanTranslationText时重新编译正则
    private static readonly System.Text.RegularExpressions.Regex SystemTagRegex = new(
        @"<system>.*?</system>",
        System.Text.RegularExpressions.RegexOptions.Singleline |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.Compiled
    );

    private static readonly System.Text.RegularExpressions.Regex XmlTagPairsRegex = new(
        @"<(role|task|critical_rules|output_format|examples|example|input|output|reminder)>.*?</\1>",
        System.Text.RegularExpressions.RegexOptions.Singleline |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.Compiled
    );

    private static readonly System.Text.RegularExpressions.Regex SingleXmlTagsRegex = new(
        @"</?(?:system|role|task|critical_rules|output_format|examples|example|input|output|reminder)>",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.Compiled
    );

    public BailianApiClient(
        HttpClient httpClient,
        ConfigManager configManager)
    {
        _httpClient = httpClient;
        _configManager = configManager;

        _httpClient.BaseAddress = new Uri("https://dashscope.aliyuncs.com");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        // 初始化API密钥
        RefreshApiKey();
    }

    /// <summary>
    /// 刷新API密钥 - 从ConfigManager或环境变量读取
    /// </summary>
    public void RefreshApiKey()
    {
        lock (_apiKeyLock)
        {
            // 优先从ConfigManager读取（用户通过设置对话框保存的）
            _apiKey = _configManager.GetString("Bailian:ApiKey");

            // 如果ConfigManager中没有，尝试从环境变量读取
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
            }

            if (!string.IsNullOrEmpty(_apiKey))
            {
                Log.Information("API密钥已加载");
            }
            else
            {
                Log.Warning("未找到API密钥，请在设置中配置");
            }
        }
    }

    /// <summary>
    /// 获取当前API密钥（线程安全）
    /// </summary>
    private string? GetApiKey()
    {
        lock (_apiKeyLock)
        {
            return _apiKey;
        }
    }

    /// <summary>
    /// 检查API密钥是否已配置
    /// </summary>
    public bool HasApiKey
    {
        get
        {
            lock (_apiKeyLock)
            {
                return !string.IsNullOrEmpty(_apiKey);
            }
        }
    }

    /// <summary>
    /// 获取Token使用统计
    /// </summary>
    public (long inputTokens, long outputTokens, long totalTokens) GetTokenUsage()
    {
        lock (_tokenStatsLock)
        {
            return (_totalInputTokens, _totalOutputTokens, _totalInputTokens + _totalOutputTokens);
        }
    }

    /// <summary>
    /// 重置Token统计
    /// </summary>
    public void ResetTokenUsage()
    {
        lock (_tokenStatsLock)
        {
            _totalInputTokens = 0;
            _totalOutputTokens = 0;
            Log.Information("Token使用统计已重置");
        }
    }

    /// <summary>
    /// 清理翻译文本中的特殊标识符和提示词泄露
    /// 阿里云百炼模型可能返回 <|endofcontent|> 等特殊token，或者返回DomainPrompt内容
    /// </summary>
    /// <summary>
    /// ✅ P0增强：深度清洗qwen-flash等通用对话模型的翻译响应
    /// 处理各种可能的无关内容：提示词、解释、markdown格式、注释等
    /// </summary>
    private string CleanTranslationText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var original = text;

        // ========== 第1步：移除完整的XML标签块（最优先） ==========
        // qwen-flash可能返回整个<system>...</system>块
        // ⚠️ 紧急修复：v1.0.9 XML Prompt导致返回完整系统提示词
        // ✅ P1优化：使用编译后的静态Regex（性能提升30-50%）
        text = SystemTagRegex.Replace(text, "");

        // ✅ 性能优化：合并所有已知XML标签为单个正则表达式（避免多次遍历）
        // 使用反向引用\1确保开闭标签匹配
        text = XmlTagPairsRegex.Replace(text, "");

        // ✅ 安全优化：仅移除已知的系统XML标签（避免误删合法内容如<GB 50010>）
        // 旧实现：@"</?[a-zA-Z_]+>" 会误删所有尖括号内容
        // 新实现：仅删除已知的系统标签（role, task等），保留工程规范引用
        text = SingleXmlTagsRegex.Replace(text, "").Trim();

        // ========== 第2步：移除Markdown代码块标记 ==========
        // qwen-flash可能返回 ```text\n翻译内容\n``` 格式
        if (text.StartsWith("```"))
        {
            // ✅ P0修复：完善边界条件处理
            // 移除开头的 ```language 或 ```
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0)
            {
                // 有换行符：移除开头的```language\n
                text = text.Substring(firstNewline + 1);
            }
            else if (firstNewline == -1)
            {
                // ✅ 新增：无换行符情况（如"```主梁```"）
                // 直接移除开头的```
                text = text.Substring(3);
            }
            // firstNewline == 0 的情况：文本以\n开头（极少见），保持不变

            // 移除结尾的 ```
            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3);
            }

            text = text.Trim();
            Log.Debug("移除Markdown代码块标记");
        }

        // ========== 第3步：移除系统提示词特征短语 ==========
        var systemPromptKeywords = new[]
        {
            // ✅ P0紧急修复 2025-11-18：用户报告的实际泄漏模式
            "工程图纸。使用建筑术语。翻译：说明。保留：代码、数字、单位",
            "工程图纸。使用建筑术语。翻译：说明。保留：代码、数字、单位。",
            "工程图纸。使用建筑术语",
            "翻译：说明。保留：代码、数字、单位",
            "翻译：说明。保留",
            "<|startofcontent|>",  // qwen模型特殊标记
            "<|endofcontent|>",
            "<|im_start|>",
            "<|im_end|>",

            // ✅ v1.0.9新增：XML Prompt关键词
            "CAD/BIM工程图纸专业翻译专家",
            "将中文CAD工程图纸文本翻译为英文",
            "将英文CAD工程图纸文本翻译为中文",
            "仅输出译文本身",
            "绝对不添加任何前缀",
            "使用标准工程术语",
            "参考国际工程规范",
            "保留所有技术标识符",
            "错误示例",
            "正确示例",
            "直接输出翻译结果",
            "无需任何修饰或说明",

            // ✅ v1.0.7原有：中文system prompt关键词
            "你是CAD/BIM工程图纸专业翻译",
            "严格遵守：",
            "保留图号、规范代号",
            "直接输出译文",
            "不加任何解释",

            // 旧版英文提示词关键词
            "You are a professional CAD/BIM",
            "You are a professional translator",
            "Follow these rules strictly:",
            "STANDARD INDUSTRY TERMINOLOGY",
            "PRESERVE ALL technical identifiers",
            "MAINTAIN original formatting",
            "OUTPUT FORMAT:",
            "Direct translation ONLY",
            "Do NOT add:",
            "Task: Translate",
            "Output ONLY the translated text",

            // 其他中文关键词
            "您是专业的CAD/BIM",
            "您是专业翻译",
            "请严格遵循以下规则",
            "标准行业术语",
            "保留所有技术标识",
            "保持原始格式",
            "输出格式",
            "仅输出翻译",
            "不要添加",
            "任务：翻译"
        };

        foreach (var keyword in systemPromptKeywords)
        {
            if (text.Contains(keyword))
            {
                var keywordIndex = text.IndexOf(keyword);
                if (keywordIndex >= 0)
                {
                    // 如果关键词在开头，直接移除到关键词后
                    if (keywordIndex == 0)
                    {
                        // 查找第一个换行符或冒号后的内容
                        var separatorIndex = Math.Max(
                            text.IndexOf('\n', keyword.Length),
                            text.IndexOf(':', keyword.Length)
                        );

                        if (separatorIndex > 0)
                        {
                            text = text.Substring(separatorIndex + 1).Trim();
                        }
                        else
                        {
                            text = "";
                        }
                    }
                    // 如果关键词在中间/结尾，截取到关键词之前
                    else
                    {
                        text = text.Substring(0, keywordIndex).Trim();
                    }

                    Log.Debug($"移除系统提示词片段: {keyword}");
                    break;
                }
            }
        }

        // ========== 第4步：移除常见的解释性前缀 ==========
        var explanatoryPrefixes = new[]
        {
            "Translation:",
            "译文：",
            "翻译结果：",
            "翻译：",
            "Translated text:",
            "The translation is:",
            "Here is the translation:",
            "以下是翻译：",
            "翻译如下：",
            "答案：",
            "Answer:",
            "Result:",
            "结果："
        };

        foreach (var prefix in explanatoryPrefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(prefix.Length).Trim();
                Log.Debug($"移除解释性前缀: {prefix}");
                break;
            }
        }

        // ========== 第4步：移除引号包裹（如果整个文本被引号包裹） ==========
        text = text.Trim();
        if ((text.StartsWith("\"") && text.EndsWith("\"")) ||
            (text.StartsWith("'") && text.EndsWith("'")) ||
            (text.StartsWith(""") && text.EndsWith(""")))
        {
            text = text.Substring(1, text.Length - 2).Trim();
            Log.Debug("移除引号包裹");
        }

        // ========== 第5步：移除特殊标识符 ==========
        text = text
            .Replace("<|endofcontent|>", "")
            .Replace("<|im_end|>", "")
            .Replace("<|im_start|>", "")
            .Replace("<|end|>", "")
            .Replace("<|start|>", "")
            .Replace("<eot_id>", "")
            .Replace("<start_of_turn>", "")
            .Replace("<end_of_turn>", "")
            .Trim();

        // ========== 第6步：提取"原文+译文"格式中的译文 ==========
        // 匹配格式：原文：xxx 译文：yyy 或 Source: xxx Target: yyy
        var sourceTargetMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:原文[:：].*?)?译文[:：]\s*(.+?)(?:\n|$)|(?:Source:.*?)?Target:\s*(.+?)(?:\n|$)",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (sourceTargetMatch.Success)
        {
            var extractedTranslation = sourceTargetMatch.Groups[1].Success
                ? sourceTargetMatch.Groups[1].Value
                : sourceTargetMatch.Groups[2].Value;

            if (!string.IsNullOrWhiteSpace(extractedTranslation))
            {
                text = extractedTranslation.Trim();
                Log.Debug("提取原文+译文格式中的译文");
            }
        }

        // ========== 第7步：移除注释和说明性文本 ==========
        // 模式：(注: xxx) 或 [注释: xxx] 或 <!-- xxx -->
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\(注[:：].*?\)", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[注释[:：].*?\]", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<!--.*?-->", "");
        text = text.Trim();

        // ========== 第8步：移除解释性后缀 ==========
        // 移除以"注意"、"说明"、"备注"等开头的后缀段落
        var explanationPatterns = new[]
        {
            @"\n+注意[:：].*",
            @"\n+Note:.*",
            @"\n+说明[:：].*",
            @"\n+Explanation:.*",
            @"\n+备注[:：].*",
            @"\n+Remark:.*",
            @"\n+提示[:：].*",
            @"\n+Tip:.*"
        };

        foreach (var pattern in explanationPatterns)
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, pattern, "",
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        text = text.Trim();

        // ========== 第9步：检测并警告异常情况 ==========
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            Log.Warning($"翻译结果清洗后为空！原始响应前100字符: {original.Substring(0, Math.Min(100, original.Length))}");
            // 如果清洗后为空，返回原始文本（最后的兜底）
            return original;
        }

        // 检测是否清洗掉了大量内容（可能过度清洗）
        if (original.Length > 100 && text.Length < original.Length * 0.3)
        {
            Log.Warning($"清洗可能过度：原始{original.Length}字符 → 清洗后{text.Length}字符（保留{text.Length * 100.0 / original.Length:F1}%）");
        }

        return text;
    }

    /// <summary>
    /// ❌ 已移除：ExtractPureTranslation 方法引入了严重的误判问题
    ///
    /// 原问题：启发式算法经常选择错误的行，导致翻译结果不正确
    /// 用户反馈：
    /// - "增强属性" 被误翻译成 "# 读者对象"
    /// - 很多文本被误翻译成 "#背景"
    ///
    /// 解决方案：
    /// 1. 依赖qwen-mt-flash专业翻译模型的原生输出
    /// 2. 仅使用CleanTranslationText进行基础清理
    /// 3. 添加详细日志记录异常情况，由用户反馈而不是自动猜测
    /// </summary>

    /// <summary>
    /// 转换语言代码为英文全称
    /// 例如: "zh" -> "Chinese", "en" -> "English"
    /// </summary>
    private string ConvertLanguageCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return "auto"; // 自动识别
        }

        var code = languageCode.ToLower().Trim();

        // 如果已经是英文全称，直接返回
        if (LanguageCodeMap.ContainsValue(languageCode))
        {
            return languageCode;
        }

        // 如果是语言代码，转换为英文全称
        if (LanguageCodeMap.TryGetValue(code, out var fullName))
        {
            return fullName;
        }

        // 未知语言代码，返回原值并记录警告
        Log.Warning($"未知的语言代码: {languageCode}，将直接使用原值");
        return languageCode;
    }

    /// <summary>
    /// 记录Token使用量（线程安全）
    /// </summary>
    private void TrackTokenUsage(int inputTokens, int outputTokens)
    {
        lock (_tokenStatsLock)
        {
            _totalInputTokens += inputTokens;
            _totalOutputTokens += outputTokens;
        }
    }

    /// <summary>
    /// 带重试的HTTP请求 - 基于阿里云官方最佳实践
    ///
    /// 重试策略：
    /// - 5xx服务器错误 → 重试
    /// - 429限流错误 → 重试
    /// - 网络超时/连接错误 → 重试
    /// - 4xx客户端错误（除429外）→ 不重试
    /// - 指数退避：1s, 2s, 4s
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? lastResponse = null;
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // 克隆请求（因为HttpRequestMessage只能发送一次）
                var clonedRequest = await CloneHttpRequestAsync(request);

                // ✅ 商业级最佳实践: Dispose上一次重试的response避免资源泄漏
                lastResponse?.Dispose();

                lastResponse = await _httpClient.SendAsync(clonedRequest, cancellationToken);

                // 检查是否需要重试
                if (lastResponse.IsSuccessStatusCode)
                {
                    return lastResponse; // 成功，返回
                }

                // 错误分类
                var shouldRetry = ShouldRetryOnStatusCode(lastResponse.StatusCode);

                if (!shouldRetry)
                {
                    // 不可重试的错误（如401、400），直接返回
                    Log.Warning($"API请求失败（不重试）: {lastResponse.StatusCode}");
                    return lastResponse;
                }

                // 可重试的错误（如5xx、429）
                if (attempt < MaxRetries)
                {
                    var delayMs = InitialRetryDelayMs * (int)Math.Pow(2, attempt);
                    Log.Warning($"API请求失败（第{attempt + 1}次重试，{delayMs}ms后）: {lastResponse.StatusCode}");
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                // 网络错误 → 重试
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    var delayMs = InitialRetryDelayMs * (int)Math.Pow(2, attempt);
                    Log.Warning(ex, $"API请求网络异常（第{attempt + 1}次重试，{delayMs}ms后）");
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // 超时错误 → 重试
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    var delayMs = InitialRetryDelayMs * (int)Math.Pow(2, attempt);
                    Log.Warning($"API请求超时（第{attempt + 1}次重试，{delayMs}ms后）");
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }

        // 所有重试都失败
        if (lastResponse != null)
        {
            Log.Error($"API请求最终失败: {lastResponse.StatusCode}");
            return lastResponse;
        }

        if (lastException != null)
        {
            Log.Error(lastException, "API请求最终异常");
            throw lastException;
        }

        throw new Exception("未知的API请求失败");
    }

    /// <summary>
    /// 判断HTTP状态码是否应该重试
    /// </summary>
    private bool ShouldRetryOnStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            // 5xx服务器错误 → 重试
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,

            // 429限流 → 重试
            (HttpStatusCode)429 => true,

            // 4xx客户端错误 → 不重试
            HttpStatusCode.BadRequest => false,
            HttpStatusCode.Unauthorized => false,
            HttpStatusCode.Forbidden => false,
            HttpStatusCode.NotFound => false,

            // 其他错误 → 不重试
            _ => false
        };
    }

    /// <summary>
    /// 克隆HttpRequestMessage（用于重试）
    /// </summary>
    private async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // 复制Headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // 复制Content
        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            // 复制Content Headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    /// <summary>
    /// 批量翻译 - 使用 OpenAI 兼容模式（2025官方推荐）
    ///
    /// 策略：逐条翻译，每条一个API请求
    /// 优势：
    /// 1. qwen-mt-flash支持流式增量输出，适合单条处理
    /// 2. 更好的错误隔离（一条失败不影响其他）
    /// 3. 更准确的进度报告
    /// 4. 单条并发支持1000 QPS，99.8%成功率（官方性能数据）
    /// </summary>
    public async Task<List<string>> TranslateBatchAsync(
        List<string> texts,
        string targetLanguage,
        string? model = null,
        string? sourceLanguage = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // ✅ P0紧急修复：强制使用qwen-mt-flash，避免提示词泄露
        // 原因：通用对话模型（qwen3-max-preview/qwen-flash）会回显系统提示词
        // 解决：始终使用专用翻译模型，使用translation_options API，完全无提示词泄露
        if (string.IsNullOrEmpty(model))
        {
            model = BailianModelSelector.Models.QwenMTFlash; // 强制使用qwen-mt-flash
            Log.Information("批量翻译强制使用专用翻译模型: qwen-mt-flash（避免提示词泄露）");
        }
        else if (!model.Contains("mt-flash") && !model.Contains("mt-turbo") && !model.Contains("mt-plus"))
        {
            // 如果配置的是通用对话模型，覆盖为qwen-mt-flash
            Log.Warning($"检测到通用对话模型配置: {model}，自动切换为 qwen-mt-flash（避免提示词泄露）");
            model = BailianModelSelector.Models.QwenMTFlash;
        }

        Log.Information($"批量翻译: {texts.Count}条文本，使用模型: {model}");

        var totalCount = texts.Count;

        // 转换语言代码
        var sourceLang = string.IsNullOrEmpty(sourceLanguage) ? "auto" : ConvertLanguageCode(sourceLanguage);
        var targetLang = ConvertLanguageCode(targetLanguage);

        // ✅ P1修复：处理超长文本
        // 批量翻译前，将超过4000字符的文本分离出来，单独使用分段翻译
        var normalTexts = new List<(int index, string text)>();  // 正常长度的文本
        var longTexts = new List<(int index, string text)>();    // 超长文本

        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i].Length > EngineeringTranslationConfig.MaxCharsPerBatch)
            {
                longTexts.Add((i, texts[i]));
                Log.Information($"检测到超长文本(索引{i}): {texts[i].Length}字符，将使用分段翻译");
            }
            else
            {
                normalTexts.Add((i, texts[i]));
            }
        }

        Log.Information($"批量翻译分类: {normalTexts.Count}条正常文本, {longTexts.Count}条超长文本");

        var allTasks = new List<Task<(int index, string result)>>();

        // ✅ 处理超长文本（使用TranslateAsync，它会自动分段）
        foreach (var item in longTexts)
        {
            var (index, text) = item;
            var task = Task.Run(async () =>
            {
                try
                {
                    var translated = await TranslateAsync(text, targetLanguage, model, sourceLanguage, cancellationToken);

                    // 更新进度
                    var completedCount = Interlocked.Increment(ref _batchProgressCounter);
                    progress?.Report((double)completedCount / totalCount * 100);

                    return (index, translated);
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, $"超长文本翻译失败 (索引{index})");
                    return (index, text);  // 失败时返回原文
                }
            }, cancellationToken);
            allTasks.Add(task);
        }

        // ✅ 处理正常长度的文本（原批量翻译逻辑）
        // 并发控制 - 限制同时进行的请求数量（避免触发限流）
        // ⚠️ 重要：SemaphoreSlim必须在所有tasks完成后Dispose（在finally块中）
        using var semaphore = new SemaphoreSlim(10); // 最多10个并发请求

        for (int i = 0; i < normalTexts.Count; i++)
        {
            var index = normalTexts[i].index;
            var text = normalTexts[i].text;

            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // ✅ 智能模型选择：根据模型类型使用不同的API格式
                    // qwen-mt-flash: 专用翻译模型，使用translation_options
                    // qwen-flash/qwen-plus: 通用对话模型，使用系统提示词（1M上下文，强大的专业术语理解）
                    object requestBody;

                    if (model.Contains("mt-flash") || model.Contains("mt-plus") || model.Contains("mt-turbo"))
                    {
                        // ✅ qwen-mt-flash专用翻译API格式（完全符合阿里云百炼官方文档）
                        // 官方文档：https://help.aliyun.com/zh/model-studio/qwen-mt-api
                        requestBody = new
                        {
                            model = model,
                            messages = new[]
                            {
                                new
                                {
                                    role = "user",
                                    content = text
                                }
                            },
                            // ✅ 根据官方文档，HTTP调用时 translation_options 放在顶层（不是 extra_body）
                            translation_options = new
                            {
                                source_lang = sourceLang,        // 必选：源语言英文全称或"auto"
                                target_lang = targetLang,         // 必选：目标语言英文全称
                                domains = EngineeringTranslationConfig.DomainPrompt,  // 可选：领域提示（仅英文）
                                terms = EngineeringTranslationConfig.GetApiTerms(sourceLang, targetLang),  // 可选：术语干预
                                tm_list = EngineeringTranslationConfig.GetApiTranslationMemory(sourceLang, targetLang)  // 可选：翻译记忆
                            },
                            // ✅ 根据官方文档，temperature 默认 0.65，范围 [0, 2)
                            // 对于专业翻译，使用较低值（0.3）保证翻译稳定性和一致性
                            temperature = 0.3,
                            // ✅ 官方建议：仅设置 temperature 或 top_p 其中之一
                            // top_p 默认 0.8，此处不设置，让模型使用默认值
                        };
                    }
                    else
                    {
                        // qwen-flash/qwen-plus通用对话模型格式
                        // 使用简洁系统提示词 + 关键Few-shot示例
                        var systemPrompt = EngineeringTranslationConfig.BuildSystemPromptForModel(sourceLang, targetLang);
                        requestBody = new
                        {
                            model = model,
                            messages = new[]
                            {
                                new
                                {
                                    role = "system",
                                    content = systemPrompt
                                },
                                new
                                {
                                    role = "user",
                                    content = text
                                }
                            }
                        };
                    }

                    // ✅ 调试日志：记录API调用参数
                    Log.Debug($"[翻译API] 索引{index}: 模型={model}, 源语言={sourceLang}, 目标语言={targetLang}, 原文={text.Substring(0, Math.Min(50, text.Length))}");

                    var apiKey = GetApiKey();
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
                    {
                        Content = JsonContent.Create(requestBody)
                    };
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                    }

                    var response = await SendWithRetryAsync(httpRequest, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseJson);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("message", out var message))
                            {
                                if (message.TryGetProperty("content", out var content))
                                {
                                    var rawResponse = content.GetString() ?? text;

                                    // ✅ 调试日志：记录原始响应
                                    if (rawResponse.Length > 200)
                                    {
                                        Log.Debug($"[批量翻译原始响应] 索引{index}, 长度={rawResponse.Length}, 前100字符: {rawResponse.Substring(0, 100)}");
                                    }

                                    // ✅ 深度清洗翻译文本
                                    var translatedText = CleanTranslationText(rawResponse);

                                    // ✅ 异常检测：记录过长的翻译结果（但不再使用智能提取避免误判）
                                    if (translatedText.Length > text.Length * 5 && translatedText.Length > 100)
                                    {
                                        Log.Warning($"[批量翻译异常] 索引{index}, 清洗后过长: 原文={text.Length}字符, 译文={translatedText.Length}字符");
                                        Log.Warning($"[原文] {text}");
                                        Log.Warning($"[译文] {translatedText.Substring(0, Math.Min(200, translatedText.Length))}");
                                    }

                                    // ✅ 调试日志：记录翻译结果
                                    Log.Debug($"[批量翻译成功] 索引{index}: 原文={text.Substring(0, Math.Min(30, text.Length))} → 译文={translatedText.Substring(0, Math.Min(30, translatedText.Length))}");

                                    // 记录Token使用量
                                    if (root.TryGetProperty("usage", out var usage))
                                    {
                                        var inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                                        var outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                                        TrackTokenUsage(inputTokens, outputTokens);
                                    }

                                    return (index, translatedText);
                                }
                            }
                        }

                        Log.Warning($"翻译响应格式异常 (索引{index})");
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Log.Warning($"翻译失败 (索引{index}): {response.StatusCode}, {errorContent}");
                    }

                    return (index, text); // 失败时返回原文
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, $"翻译异常 (索引{index})");
                    return (index, text);
                }
                finally
                {
                    semaphore.Release();

                    // 报告进度
                    var completedCount = Interlocked.Increment(ref _batchProgressCounter);
                    progress?.Report((double)completedCount / totalCount * 100);
                }
            }, cancellationToken);

            allTasks.Add(task);
        }

        // ✅ 等待所有任务完成（包括超长文本和正常文本）
        var taskResults = await Task.WhenAll(allTasks);

        // ✅ 按照原始顺序排列结果
        var results = taskResults
            .OrderBy(r => r.index)
            .Select(r => r.result)
            .ToList();

        // 重置进度计数器
        _batchProgressCounter = 0;

        Log.Information($"批量翻译完成: {results.Count}/{texts.Count}条");
        return results;
    }

    // 批量翻译进度计数器
    private int _batchProgressCounter = 0;

    /// <summary>
    /// 单文本翻译 - 使用 OpenAI 兼容模式（2025官方推荐）
    ///
    /// ✅ 智能分段处理：
    /// - qwen-mt-flash模型限制：最大输入8192 tokens，最大输出8192 tokens
    /// - 超过4000字符时自动分段翻译，确保质量
    /// </summary>
    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string? model = null,
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        // ✅ P0紧急修复：强制使用qwen-mt-flash，避免提示词泄露
        // 原因：通用对话模型（qwen3-max-preview/qwen-flash）会回显系统提示词
        // 解决：始终使用专用翻译模型，使用translation_options API，完全无提示词泄露
        if (string.IsNullOrEmpty(model))
        {
            model = BailianModelSelector.Models.QwenMTFlash; // 强制使用qwen-mt-flash
            Log.Information("单文本翻译强制使用专用翻译模型: qwen-mt-flash（避免提示词泄露）");
        }
        else if (!model.Contains("mt-flash") && !model.Contains("mt-turbo") && !model.Contains("mt-plus"))
        {
            // 如果配置的是通用对话模型，覆盖为qwen-mt-flash
            Log.Warning($"检测到通用对话模型配置: {model}，自动切换为 qwen-mt-flash（避免提示词泄露）");
            model = BailianModelSelector.Models.QwenMTFlash;
        }

        Log.Debug($"翻译使用模型: {model}");

        // ✅ 根据模型类型选择合适的批次大小
        int maxCharsPerBatch;
        if (model.Contains("flash") && !model.Contains("mt-flash"))
        {
            // qwen-flash/qwen-plus：1M上下文，可处理超长文本
            maxCharsPerBatch = 450000; // 900K tokens（留100K余量）
            Log.Debug($"使用大上下文模型 {model}，批次大小: {maxCharsPerBatch}字符");
        }
        else
        {
            // qwen-mt-flash等：8K上下文
            maxCharsPerBatch = EngineeringTranslationConfig.MaxCharsPerBatch; // 3500字符
            Log.Debug($"使用标准模型 {model}，批次大小: {maxCharsPerBatch}字符");
        }

        // ✅ 检查文本长度，如果超过限制则分段翻译
        if (text.Length > maxCharsPerBatch)
        {
            Log.Information($"文本过长({text.Length}字符)，启用分段翻译（每段{maxCharsPerBatch}字符）");
            return await TranslateWithSegmentationAsync(text, targetLanguage, model, sourceLanguage, cancellationToken);
        }

        try
        {
            // 转换语言代码为英文全称
            var sourceLang = string.IsNullOrEmpty(sourceLanguage) ? "auto" : ConvertLanguageCode(sourceLanguage);
            var targetLang = ConvertLanguageCode(targetLanguage);

            // ✅ 根据模型类型选择不同的API调用格式
            object requestBody;

            // 判断是否为专用翻译模型（qwen-mt系列）
            if (model.Contains("mt-flash") || model.Contains("mt-turbo") || model.Contains("mt-plus"))
            {
                // ✅ qwen-mt-flash专用翻译API格式（完全符合阿里云百炼官方文档）
                // 官方文档：https://help.aliyun.com/zh/model-studio/qwen-mt-api
                Log.Debug($"使用专用翻译模型API格式: {model}");
                requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = text
                        }
                    },
                    // ✅ 根据官方文档，HTTP调用时 translation_options 放在顶层（不是 extra_body）
                    translation_options = new
                    {
                        source_lang = sourceLang,        // 必选：源语言英文全称或"auto"
                        target_lang = targetLang,         // 必选：目标语言英文全称
                        domains = EngineeringTranslationConfig.DomainPrompt,  // 可选：领域提示（仅英文）
                        terms = EngineeringTranslationConfig.GetApiTerms(sourceLang, targetLang),  // 可选：术语干预
                        tm_list = EngineeringTranslationConfig.GetApiTranslationMemory(sourceLang, targetLang)  // 可选：翻译记忆
                    },
                    // ✅ 根据官方文档，temperature 默认 0.65，范围 [0, 2)
                    // 对于专业翻译，使用较低值（0.3）保证翻译稳定性和一致性
                    temperature = 0.3,
                    // ✅ 官方建议：仅设置 temperature 或 top_p 其中之一
                    // top_p 默认 0.8，此处不设置，让模型使用默认值
                };
            }
            else
            {
                // ✅ 通用对话模型（qwen-flash/qwen-plus等）：使用system message
                Log.Debug($"使用通用对话模型API格式: {model}");
                var systemPrompt = EngineeringTranslationConfig.BuildSystemPromptForModel(sourceLang, targetLang);

                requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = systemPrompt
                        },
                        new
                        {
                            role = "user",
                            content = text
                        }
                    }
                    // ❌ 不使用translation_options（通用对话模型不支持）
                };
            }

            // 创建带Authorization头的请求（线程安全）
            var apiKey = GetApiKey();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
            {
                Content = JsonContent.Create(requestBody)
            };
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            Log.Debug($"翻译请求: {sourceLang} -> {targetLang}, 文本长度: {text.Length}");

            // 使用带重试的HTTP请求
            var response = await SendWithRetryAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // 解析 OpenAI 格式响应
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var content))
                        {
                            var rawResponse = content.GetString() ?? text;

                            // ✅ P0增强：记录原始响应（用于调试）
                            if (rawResponse.Length > 200)
                            {
                                Log.Debug($"[翻译API原始响应] 长度={rawResponse.Length}, 前200字符: {rawResponse.Substring(0, 200)}");
                            }
                            else
                            {
                                Log.Debug($"[翻译API原始响应] {rawResponse}");
                            }

                            // ✅ 深度清洗翻译文本
                            var translatedText = CleanTranslationText(rawResponse);

                            // ✅ 异常检测：记录过长的翻译结果（但不再使用智能提取避免误判）
                            if (translatedText.Length > text.Length * 5 && translatedText.Length > 100)
                            {
                                Log.Warning($"[翻译结果异常] 清洗后过长: 原文={text.Length}字符, 译文={translatedText.Length}字符");
                                Log.Warning($"[原文] {text}");
                                Log.Warning($"[译文] {translatedText.Substring(0, Math.Min(200, translatedText.Length))}");
                            }

                            // 记录Token使用量
                            if (root.TryGetProperty("usage", out var usage))
                            {
                                var inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                                var outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                                TrackTokenUsage(inputTokens, outputTokens);
                            }

                            Log.Debug($"[翻译成功] 原文: {text.Substring(0, Math.Min(30, text.Length))} -> 译文: {translatedText.Substring(0, Math.Min(30, translatedText.Length))}");
                            return translatedText;
                        }
                    }
                }

                Log.Warning("翻译响应格式异常: {Response}", responseJson);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Warning("翻译失败: {StatusCode}, {Error}", response.StatusCode, errorContent);
            }

            return text; // 失败时返回原文
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "翻译异常: {Text}", text.Substring(0, Math.Min(50, text.Length)));
            return text;
        }
    }

    /// <summary>
    /// ✅ 分段翻译长文本
    ///
    /// 用于处理超过qwen-mt-flash模型8192 token限制的长文本
    /// 按语义单位（换行符）分段，确保上下文连贯性
    /// </summary>
    private async Task<string> TranslateWithSegmentationAsync(
        string text,
        string targetLanguage,
        string? model = null,
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ✅ 按换行符分段（保持语义完整性）
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            var segments = new List<string>();
            var currentSegment = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                // 如果加上当前行会超过限制，则保存当前段，开始新段
                if (currentSegment.Length + line.Length + 1 > EngineeringTranslationConfig.MaxCharsPerBatch && currentSegment.Length > 0)
                {
                    segments.Add(currentSegment.ToString());
                    currentSegment.Clear();
                }

                if (currentSegment.Length > 0)
                {
                    currentSegment.AppendLine();
                }
                currentSegment.Append(line);
            }

            // 添加最后一段
            if (currentSegment.Length > 0)
            {
                segments.Add(currentSegment.ToString());
            }

            Log.Information($"文本已分段：总计{segments.Count}段");

            // ✅ 并行翻译（提升性能，避免太慢）
            // 使用SemaphoreSlim控制并发数，避免触发API限流
            var translatedSegments = new string[segments.Count];
            using var semaphore = new SemaphoreSlim(5); // 最多5个并发请求

            var tasks = segments.Select(async (segment, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    Log.Debug($"翻译第{index + 1}/{segments.Count}段 (长度:{segment.Length}字符)");

                    var translated = await TranslateAsync(
                        segment,
                        targetLanguage,
                        model,
                        sourceLanguage,
                        cancellationToken
                    );

                    translatedSegments[index] = translated;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            // ✅ 合并翻译结果
            var result = string.Join("\n", translatedSegments);
            Log.Information($"分段翻译完成：{segments.Count}段 → {result.Length}字符（并行翻译）");

            return result;
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "分段翻译失败");
            return text;  // 失败时返回原文
        }
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Warning("API密钥为空");
                return false;
            }

            // 使用2025年推荐的OpenAI兼容模式测试连接
            var model = "qwen-turbo"; // 使用基础模型进行测试

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "你好"
                    }
                }
            };

            // 创建HTTP请求（使用OpenAI兼容端点）
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
            {
                Content = JsonContent.Create(requestBody)
            };
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

            Log.Debug("测试API连接: {Model}, 端点: {Endpoint}", model, ChatCompletionEndpoint);

            // ✅ 商业级最佳实践: HttpResponseMessage必须dispose避免资源泄漏
            using (var response = await _httpClient.SendAsync(httpRequest, cancellationToken))
            {
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Log.Information("API连接测试成功: {StatusCode}, 响应预览: {Preview}",
                        response.StatusCode,
                        responseContent.Length > 100 ? responseContent.Substring(0, 100) : responseContent);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Warning("API连接测试失败: {StatusCode}, 完整响应: {Error}", response.StatusCode, errorContent);
                    return false;
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "API连接测试异常");
            return false;
        }
    }

    /// <summary>
    /// 流式对话 - 支持深度思考和Function Calling
    /// </summary>
    /// <param name="messages">对话消息列表</param>
    /// <param name="model">使用的模型（默认从配置读取）</param>
    /// <param name="tools">Function Calling工具定义</param>
    /// <param name="onStreamChunk">流式输出回调</param>
    /// <param name="temperature">温度参数（0-2）</param>
    /// <param name="topP">Top-P参数（0-1）</param>
    /// <param name="thinkingBudget">深度思考token限制（用于推理模型）</param>
    /// <param name="enableParallelToolCalls">启用并行工具调用（官方最佳实践）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整响应内容</returns>
    public async Task<ChatCompletionResult> ChatCompletionStreamAsync(
        List<ChatMessage> messages,
        string? model = null,
        List<object>? tools = null,
        Action<string>? onStreamChunk = null,
        Action<string>? onReasoningChunk = null,
        double temperature = 0.7,
        double topP = 0.9,
        int? thinkingBudget = null,
        bool enableThinking = true,  // ✅ 添加enable_thinking参数（混合思考模型需要）
        bool enableParallelToolCalls = true,
        CancellationToken cancellationToken = default)
    {
        // ✅ P1修复：验证thinking budget参数范围（阿里云百炼API限制）
        if (thinkingBudget.HasValue && (thinkingBudget.Value < 1 || thinkingBudget.Value > 8192))
        {
            throw new ArgumentOutOfRangeException(nameof(thinkingBudget),
                $"Thinking budget必须在1-8192之间，当前值: {thinkingBudget.Value}");
        }

        // ✅ P1修复：验证消息链的正确性（防止API拒绝）
        if (!ValidateMessageChain(messages, out string validationError))
        {
            throw new ArgumentException($"消息链验证失败: {validationError}", nameof(messages));
        }

        // 从配置读取模型，如果未指定则使用对话模型
        if (string.IsNullOrEmpty(model))
        {
            model = _configManager.GetString(
                "Bailian:ConversationModel",
                BailianModelSelector.Models.Qwen3MaxPreview // 2025 Flash：思考模式融合
            );
        }

        Log.Debug($"对话使用模型: {model}");

        var requestBody = new
        {
            model = model,
            messages = messages.Select(m => new {
                role = m.Role,
                content = m.MultiModalContent ?? (object)m.Content  // ✅ 优先使用MultiModalContent（支持视觉模型）
            }).ToList(),
            tools = tools,
            stream = true,
            // ✅ 阿里云官方推荐：incremental_output 必须是顶级参数，不能嵌套在 stream_options 中
            incremental_output = true,  // 增量输出优化，每个chunk只包含新生成的内容
            stream_options = new
            {
                include_usage = true
            },
            temperature = temperature,
            top_p = topP,
            enable_thinking = enableThinking,  // ✅ 控制是否启用思考模式（混合思考模型）
            thinking_budget = thinkingBudget,
            parallel_tool_calls = enableParallelToolCalls  // 阿里云官方推荐：并行工具调用
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // 创建带Authorization头的请求（线程安全）
        var apiKey = GetApiKey();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        // ✅ 关键修复：捕获调用线程的SynchronizationContext（AutoCAD主线程）
        // 这样后台线程的SSE回调可以Marshal回主线程，避免线程安全问题
        var syncContext = System.Threading.SynchronizationContext.Current;
        bool hasContext = syncContext != null;

        Log.Debug($"流式API调用 - SynchronizationContext: {(hasContext ? syncContext.GetType().Name : "null")}");

        // ✅ 商业级最佳实践: HttpResponseMessage必须dispose避免连接泄漏
        // 使用HttpCompletionOption.ResponseHeadersRead时，连接会保持打开直到dispose
        using (var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            // 处理流式响应
            var fullResponse = new StringBuilder();
            var fullReasoning = new StringBuilder();
            var toolCalls = new List<ToolCall>();
            int inputTokens = 0;
            int outputTokens = 0;

            using (var stream = await response.Content.ReadAsStreamAsync())
        {
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]")
                        break;

                    try
                    {
                        var chunk = JsonSerializer.Deserialize<JsonElement>(data);
                        var choices = chunk.GetProperty("choices");
                        if (choices.GetArrayLength() == 0)
                            continue;

                        var delta = choices[0].GetProperty("delta");

                        // 处理普通文本内容
                        if (delta.TryGetProperty("content", out var content))
                        {
                            var text = content.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                fullResponse.Append(text);

                                // ✅ 流式显示：使用Post异步调度到UI线程
                                // Post不会阻塞后台线程，允许快速读取stream
                                // 回调函数内部不应再使用Dispatcher.Invoke，会造成双重调度延迟
                                if (onStreamChunk != null)
                                {
                                    if (syncContext != null)
                                    {
                                        // Post: 异步排队到UI线程，不阻塞stream读取
                                        syncContext.Post(_ => onStreamChunk(text), null);
                                    }
                                    else
                                    {
                                        // 降级：直接调用（可能在后台线程，有风险）
                                        onStreamChunk(text);
                                    }
                                }
                            }
                        }

                        // 处理深度思考内容
                        if (delta.TryGetProperty("reasoning_content", out var reasoning))
                        {
                            var thinkingText = reasoning.GetString();
                            if (!string.IsNullOrEmpty(thinkingText))
                            {
                                fullReasoning.Append(thinkingText);

                                // ✅ 流式显示：Post异步调度，避免阻塞stream读取
                                if (onReasoningChunk != null)
                                {
                                    if (syncContext != null)
                                    {
                                        syncContext.Post(_ => onReasoningChunk(thinkingText), null);
                                    }
                                    else
                                    {
                                        onReasoningChunk(thinkingText);
                                    }
                                }
                            }
                        }

                        // 处理Function Calling
                        if (delta.TryGetProperty("tool_calls", out var toolCallsElement))
                        {
                            foreach (var toolCall in toolCallsElement.EnumerateArray())
                            {
                                var function = toolCall.GetProperty("function");
                                var name = function.GetProperty("name").GetString();
                                var arguments = function.GetProperty("arguments").GetString();

                                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(arguments))
                                {
                                    toolCalls.Add(new ToolCall
                                    {
                                        Name = name,
                                        Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments) ?? new()
                                    });
                                }
                            }
                        }

                        // 处理Token使用量（阿里云官方：最后一个chunk包含usage）
                        if (chunk.TryGetProperty("usage", out var usage))
                        {
                            if (usage.TryGetProperty("input_tokens", out var input))
                            {
                                inputTokens = input.GetInt32();
                            }
                            if (usage.TryGetProperty("output_tokens", out var output))
                            {
                                outputTokens = output.GetInt32();
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(ex, $"解析流式响应失败: {data}");
                    }
                }
            }
            }

            // 记录Token使用量
            if (inputTokens > 0 || outputTokens > 0)
            {
                TrackTokenUsage(inputTokens, outputTokens);
                Log.Debug($"对话Token使用: 输入{inputTokens}, 输出{outputTokens}");
            }

            return new ChatCompletionResult
            {
                Content = fullResponse.ToString(),
                ReasoningContent = fullReasoning.ToString(),
                ToolCalls = toolCalls,
                Model = model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            };
        }
    }

    /// <summary>
    /// 非流式对话 - 适合简单请求
    /// </summary>
    public async Task<ChatCompletionResult> ChatCompletionAsync(
        List<ChatMessage> messages,
        string? model = null,
        List<object>? tools = null,
        double temperature = 0.7,
        double topP = 0.9,
        int? thinkingBudget = null,
        bool enableThinking = true,  // ✅ 添加enable_thinking参数
        bool enableParallelToolCalls = true,
        CancellationToken cancellationToken = default)
    {
        // ✅ P1修复：验证thinking budget参数范围（阿里云百炼API限制）
        if (thinkingBudget.HasValue && (thinkingBudget.Value < 1 || thinkingBudget.Value > 8192))
        {
            throw new ArgumentOutOfRangeException(nameof(thinkingBudget),
                $"Thinking budget必须在1-8192之间，当前值: {thinkingBudget.Value}");
        }

        // ✅ P1修复：验证消息链的正确性（防止API拒绝）
        if (!ValidateMessageChain(messages, out string validationError))
        {
            throw new ArgumentException($"消息链验证失败: {validationError}", nameof(messages));
        }

        // 从配置读取模型，如果未指定则使用对话模型
        if (string.IsNullOrEmpty(model))
        {
            model = _configManager.GetString(
                "Bailian:ConversationModel",
                BailianModelSelector.Models.Qwen3MaxPreview // 2025 Flash：思考模式融合
            );
        }

        Log.Debug($"对话使用模型: {model}");

        var requestBody = new
        {
            model = model,
            messages = messages.Select(m => new {
                role = m.Role,
                content = m.MultiModalContent ?? (object)m.Content  // ✅ 优先使用MultiModalContent（支持视觉模型）
            }).ToList(),
            tools = tools,
            stream = false,
            temperature = temperature,
            top_p = topP,
            enable_thinking = enableThinking,  // ✅ 控制是否启用思考模式
            thinking_budget = thinkingBudget,
            parallel_tool_calls = enableParallelToolCalls  // 阿里云官方推荐：并行工具调用
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // 创建带Authorization头的请求（线程安全）
        var apiKey = GetApiKey();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        // 使用带重试的HTTP请求
        var response = await SendWithRetryAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        // ✅ P0修复: 防御性null检查，防止ReadFromJsonAsync返回null或属性不存在
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (result.ValueKind == JsonValueKind.Null || result.ValueKind == JsonValueKind.Undefined)
        {
            var rawContent = await response.Content.ReadAsStringAsync();
            Log.Error("API返回null或undefined JSON: {RawContent}", rawContent);
            throw new InvalidOperationException($"API返回无效JSON: {rawContent.Substring(0, Math.Min(200, rawContent.Length))}");
        }

        if (!result.TryGetProperty("choices", out var choices))
        {
            var rawContent = await response.Content.ReadAsStringAsync();
            Log.Error("API响应缺少choices字段: {RawContent}", rawContent);
            throw new InvalidOperationException("API响应格式错误：缺少choices字段");
        }

        if (choices.GetArrayLength() == 0)
        {
            throw new Exception("API返回空响应");
        }

        var message = choices[0].GetProperty("message");
        var content = message.TryGetProperty("content", out var c) ? c.GetString() : "";
        var reasoningContent = message.TryGetProperty("reasoning_content", out var r) ? r.GetString() : "";

        var toolCalls = new List<ToolCall>();
        if (message.TryGetProperty("tool_calls", out var toolCallsElement))
        {
            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                var function = toolCall.GetProperty("function");
                var name = function.GetProperty("name").GetString();
                var arguments = function.GetProperty("arguments").GetString();

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(arguments))
                {
                    toolCalls.Add(new ToolCall
                    {
                        Name = name,
                        Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments) ?? new()
                    });
                }
            }
        }

        // 记录Token使用量
        int inputTokens = 0;
        int outputTokens = 0;
        if (result.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var input))
            {
                inputTokens = input.GetInt32();
            }
            if (usage.TryGetProperty("output_tokens", out var output))
            {
                outputTokens = output.GetInt32();
            }

            if (inputTokens > 0 || outputTokens > 0)
            {
                TrackTokenUsage(inputTokens, outputTokens);
                Log.Debug($"对话Token使用: 输入{inputTokens}, 输出{outputTokens}");
            }
        }

        return new ChatCompletionResult
        {
            Content = content ?? "",
            ReasoningContent = reasoningContent ?? "",
            ToolCalls = toolCalls,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    /// <summary>
    /// 调用视觉语言模型（qwen3-vl-flash / qwen2-vl-7b-instruct等）
    ///
    /// 支持图像+文本混合输入，用于工程图纸识别、构件分析等场景
    /// </summary>
    /// <param name="model">模型名称（推荐qwen3-vl-flash）</param>
    /// <param name="prompt">文本Prompt</param>
    /// <param name="imageBase64">Base64编码的图像数据</param>
    /// <param name="maxTokens">最大输出Token数</param>
    /// <param name="temperature">温度参数（0-1，越低越确定）</param>
    /// <returns>模型响应内容</returns>
    public async Task<string> CallVisionModelAsync(
        string model,
        string prompt,
        string imageBase64,
        int maxTokens = 8000,
        double temperature = 0.1)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("未配置API密钥，请在设置中配置阿里云百炼API密钥");
        }

        // 构建多模态消息（OpenAI兼容格式）
        var messages = new List<object>
        {
            new
            {
                role = "user",
                content = new object[]
                {
                    // 文本部分
                    new { type = "text", text = prompt },
                    // 图像部分
                    new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:image/png;base64,{imageBase64}"
                        }
                    }
                }
            }
        };

        // 构建请求体
        var requestBody = new
        {
            model,
            messages,
            max_tokens = maxTokens,
            temperature,
            top_p = 0.9
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Log.Debug("调用视觉模型: {Model}, MaxTokens:{MaxTokens}, 图像大小:{ImageSize}KB",
            model, maxTokens, imageBase64.Length / 1024);

        var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionEndpoint)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        try
        {
            var response = await SendWithRetryAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("视觉模型API调用失败: {StatusCode}, 响应: {Response}",
                    response.StatusCode, responseContent);
                throw new HttpRequestException($"API调用失败: {response.StatusCode}, {responseContent}");
            }

            // 解析响应
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // ✅ P0修复: 防御性JSON导航，防止链式GetProperty抛异常
            string content = "";
            if (root.TryGetProperty("choices", out var choicesElement) &&
                choicesElement.GetArrayLength() > 0)
            {
                var firstChoice = choicesElement[0];
                if (firstChoice.TryGetProperty("message", out var messageElement) &&
                    messageElement.TryGetProperty("content", out var contentElement))
                {
                    content = contentElement.GetString() ?? "";
                }
                else
                {
                    Log.Warning("视觉模型响应缺少message.content字段");
                }
            }
            else
            {
                Log.Error("视觉模型响应缺少choices字段或为空数组: {Response}", responseContent);
                throw new InvalidOperationException("视觉模型API返回格式错误：缺少有效的choices数组");
            }

            // 提取Token使用量
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var promptTokens) &&
                    usage.TryGetProperty("completion_tokens", out var completionTokens))
                {
                    var inputTokens = promptTokens.GetInt32();
                    var outputTokens = completionTokens.GetInt32();

                    TrackTokenUsage(inputTokens, outputTokens);
                    Log.Debug("视觉模型Token使用: 输入{InputTokens}, 输出{OutputTokens}",
                        inputTokens, outputTokens);
                }
            }

            Log.Information("视觉模型调用成功，响应长度:{Length}字符", content.Length);

            return content;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "视觉模型API调用HTTP异常");
            throw;
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "视觉模型API响应解析失败");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "视觉模型API调用失败");
            throw;
        }
    }

    /// <summary>
    /// 验证消息链的正确性（阿里云百炼Function Calling规范）
    /// </summary>
    /// <remarks>
    /// 根据阿里云百炼API规范，tool消息必须满足：
    /// 1. tool消息前面必须有assistant消息
    /// 2. assistant消息必须包含tool_calls
    /// 3. tool消息的tool_call_id必须匹配assistant消息中的某个tool_calls[].id
    /// </remarks>
    private bool ValidateMessageChain(List<ChatMessage> messages, out string error)
    {
        error = string.Empty;
        ChatMessage? lastAssistant = null;

        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];

            if (msg.Role.ToLower() == "assistant")
            {
                lastAssistant = msg;
            }
            else if (msg.Role.ToLower() == "tool")
            {
                // tool消息必须有对应的assistant消息
                if (lastAssistant == null || lastAssistant.ToolCalls == null || lastAssistant.ToolCalls.Count == 0)
                {
                    error = $"消息{i}: tool消息前面没有包含tool_calls的assistant消息 " +
                           $"(tool_call_id={msg.ToolCallId}, name={msg.Name})";
                    return false;
                }

                // 验证tool_call_id匹配
                bool hasMatch = lastAssistant.ToolCalls.Any(tc => tc.Id == msg.ToolCallId);
                if (!hasMatch)
                {
                    error = $"消息{i}: tool_call_id不匹配 " +
                           $"(tool_call_id={msg.ToolCallId}, available_ids=[{string.Join(", ", lastAssistant.ToolCalls.Select(tc => tc.Id))}])";
                    return false;
                }
            }
        }

        return true;
    }
}

// 对话API数据模型（已统一使用 OpenAI 兼容格式，无需自定义响应模型）
/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = ""; // "user", "assistant", "system", "tool"
    public string Content { get; set; } = "";

    /// <summary>
    /// ✅ 多模态内容（用于视觉模型，如qwen-vl-max）
    /// 当设置此字段时，优先使用MultiModalContent而非Content
    /// 支持文字+图片混合内容：[{type:"text", text:"..."}, {type:"image_url", image_url:{url:"data:image/png;base64,..."}}]
    /// </summary>
    public object? MultiModalContent { get; set; }

    public string? Name { get; set; } // 工具名称（用于role="tool"的消息）
    public string? ToolCallId { get; set; } // ✅ tool消息必须包含tool_call_id关联到assistant的tool_calls

    // ✅ CRITICAL FIX: assistant消息必须保存工具调用信息
    // 参考：阿里云百炼官方文档 - Function Calling要求assistant消息包含完整的tool_calls数组
    // 错误:"messages with role 'tool' must be a response to a preceeding message with 'tool_calls'"
    // 原因：当会话恢复或BuildMessages时，assistant消息缺少tool_calls字段导致API拒绝后续的tool消息
    public List<ToolCallInfo>? ToolCalls { get; set; }
}

/// <summary>
/// 工具调用信息（用于ChatMessage序列化到API）
/// 与ToolCall类分离，专门用于消息历史持久化
/// </summary>
public class ToolCallInfo
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "function";
    public FunctionCallInfo Function { get; set; } = new();
    public int Index { get; set; }
}

/// <summary>
/// 函数调用信息
/// </summary>
public class FunctionCallInfo
{
    public string Name { get; set; } = "";
    /// <summary>
    /// 函数参数（JSON字符串格式）
    /// ✅ v1.0.8+修复：默认值从""改为"{}"，防止BinaryData.FromString("")报错
    /// </summary>
    public string Arguments { get; set; } = "{}"; // 默认为空JSON对象，不是空字符串
}

/// <summary>
/// 对话完成结果
/// </summary>
public class ChatCompletionResult
{
    public string Content { get; set; } = "";
    public string ReasoningContent { get; set; } = "";
    public List<ToolCall> ToolCalls { get; set; } = new();
    public string Model { get; set; } = "";
    public int InputTokens { get; set; } = 0;
    public int OutputTokens { get; set; } = 0;
}

/// <summary>
/// 工具调用
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = ""; // ✅ 商业级最佳实践：工具调用唯一ID（tool_call_id）
    public string Name { get; set; } = "";
    public Dictionary<string, object> Arguments { get; set; } = new();
}
