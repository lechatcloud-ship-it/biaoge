using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// AI助手服务 - 提供智能对话和建议
/// </summary>
public class AIAssistant
{
    private readonly BailianApiClient _apiClient;
    private readonly AIContextManager _contextManager;
    private readonly ILogger<AIAssistant> _logger;
    private readonly List<ChatMessage> _conversationHistory = new();

    public AIAssistant(
        BailianApiClient apiClient,
        AIContextManager contextManager,
        ILogger<AIAssistant> logger)
    {
        _apiClient = apiClient;
        _contextManager = contextManager;
        _logger = logger;
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
        _conversationHistory.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        try
        {
            // 获取上下文信息
            var context = _contextManager.BuildContext();

            // 构建系统提示词
            var systemPrompt = BuildSystemPrompt(context);

            // 调用AI模型
            var response = await CallAIModelAsync(systemPrompt, userMessage, cancellationToken);

            // 添加助手回复到历史
            _conversationHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response
            });

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
        return $@"你是表哥建筑CAD助手，专门帮助用户处理DWG图纸、翻译和算量任务。

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
    /// 调用AI模型
    /// </summary>
    private async Task<string> CallAIModelAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        // 这里简化处理，实际应该调用百炼的对话模型
        // 由于百炼API不同模型有不同的调用方式，这里提供基本框架

        // TODO: 根据具体的百炼对话API文档实现
        // 可能需要使用qwen-max或qwen-plus模型

        await Task.Delay(500, cancellationToken); // 模拟API调用

        // 暂时返回一个简单的响应
        return $"我理解您的问题：{userMessage}\n\n基于当前上下文，我建议您查看相关的构件识别结果和翻译数据。";
    }

    /// <summary>
    /// 清空对话历史
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
        _logger.LogInformation("对话历史已清空");
    }

    /// <summary>
    /// 获取对话历史
    /// </summary>
    public List<ChatMessage> GetHistory()
    {
        return new List<ChatMessage>(_conversationHistory);
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
