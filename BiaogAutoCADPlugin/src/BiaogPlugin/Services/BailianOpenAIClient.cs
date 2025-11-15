using OpenAI;
using OpenAI.Chat;
using Serilog;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogPlugin.Services;

/// <summary>
/// 阿里云百炼OpenAI SDK客户端包装
///
/// 使用官方OpenAI .NET SDK调用阿里云百炼API
/// 优势：
/// - 类型安全
/// - 自动错误处理
/// - 流式输出原生支持
/// - 社区支持
///
/// 端点：https://dashscope.aliyuncs.com/compatible-mode/v1
/// 参考：https://help.aliyun.com/zh/model-studio/compatibility-of-openai-with-dashscope
/// </summary>
public class BailianOpenAIClient
{
    private readonly ChatClient _chatClient;
    private readonly string _model;
    private readonly ConfigManager _configManager;

    // Token使用量统计
    private long _totalInputTokens = 0;
    private long _totalOutputTokens = 0;
    private readonly object _tokenStatsLock = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="model">模型名称（如：qwen3-max-preview, qwen-plus, qwen3-vl-flash等）</param>
    /// <param name="configManager">配置管理器</param>
    public BailianOpenAIClient(string model, ConfigManager configManager)
    {
        _model = model;
        _configManager = configManager;

        // 获取API密钥
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("API密钥未配置，请先设置DASHSCOPE_API_KEY环境变量或在配置文件中设置");
        }

