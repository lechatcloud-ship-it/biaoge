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

    // API端点 - 统一使用 OpenAI 兼容模式（2025官方推荐）
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
    private string CleanTranslationText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // ✅ 修复严重Bug：移除API响应中可能包含的DomainPrompt提示词内容
        // 问题：有时API会将提示词内容包含在翻译结果中，导致图纸出现大段英文提示词
        // 解决：检测并移除所有DomainPrompt相关的内容

        // 1. 移除完整的DomainPrompt（如果API完整返回了提示词）
        if (text.Contains("This text is from AutoCAD engineering"))
        {
            // 尝试截取到提示词之前的部分
            var promptStart = text.IndexOf("This text is from AutoCAD engineering");
            if (promptStart > 0)
            {
                text = text.Substring(0, promptStart).Trim();
            }
            else
            {
                // 如果响应只包含提示词，返回空字符串
                text = "";
            }
        }

        // 2. 移除DomainPrompt中的关键短语（英文版本 - 部分泄露的情况）
        var promptKeyPhrasesEn = new[]
        {
            "This text is from AutoCAD",
            "IMPORTANT TRANSLATION RULES:",
            "PRESERVE: Drawing numbers",
            "TRANSLATE: Descriptive text",
            "Use official construction industry terminology",
            "Translate into professional construction engineering domain style",
            "architectural construction drawings",
            "structural engineering",
            "MEP systems"
        };

        foreach (var phrase in promptKeyPhrasesEn)
        {
            if (text.Contains(phrase))
            {
                var phraseStart = text.IndexOf(phrase);
                if (phraseStart > 0)
                {
                    // 截取到短语之前的部分
                    text = text.Substring(0, phraseStart).Trim();
                }
                else
                {
                    // 如果响应开头就是这些短语，清空
                    text = "";
                }
                break;
            }
        }

        // 3. ✅ 新增：移除DomainPrompt的中文翻译版本（用户报告：出现"文本来自AutoCAD工程与建筑施工图"）
        var promptKeyPhrasesZh = new[]
        {
            "文本来自AutoCAD",      // ✅ 用户确认的准确短语
            "本文来自AutoCAD",
            "这段文本来自",
            "此文本来自",
            "工程与建筑施工图",
            "工程和建筑施工图",
            "建筑施工图纸",
            "结构工程",
            "机电系统",
            "重要翻译规则",
            "保留：图纸编号",
            "翻译：描述性文本",
            "使用官方建筑行业术语",
            "翻译成专业建筑工程领域风格"
        };

        foreach (var phrase in promptKeyPhrasesZh)
        {
            if (text.Contains(phrase))
            {
                var phraseStart = text.IndexOf(phrase);
                if (phraseStart > 0)
                {
                    // 截取到短语之前的部分
                    text = text.Substring(0, phraseStart).Trim();
                }
                else
                {
                    // 如果响应开头就是这些短语，清空
                    text = "";
                }
                break;
            }
        }

        // 3. 移除常见的特殊标识符
        text = text
            .Replace("<|endofcontent|>", "")
            .Replace("<|im_end|>", "")
            .Replace("<|im_start|>", "")
            .Replace("<|end|>", "")
            .Replace("<|start|>", "")
            .Trim();

        // 4. 如果清理后为空或只剩下提示词片段，记录警告
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            Log.Warning("翻译结果清理后为空，可能是提示词泄露或API响应异常");
        }

        return text;
    }

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
        // 从配置读取模型，如果未指定则使用最优翻译模型
        if (string.IsNullOrEmpty(model))
        {
            model = _configManager.GetString(
                "Bailian:TextTranslationModel",
                BailianModelSelector.Models.QwenMTFlash // qwen-mt-flash：术语定制+格式还原
            );
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
        var semaphore = new SemaphoreSlim(10); // 最多10个并发请求

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
                    // qwen3-max-preview: 通用对话模型，使用系统提示词（256K上下文，更好的专业术语理解）
                    object requestBody;

                    if (model.Contains("mt-flash") || model.Contains("mt-plus"))
                    {
                        // qwen-mt-flash专用翻译API格式
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
                            translation_options = new  // ✅ 根级别，不是extra_body
                            {
                                source_lang = sourceLang,
                                target_lang = targetLang,
                                domains = EngineeringTranslationConfig.DomainPrompt,
                                terms = EngineeringTranslationConfig.GetApiTerms(sourceLang, targetLang)
                            }
                        };
                    }
                    else
                    {
                        // qwen3-max-preview通用对话模型格式
                        // 使用系统提示词 + 专业术语上下文
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
                                    var translatedText = content.GetString() ?? text;

                                    // ✅ 清理特殊标识符（如 <|endofcontent|>）
                                    translatedText = CleanTranslationText(translatedText);

                                    // ✅ 调试日志：记录翻译结果
                                    Log.Debug($"[翻译结果] 索引{index}: 原文={text.Substring(0, Math.Min(30, text.Length))} → 译文={translatedText.Substring(0, Math.Min(30, translatedText.Length))}");

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
        // 从配置读取模型，如果未指定则使用最优翻译模型
        if (string.IsNullOrEmpty(model))
        {
            model = _configManager.GetString(
                "Bailian:TextTranslationModel",
                BailianModelSelector.Models.QwenMTFlash // qwen-mt-flash：术语定制+格式还原
            );
        }

        Log.Debug($"翻译使用模型: {model}");

        // ✅ 检查文本长度，如果超过限制则分段翻译
        if (text.Length > EngineeringTranslationConfig.MaxCharsPerBatch)
        {
            Log.Information($"文本过长({text.Length}字符)，启用分段翻译（每段{EngineeringTranslationConfig.MaxCharsPerBatch}字符）");
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
                // ✅ 专用翻译模型：使用translation_options参数（OpenAI兼容模式顶层参数）
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
                    // ✅ 直接在顶层放置translation_options（OpenAI兼容接口）
                    translation_options = new
                    {
                        source_lang = sourceLang,
                        target_lang = targetLang,
                        domains = EngineeringTranslationConfig.DomainPrompt,
                        terms = EngineeringTranslationConfig.GetApiTerms(sourceLang, targetLang)
                    }
                };
            }
            else
            {
                // ✅ 通用对话模型（qwen3-max-preview等）：使用system message
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
                            var translatedText = content.GetString() ?? text;

                            // ✅ 清理特殊标识符（如 <|endofcontent|>）
                            translatedText = CleanTranslationText(translatedText);

                            // 记录Token使用量
                            if (root.TryGetProperty("usage", out var usage))
                            {
                                var inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                                var outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                                TrackTokenUsage(inputTokens, outputTokens);
                            }

                            Log.Debug($"翻译成功: {text.Substring(0, Math.Min(20, text.Length))}... -> {translatedText.Substring(0, Math.Min(20, translatedText.Length))}...");
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

            // ✅ 逐段翻译
            var translatedSegments = new List<string>();
            for (int i = 0; i < segments.Count; i++)
            {
                Log.Debug($"翻译第{i + 1}/{segments.Count}段 (长度:{segments[i].Length}字符)");

                var translated = await TranslateAsync(
                    segments[i],
                    targetLanguage,
                    model,
                    sourceLanguage,
                    cancellationToken
                );

                translatedSegments.Add(translated);

                // ✅ 避免触发API限流，段间延迟100ms
                if (i < segments.Count - 1)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            // ✅ 合并翻译结果
            var result = string.Join("\n", translatedSegments);
            Log.Information($"分段翻译完成：{segments.Count}段 → {result.Length}字符");

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
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
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
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
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

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var choices = result.GetProperty("choices");
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

            // 提取内容
            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // 提取Token使用量
            if (root.TryGetProperty("usage", out var usage))
            {
                var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
                var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

                TrackTokenUsage(inputTokens, outputTokens);
                Log.Debug("视觉模型Token使用: 输入{InputTokens}, 输出{OutputTokens}",
                    inputTokens, outputTokens);
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
}

// 对话API数据模型（已统一使用 OpenAI 兼容格式，无需自定义响应模型）
/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = ""; // "user", "assistant", "system", "tool"
    public string Content { get; set; } = "";
    public string? Name { get; set; } // 工具名称（用于role="tool"的消息）
    public string? ToolCallId { get; set; } // ✅ 商业级最佳实践：支持工具调用ID（Function Calling必需）
                                               // 参考：阿里云百炼官方文档 - Function Calling要求tool消息必须包含tool_call_id
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
