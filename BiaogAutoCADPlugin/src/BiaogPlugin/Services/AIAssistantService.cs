using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 标哥AI助手服务 - 基于阿里云百炼大模型
    /// 支持流式输出、深度思考、Function Calling
    /// 使用统一的BailianApiClient，一个API密钥调用所有模型
    /// </summary>
    public class AIAssistantService
    {
        private readonly BailianApiClient _bailianClient;
        private readonly ConfigManager _configManager;
        private readonly DrawingContextManager _contextManager;

        // 对话历史
        private readonly List<BiaogPlugin.Services.ChatMessage> _chatHistory = new();

        public AIAssistantService(
            BailianApiClient bailianClient,
            ConfigManager configManager,
            DrawingContextManager contextManager)
        {
            _bailianClient = bailianClient;
            _configManager = configManager;
            _contextManager = contextManager;

            Log.Information("AI助手服务初始化完成（使用统一Bailian客户端）");
        }

        /// <summary>
        /// 流式对话 - 支持深度思考和Function Calling
        /// </summary>
        /// <param name="userMessage">用户消息</param>
        /// <param name="useDeepThinking">是否启用深度思考模式</param>
        /// <param name="onStreamChunk">流式输出回调</param>
        public async Task<AssistantResponse> ChatStreamAsync(
            string userMessage,
            bool useDeepThinking = false,
            Action<string>? onStreamChunk = null)
        {
            try
            {
                // 1. 获取当前图纸上下文
                var drawingContext = _contextManager.GetCurrentDrawingContext();

                // 2. 构建系统提示（包含图纸信息）
                var systemPrompt = BuildSystemPrompt(drawingContext);

                // 3. 添加用户消息到历史
                _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                {
                    Role = "user",
                    Content = userMessage
                });

                // 4. 构建消息列表（系统提示 + 历史）
                var messages = BuildMessages(systemPrompt);

                // 5. 选择最优模型
                var model = useDeepThinking
                    ? BailianModelSelector.GetOptimalModel(BailianModelSelector.TaskType.DeepThinking)
                    : BailianModelSelector.GetOptimalModel(BailianModelSelector.TaskType.Conversation, highPerformance: true);

                Log.Information($"使用模型: {model} (深度思考: {useDeepThinking})");

                // 6. 使用统一客户端调用API - 支持流式输出和深度思考
                var result = await _bailianClient.ChatCompletionStreamAsync(
                    messages: messages,
                    model: model,
                    tools: GetAvailableTools(),
                    onStreamChunk: onStreamChunk,
                    onReasoningChunk: reasoning => onStreamChunk?.Invoke($"\n[思考中: {reasoning}]\n"),
                    temperature: 0.7,
                    topP: 0.9,
                    thinkingBudget: useDeepThinking ? 10000 : null
                );

                // 7. 添加AI回复到历史
                _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                {
                    Role = "assistant",
                    Content = result.Content
                });

                Log.Information($"AI助手回复完成: {result.Content.Length} 字符, 使用模型: {result.Model}");

                return new AssistantResponse
                {
                    Success = true,
                    Message = result.Content,
                    ToolCalls = result.ToolCalls.Select(tc => new AssistantToolCall
                    {
                        Name = tc.Name,
                        Arguments = tc.Arguments
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AI助手对话失败");
                return new AssistantResponse
                {
                    Success = false,
                    Error = $"对话失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 构建系统提示（包含图纸上下文）
        /// </summary>
        private string BuildSystemPrompt(DrawingContext context)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("你是**标哥AI助手**，一个专业的AutoCAD图纸分析和操作助手。");
            prompt.AppendLine();
            prompt.AppendLine("## 你的能力");
            prompt.AppendLine("1. 分析和理解AutoCAD图纸内容");
            prompt.AppendLine("2. 回答用户关于图纸的问题（文本内容、图层、实体统计等）");
            prompt.AppendLine("3. 通过Function Calling修改图纸（修改文本、图层操作等）");
            prompt.AppendLine("4. 识别建筑构件并进行工程量计算");
            prompt.AppendLine("5. 提供专业的CAD绘图建议");
            prompt.AppendLine();

            if (!string.IsNullOrEmpty(context.ErrorMessage))
            {
                prompt.AppendLine($"## ⚠️ 当前图纸状态");
                prompt.AppendLine(context.ErrorMessage);
            }
            else
            {
                prompt.AppendLine("## 当前图纸信息");
                prompt.AppendLine(context.Summary);
            }

            prompt.AppendLine();
            prompt.AppendLine("## 注意事项");
            prompt.AppendLine("- 回答要专业、准确、简洁");
            prompt.AppendLine("- 涉及修改图纸时，务必询问用户确认");
            prompt.AppendLine("- 如果信息不足，主动询问用户需要什么帮助");

            return prompt.ToString();
        }

        /// <summary>
        /// 构建消息列表
        /// </summary>
        private List<BiaogPlugin.Services.ChatMessage> BuildMessages(string systemPrompt)
        {
            var messages = new List<BiaogPlugin.Services.ChatMessage>
            {
                new BiaogPlugin.Services.ChatMessage { Role = "system", Content = systemPrompt }
            };

            // 只保留最近10轮对话
            var recentHistory = _chatHistory.TakeLast(20).ToList();
            messages.AddRange(recentHistory);

            return messages;
        }

        /// <summary>
        /// 定义可用的Function Calling工具
        /// </summary>
        private List<object> GetAvailableTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "modify_text",
                        description = "修改CAD图纸中的文本内容",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                original_text = new
                                {
                                    type = "string",
                                    description = "要修改的原始文本内容"
                                },
                                new_text = new
                                {
                                    type = "string",
                                    description = "修改后的新文本内容"
                                }
                            },
                            required = new[] { "original_text", "new_text" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "query_layers",
                        description = "查询图纸中的图层信息",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                layer_name = new
                                {
                                    type = "string",
                                    description = "要查询的图层名称（可选，为空则返回所有图层）"
                                }
                            }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "count_entities",
                        description = "统计图纸中的实体数量",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                entity_type = new
                                {
                                    type = "string",
                                    description = "要统计的实体类型（如Line, Circle, Text等）"
                                }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 清除对话历史
        /// </summary>
        public void ClearHistory()
        {
            _chatHistory.Clear();
            Log.Information("对话历史已清除");
        }

        /// <summary>
        /// 获取对话历史
        /// </summary>
        public List<BiaogPlugin.Services.ChatMessage> GetHistory()
        {
            return _chatHistory.ToList();
        }
    }

    /// <summary>
    /// 助手响应
    /// </summary>
    public class AssistantResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<AssistantToolCall> ToolCalls { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// 助手工具调用（用于UI显示）
    /// </summary>
    public class AssistantToolCall
    {
        public string Name { get; set; } = "";
        public Dictionary<string, object> Arguments { get; set; } = new();
    }
}