        // 创建OpenAI客户端，配置为阿里云百炼端点
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1")
        };

        // 创建API凭证
        var credential = new ApiKeyCredential(apiKey);

        // 初始化ChatClient
        _chatClient = new ChatClient(model, credential, clientOptions);

        Log.Information($"BailianOpenAIClient已初始化: 模型={model}, 端点=dashscope.aliyuncs.com");
    }

    /// <summary>
    /// 获取API密钥（从配置或环境变量）
    /// </summary>
    private string? GetApiKey()
    {
        // 优先从ConfigManager读取
        var apiKey = _configManager.GetString("Bailian:ApiKey");

        // 如果配置中没有，从环境变量读取
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
        }

        return apiKey;
    }

    /// <summary>
    /// 标准对话补全（非流式）
    /// </summary>
    /// <param name="messages">对话消息列表</param>
    /// <param name="temperature">温度参数（0.0-2.0）</param>
    /// <param name="maxTokens">最大输出token数</param>
    /// <param name="tools">工具列表（Function Calling）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话补全结果</returns>
    public async Task<ChatCompletionResult> CompleteAsync(
        List<ChatMessage> messages,
        float temperature = 0.7f,
        int? maxTokens = null,
        IEnumerable<ChatTool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建ChatCompletionOptions
            var options = new ChatCompletionOptions
            {
                Temperature = temperature,
                MaxOutputTokenCount = maxTokens
            };

            // 添加工具（如果有）
            if (tools != null)
            {
                foreach (var tool in tools)
                {
                    options.Tools.Add(tool);
                }
            }

            Log.Debug($"调用OpenAI SDK: 模型={_model}, 消息数={messages.Count}, 温度={temperature}");

            // 转换自定义ChatMessage为OpenAI SDK格式
            var openAIMessages = ConvertToOpenAIMessages(messages);

            // 调用API
            var completion = await _chatClient.CompleteChatAsync(openAIMessages, options, cancellationToken);

            // 记录Token使用量
            if (completion.Value.Usage != null)
            {
                var usage = completion.Value.Usage;
                TrackTokenUsage(usage.InputTokenCount, usage.OutputTokenCount);
                Log.Debug($"Token使用: 输入={usage.InputTokenCount}, 输出={usage.OutputTokenCount}");
            }

            // 转换为标准结果格式
            return ConvertToChatCompletionResult(completion.Value);
        }
        catch (ClientResultException ex)
        {
            Log.Error(ex, $"OpenAI SDK调用失败: {ex.Message}");
            throw new Exception($"API调用失败: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"对话补全异常: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 流式对话补全
    /// </summary>
    /// <param name="messages">对话消息列表</param>
    /// <param name="onChunk">流式输出回调</param>
    /// <param name="temperature">温度参数</param>
    /// <param name="maxTokens">最大输出token数</param>
    /// <param name="tools">工具列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整对话补全结果</returns>
    public async Task<ChatCompletionResult> CompleteStreamingAsync(
        List<ChatMessage> messages,
        Action<string> onChunk,
        float temperature = 0.7f,
        int? maxTokens = null,
        IEnumerable<ChatTool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new ChatCompletionOptions
            {
                Temperature = temperature,
                MaxOutputTokenCount = maxTokens
            };

            if (tools != null)
            {
                foreach (var tool in tools)
                {
                    options.Tools.Add(tool);
                }
            }

            Log.Debug($"流式调用OpenAI SDK: 模型={_model}, 消息数={messages.Count}");

            // 转换自定义ChatMessage为OpenAI SDK格式
            var openAIMessages = ConvertToOpenAIMessages(messages);

            var fullContent = new StringBuilder();
            string? finishReason = null;
            var toolCallsDict = new Dictionary<int, StreamingToolCallAccumulator>();
            int inputTokens = 0;
            int outputTokens = 0;

            // ✅ 官方OpenAI SDK最佳实践：调用CompleteChatStreamingAsync获取流式更新
            // 参考：https://github.com/openai/openai-dotnet
            var streamingUpdates = _chatClient.CompleteChatStreamingAsync(openAIMessages, options, cancellationToken);

            // ✅ 关键：不使用ConfigureAwait(false)，保留SynchronizationContext
            // 这样await foreach会在调用线程（UI线程）继续执行，onChunk回调也在UI线程
            // 参考：OpenAI .NET SDK官方文档 - Streaming Best Practices
            await foreach (var update in streamingUpdates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 处理内容块
                foreach (var contentPart in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentPart.Text))
                    {
                        fullContent.Append(contentPart.Text);
                        onChunk?.Invoke(contentPart.Text);
                    }
                }

                // ✅ 处理工具调用增量更新
                foreach (var toolCallUpdate in update.ToolCallUpdates)
                {
                    var index = toolCallUpdate.Index;

                    if (!toolCallsDict.ContainsKey(index))
                    {
                        toolCallsDict[index] = new StreamingToolCallAccumulator
                        {
                            Id = toolCallUpdate.ToolCallId ?? "",
                            FunctionName = toolCallUpdate.FunctionName ?? "",
                            FunctionArguments = new StringBuilder()
                        };
                    }

                    var accumulator = toolCallsDict[index];

                    // 累积函数参数
                    if (toolCallUpdate.FunctionArgumentsUpdate != null)
                    {
                        var argsText = toolCallUpdate.FunctionArgumentsUpdate.ToString();
                        if (!string.IsNullOrEmpty(argsText))
                        {
                            accumulator.FunctionArguments.Append(argsText);
                        }
                    }

                    // 更新ID和函数名（如果提供）
                    if (!string.IsNullOrEmpty(toolCallUpdate.ToolCallId))
                    {
                        accumulator.Id = toolCallUpdate.ToolCallId;
                    }
                    if (!string.IsNullOrEmpty(toolCallUpdate.FunctionName))
                    {
                        accumulator.FunctionName = toolCallUpdate.FunctionName;
                    }
                }

                // 处理finish reason
                if (update.FinishReason.HasValue)
                {
                    finishReason = update.FinishReason.ToString();
                }

                // Token使用量（如果可用）
                if (update.Usage != null)
                {
                    inputTokens = update.Usage.InputTokenCount;
                    outputTokens = update.Usage.OutputTokenCount;
                    TrackTokenUsage(inputTokens, outputTokens);
                }
            }

            // ✅ 转换累积的工具调用为ToolCall列表
            var toolCalls = new List<ToolCall>();
            foreach (var kvp in toolCallsDict.OrderBy(x => x.Key))
            {
                var acc = kvp.Value;
                var args = new Dictionary<string, object>();

                try
                {
                    var argsJson = acc.FunctionArguments.ToString();
                    if (!string.IsNullOrEmpty(argsJson))
                    {
                        var argDoc = JsonDocument.Parse(argsJson);
                        foreach (var prop in argDoc.RootElement.EnumerateObject())
                        {
                            args[prop.Name] = prop.Value.ToString() ?? "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"解析工具调用参数失败: {acc.FunctionName}");
                }

                toolCalls.Add(new ToolCall
                {
                    Id = acc.Id, // ✅ 商业级最佳实践：保存tool_call_id（Function Calling必需）
                    Name = acc.FunctionName,
                    Arguments = args
                });

                Log.Debug($"工具调用: ID={acc.Id}, Name={acc.FunctionName}, 参数: {acc.FunctionArguments}");
            }

            Log.Debug($"流式输出完成: 内容长度={fullContent.Length}, 工具调用={toolCalls.Count}");

            // 构建结果（匹配BailianApiClient的格式）
            return new ChatCompletionResult
            {
                Content = fullContent.ToString(),
                Model = _model,
                ReasoningContent = "",
                ToolCalls = toolCalls,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"流式对话补全异常: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 视觉模型调用（支持图片输入）
    /// </summary>
    /// <param name="prompt">文本提示词</param>
    /// <param name="imageBase64">Base64编码的图片</param>
    /// <param name="maxTokens">最大输出token数</param>
    /// <param name="temperature">温度参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型响应文本</returns>
    public async Task<string> CallVisionAsync(
        string prompt,
        string imageBase64,
        int maxTokens = 8000,
        float temperature = 0.1f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建包含图片的消息
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(prompt),
                    ChatMessageContentPart.CreateImagePart(
                        BinaryData.FromBytes(Convert.FromBase64String(imageBase64)),
                        "image/png",
                        ChatImageDetailLevel.Auto
                    )
                )
            };

            var options = new ChatCompletionOptions
            {
                Temperature = temperature,
                MaxOutputTokenCount = maxTokens
            };

            Log.Debug($"调用视觉模型: 提示词长度={prompt.Length}, 图片大小={imageBase64.Length / 1024}KB");

            var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);

            // ✅ 空值安全：检查Content
            string content = "";
            if (completion.Value.Content != null && completion.Value.Content.Count > 0 && completion.Value.Content[0] != null)
            {
                content = completion.Value.Content[0].Text ?? "";
            }

            Log.Debug($"视觉模型响应: {content.Substring(0, Math.Min(100, content.Length))}...");

            return content;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"视觉模型调用异常: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 转换OpenAI SDK的ChatCompletion为标准结果格式
    /// </summary>
    private ChatCompletionResult ConvertToChatCompletionResult(ChatCompletion completion)
    {
        // ✅ 空值安全：检查Content是否为空
        string content = "";
        if (completion.Content != null && completion.Content.Count > 0 && completion.Content[0] != null)
        {
            content = completion.Content[0].Text ?? "";
        }

        var result = new ChatCompletionResult
        {
            Content = content,
            Model = _model,
            InputTokens = completion.Usage?.InputTokenCount ?? 0,
            OutputTokens = completion.Usage?.OutputTokenCount ?? 0,
            ReasoningContent = "" // qwen3-max-preview不支持深度思考
        };

        // 提取工具调用
        if (completion.ToolCalls != null && completion.ToolCalls.Count > 0)
        {
            result.ToolCalls = completion.ToolCalls
                .Select(tc =>
                {
                    // 解析Arguments JSON为Dictionary
                    var args = new Dictionary<string, object>();
                    try
                    {
                        var argJson = System.Text.Json.JsonDocument.Parse(tc.FunctionArguments.ToString());
                        foreach (var prop in argJson.RootElement.EnumerateObject())
                        {
                            args[prop.Name] = prop.Value.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"解析工具调用参数失败: {tc.FunctionArguments}");
                    }

                    return new ToolCall
                    {
                        Id = tc.Id, // ✅ 商业级最佳实践：保存tool_call_id
                        Name = tc.FunctionName,
                        Arguments = args
                    };
                })
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// 记录Token使用量
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
    /// 将自定义ChatMessage转换为OpenAI SDK的ChatMessage
    /// </summary>
    private List<OpenAI.Chat.ChatMessage> ConvertToOpenAIMessages(List<ChatMessage> messages)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();

        foreach (var msg in messages)
        {
            OpenAI.Chat.ChatMessage openAIMsg = msg.Role.ToLower() switch
            {
                "system" => new SystemChatMessage(msg.Content),
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                "tool" => new ToolChatMessage(msg.ToolCallId ?? "", msg.Content),
                _ => throw new ArgumentException($"未知的消息角色: {msg.Role}")
            };

            result.Add(openAIMsg);
        }

        return result;
    }

    /// <summary>
    /// 获取Token使用统计
    /// </summary>
    public (long InputTokens, long OutputTokens) GetTokenUsage()
    {
        lock (_tokenStatsLock)
        {
            return (_totalInputTokens, _totalOutputTokens);
        }
    }
}

// ChatCompletionResult, ToolCall等类型定义在BailianApiClient.cs中，此处重用

/// <summary>
/// 流式工具调用累积器（用于累积增量更新）
/// </summary>
internal class StreamingToolCallAccumulator
{
    public string Id { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public StringBuilder FunctionArguments { get; set; } = new();
}
