using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// AI助手服务 - 提供智能对话和建议（使用阿里云百炼API）
/// </summary>
public class AIAssistant
{
    private readonly object _historyLock = new();
    private readonly HttpClient _httpClient;
    private readonly AIContextManager _contextManager;
    private readonly ConfigManager _configManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIAssistant> _logger;
    private readonly List<ChatMessage> _conversationHistory = new();
    private string? _apiKey;

    public AIAssistant(
        HttpClient httpClient,
        AIContextManager contextManager,
        ConfigManager configManager,
        IConfiguration configuration,
        ILogger<AIAssistant> logger)
    {
        _httpClient = httpClient;
        _contextManager = contextManager;
        _configManager = configManager;
        _configuration = configuration;
        _logger = logger;

        // 设置API基地址（OpenAI兼容接口）
        _httpClient.BaseAddress = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1/");

        RefreshApiKey();
    }

    /// <summary>
    /// 刷新API密钥
    /// </summary>
    private void RefreshApiKey()
    {
        // 从配置中读取API密钥
        _apiKey = _configManager.GetString("Bailian:ApiKey")
            ?? _configuration["Bailian:ApiKey"]
            ?? Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");

        // 更新HTTP客户端的Authorization头
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _logger.LogInformation("AI助手API密钥已加载");
        }
        else
        {
            _logger.LogWarning("未找到AI助手API密钥");
        }
    }

    /// <summary>
    /// 发送消息给AI助手
    /// </summary>
    public async Task<string> SendMessageAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("用户消息: {Message}", userMessage);

        // 添加用户消息到历史
        lock (_historyLock)
        {
            _conversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage
            });
        }

        try
        {
            // 获取上下文信息
            var context = _contextManager.BuildContext();

            // 构建系统提示词
            var systemPrompt = BuildSystemPrompt(context);

            // 调用AI模型
            var response = await CallAIModelAsync(systemPrompt, userMessage, cancellationToken);

            // 添加助手回复到历史
            lock (_historyLock)
            {
                _conversationHistory.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response
                });
            }

            _logger.LogInformation("AI回复: {Response}", response);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI助手处理失败");
            return "抱歉，我遇到了一些问题。请稍后再试。";
        }
    }

    /// <summary>
    /// 构建系统提示词
    /// </summary>
    private string BuildSystemPrompt(string context)
    {
        return $@"你是CAD翻译算量工具的AI助手，专门帮助用户处理DWG图纸、翻译和算量任务。

当前上下文信息：
{context}

你的职责：
1. 回答关于图纸、构件、翻译的问题
2. 提供专业的建筑工程建议
3. 帮助用户理解识别结果和工程量数据
4. 解释置信度和验证依据

回答风格：
- 专业、准确、简洁
- 使用建筑工程术语
- 提供具体的数据和依据";
    }

    /// <summary>
    /// 调用AI模型（使用百炼OpenAI兼容接口）
    /// </summary>
    private async Task<string> CallAIModelAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return "请先在设置中配置阿里云百炼API密钥。";
        }

        try
        {
            // 构建消息列表
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            // 添加历史对话（最近5轮）
            List<ChatMessage> recentHistory;
            lock (_historyLock)
            {
                recentHistory = _conversationHistory.TakeLast(10).ToList();
            }

            foreach (var msg in recentHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            // 添加当前用户消息（如果不在历史中）
            if (recentHistory.Count == 0 || recentHistory.Last().Role != "user" || recentHistory.Last().Content != userMessage)
            {
                messages.Add(new { role = "user", content = userMessage });
            }

            // 构建请求
            var requestBody = new
            {
                model = "qwen-plus", // 使用qwen-plus模型（平衡性能和成本）
                messages = messages,
                temperature = 0.7,
                max_tokens = 2000,
                stream = false // 暂不使用流式输出
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("调用AI模型: qwen-plus");

            // 发送请求
            var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("AI模型调用失败: {StatusCode}, {Error}", response.StatusCode, error);
                return $"抱歉，AI服务暂时不可用（错误代码：{response.StatusCode}）。";
            }

            // 解析响应
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<BailianChatResponse>(responseJson);

            if (result?.Choices != null && result.Choices.Count > 0)
            {
                var reply = result.Choices[0].Message?.Content ?? "抱歉，我无法生成回复。";
                _logger.LogDebug("AI回复: {Reply}", reply);
                return reply;
            }

            return "抱歉，我无法生成回复。";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI模型网络请求失败");
            return "网络连接失败，请检查网络设置。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI模型调用异常");
            return $"抱歉，发生了错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 流式调用AI模型（用于实时显示）
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("流式用户消息: {Message}", userMessage);

        // 添加用户消息到历史
        lock (_historyLock)
        {
            _conversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage
            });
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            yield return "请先在设置中配置阿里云百炼API密钥。";
            yield break;
        }

        var context = _contextManager.BuildContext();
        var systemPrompt = BuildSystemPrompt(context);
        var fullResponse = new StringBuilder();

        try
        {
            // 构建消息列表
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            List<ChatMessage> recentHistory;
            lock (_historyLock)
            {
                recentHistory = _conversationHistory.TakeLast(10).Where(m => m.Role != "user" || m.Content != userMessage).ToList();
            }

            foreach (var msg in recentHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            messages.Add(new { role = "user", content = userMessage });

            // 构建请求（启用流式）
            var requestBody = new
            {
                model = "qwen-plus",
                messages = messages,
                temperature = 0.7,
                max_tokens = 2000,
                stream = true // 启用流式输出
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                yield return $"AI服务错误：{response.StatusCode}";
                yield break;
            }

            // 读取流式响应（SSE格式）
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                var data = line.Substring(6); // 去掉"data: "前缀

                if (data == "[DONE]")
                    break;

                try
                {
                    var chunk = JsonSerializer.Deserialize<BailianStreamChunk>(data);
                    var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;

                    if (!string.IsNullOrEmpty(delta))
                    {
                        fullResponse.Append(delta);
                        yield return delta;
                    }
                }
                catch (JsonException)
                {
                    // 忽略无法解析的行
                    continue;
                }
            }

            // 添加完整回复到历史
            var finalResponse = fullResponse.ToString();
            if (!string.IsNullOrWhiteSpace(finalResponse))
            {
                lock (_historyLock)
                {
                    _conversationHistory.Add(new ChatMessage
                    {
                        Role = "assistant",
                        Content = finalResponse
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式AI调用失败");
            yield return $"错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 清空对话历史
    /// </summary>
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _conversationHistory.Clear();
        }
        _logger.LogInformation("对话历史已清空");
    }

    /// <summary>
    /// 获取对话历史
    /// </summary>
    public List<ChatMessage> GetHistory()
    {
        lock (_historyLock)
        {
            return new List<ChatMessage>(_conversationHistory);
        }
    }
}

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// 百炼API响应模型（非流式）
/// </summary>
public record BailianChatResponse
{
    public List<BailianChoice>? Choices { get; set; }
    public string? Model { get; set; }
    public BailianUsage? Usage { get; set; }
}

public record BailianChoice
{
    public int Index { get; set; }
    public BailianMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

public record BailianMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}

public record BailianUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// 百炼API流式响应模型
/// </summary>
public record BailianStreamChunk
{
    public List<BailianStreamChoice>? Choices { get; set; }
}

public record BailianStreamChoice
{
    public int Index { get; set; }
    public BailianDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

public record BailianDelta
{
    public string? Content { get; set; }
    public string? Role { get; set; }
}
