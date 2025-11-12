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

    // API端点
    private const string TranslationEndpoint = "/api/v1/services/translation/translate";
    private const string BatchTranslationEndpoint = "/api/v1/services/translation/batch-translate";
    private const string ChatCompletionEndpoint = "/compatible-mode/v1/chat/completions";

    // 重试配置（阿里云官方推荐）
    private const int MaxRetries = 3;
    private const int InitialRetryDelayMs = 1000; // 1秒

    // Token使用量统计
    private long _totalInputTokens = 0;
    private long _totalOutputTokens = 0;
    private readonly object _tokenStatsLock = new();

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
    /// 批量翻译
    /// </summary>
    public async Task<List<string>> TranslateBatchAsync(
        List<string> texts,
        string targetLanguage,
        string? model = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 从配置读取模型，如果未指定则使用最优翻译模型
        if (string.IsNullOrEmpty(model))
        {
            model = _configManager.GetString(
                "Bailian:TextTranslationModel",
                BailianModelSelector.Models.QwenMTFlash // 2025 Flash：术语定制+格式还原
            );
        }

        Log.Debug($"翻译使用模型: {model}");

        var results = new List<string>();
        const int batchSize = 50;

        var batches = texts.Chunk(batchSize).ToList();

        for (int i = 0; i < batches.Length; i++)
        {
            var batch = batches[i].ToList();

            try
            {
                var request = new
                {
                    model = model,
                    input = new
                    {
                        source_language = "zh",
                        target_language = targetLanguage,
                        source_texts = batch
                    }
                };

                // 创建带Authorization头的请求（线程安全）
                var apiKey = GetApiKey();
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/services/translation/batch-translate")
                {
                    Content = JsonContent.Create(request)
                };
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                }

                // 使用带重试的HTTP请求
                var response = await SendWithRetryAsync(httpRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BailianBatchResponse>(
                        cancellationToken: cancellationToken
                    );

                    if (result?.Output?.Translations != null)
                    {
                        results.AddRange(result.Output.Translations);

                        // 记录Token使用量
                        if (result.Usage != null)
                        {
                            TrackTokenUsage(result.Usage.InputTokens, result.Usage.OutputTokens);
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Log.Warning("批量翻译失败: {StatusCode}, {Error}", response.StatusCode, errorContent);
                    results.AddRange(batch); // 失败时返回原文
                }

                progress?.Report((i + 1.0) / batches.Length * 100);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量翻译异常");
                results.AddRange(batch);
            }
        }

        return results;
    }

    /// <summary>
    /// 单文本翻译
    /// </summary>
    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        // 从配置读取模型，如果未指定则使用最优翻译模型
        if (string.IsNullOrEmpty(model))
        {
            model = _configManager.GetString(
                "Bailian:TextTranslationModel",
                BailianModelSelector.Models.QwenMTFlash // 2025 Flash：术语定制+格式还原
            );
        }

        Log.Debug($"翻译使用模型: {model}");

        try
        {
            var request = new
            {
                model = model,
                input = new
                {
                    source_language = "zh",
                    target_language = targetLanguage,
                    source_text = text
                }
            };

            // 创建带Authorization头的请求（线程安全）
            var apiKey = GetApiKey();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/services/translation/translate")
            {
                Content = JsonContent.Create(request)
            };
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            // 使用带重试的HTTP请求
            var response = await SendWithRetryAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BailianTranslateResponse>(
                    cancellationToken: cancellationToken
                );

                if (result?.Output?.Translation != null)
                {
                    // 记录Token使用量
                    if (result.Usage != null)
                    {
                        TrackTokenUsage(result.Usage.InputTokens, result.Usage.OutputTokens);
                    }

                    return result.Output.Translation;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Warning("翻译失败: {StatusCode}, {Error}", response.StatusCode, errorContent);
            }

            return text; // 失败时返回原文
        }
        catch (Exception ex)
        {
            Log.Error(ex, "翻译异常: {Text}", text);
            return text;
        }
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var model = _configManager.GetString("Bailian:TextTranslationModel", "qwen-mt-plus");

            var request = new
            {
                model = model,
                input = new
                {
                    source_language = "zh",
                    target_language = "en",
                    source_text = "测试"
                }
            };

            // 创建带Authorization头的请求（线程安全）
            var apiKey = GetApiKey();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, TranslationEndpoint)
            {
                Content = JsonContent.Create(request)
            };
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            // 使用带重试的HTTP请求
            var response = await SendWithRetryAsync(httpRequest, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
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
            stream_options = new
            {
                include_usage = true,
                incremental_output = true  // 阿里云官方推荐：增量输出优化
            },
            temperature = temperature,
            top_p = topP,
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

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // 处理流式响应
        var fullResponse = new StringBuilder();
        var fullReasoning = new StringBuilder();
        var toolCalls = new List<ToolCall>();
        int inputTokens = 0;
        int outputTokens = 0;

        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
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
                                onStreamChunk?.Invoke(text);
                            }
                        }

                        // 处理深度思考内容
                        if (delta.TryGetProperty("reasoning_content", out var reasoning))
                        {
                            var thinkingText = reasoning.GetString();
                            if (!string.IsNullOrEmpty(thinkingText))
                            {
                                fullReasoning.Append(thinkingText);
                                onReasoningChunk?.Invoke(thinkingText);
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
                    catch (Exception ex)
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
}

// 翻译API数据模型
public record BailianBatchResponse(
    BailianOutput Output,
    BailianUsage Usage
);

public record BailianOutput(
    List<string> Translations
);

public record BailianTranslateResponse(
    BailianTranslateOutput Output,
    BailianUsage Usage
);

public record BailianTranslateOutput(
    string Translation
);

public record BailianUsage(
    int InputTokens,
    int OutputTokens
);

// 对话API数据模型
/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = ""; // "user", "assistant", "system", "tool"
    public string Content { get; set; } = "";
    public string? Name { get; set; } // 工具名称（用于role="tool"的消息）
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
    public string Name { get; set; } = "";
    public Dictionary<string, object> Arguments { get; set; } = new();
}
