using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;
using BiaogPlugin.Extensions;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 标哥AI助手 - 由Excellent开发的专为CAD图纸服务的Agent
    ///
    /// 工作流（阿里云官方5步）：
    /// 1. 工具定义（Tools Definition）
    /// 2. 消息初始化（Message Initialization）
    /// 3. Agent决策（Initial Model Call）
    /// 4. 工具执行（Tool Execution）
    /// 5. 总结反馈（Synthesis）
    /// </summary>
    public class AIAssistantService
    {
        private readonly BailianApiClient _bailianClient;
        private readonly ConfigManager _configManager;
        private readonly DrawingContextManager _contextManager;
        private readonly ContextLengthManager _contextLengthManager;

        // Agent核心模型配置
        private const string AgentModel = "qwen3-max-preview";

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
            _contextLengthManager = new ContextLengthManager();

            Log.Information("标哥AI助手初始化完成");
        }

        /// <summary>
        /// Agent对话 - 智能分析和执行任务
        /// </summary>
        /// <param name="userMessage">用户消息</param>
        /// <param name="useDeepThinking">是否启用深度思考</param>
        /// <param name="onContentChunk">正文内容流式回调</param>
        /// <param name="onReasoningChunk">深度思考内容流式回调</param>
        public async Task<AssistantResponse> ChatStreamAsync(
            string userMessage,
            bool useDeepThinking = false,
            Action<string>? onContentChunk = null,
            Action<string>? onReasoningChunk = null)
        {
            try
            {
                // ✅ 移除重复的状态消息，使用日志记录即可
                Log.Information("开始处理用户消息: {Message}", userMessage);

                // ===== 第0步：场景检测 =====
                var detectedScenario = ScenarioPromptManager.DetectScenario(userMessage);
                Log.Information($"检测到场景: {detectedScenario}");

                // ✅ 场景识别信息也不通过流式输出，避免干扰AI回复
                if (detectedScenario != ScenarioPromptManager.Scenario.General)
                {
                    Log.Information($"场景识别: {GetScenarioDisplayName(detectedScenario)}");
                }

                // ===== 第1步：工具定义 =====
                var tools = GetAvailableTools();

                // ===== 第2步：消息初始化 =====
                var drawingContext = _contextManager.GetCurrentDrawingContext();
                var systemPrompt = BuildAgentSystemPrompt(drawingContext, detectedScenario, useDeepThinking);

                _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                {
                    Role = "user",
                    Content = userMessage
                });

                var messages = BuildMessages(systemPrompt);

                // ===== 第3步：Agent决策 =====
                // ✅ 移除状态消息，直接开始流式输出AI回复
                Log.Information("开始Agent决策...");

                // ✅ 关键修复：正确分离思考内容和正文内容的回调
                // 不再将reasoning包裹后发送到同一个回调，而是直接传递两个独立的回调
                var agentDecision = await _bailianClient.ChatCompletionStreamAsync(
                    messages: messages,
                    model: AgentModel,
                    tools: tools,
                    onStreamChunk: chunk => onContentChunk?.Invoke(chunk),  // ✅ 正文回调
                    onReasoningChunk: useDeepThinking
                        ? reasoning => onReasoningChunk?.Invoke(reasoning)  // ✅ 思考回调（不添加前缀）
                        : null,
                    temperature: 0.7,
                    topP: 0.9,
                    thinkingBudget: useDeepThinking ? 10000 : null,
                    enableThinking: useDeepThinking  // ✅ 传递enable_thinking参数
                );

                // 添加Agent回复到历史
                _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                {
                    Role = "assistant",
                    Content = agentDecision.Content
                });

                // ===== 第4步：工具执行 =====
                if (agentDecision.ToolCalls.Count > 0)
                {
                    onContentChunk?.Invoke($"\n[标哥AI助手] 需要调用{agentDecision.ToolCalls.Count}个工具执行任务\n");

                    foreach (var toolCall in agentDecision.ToolCalls)
                    {
                        Log.Information($"执行工具: {toolCall.Name}");
                        onContentChunk?.Invoke($"\n[工具调用] {toolCall.Name}\n");

                        string toolResult = await ExecuteTool(toolCall, onContentChunk);

                        // 添加工具结果到历史（官方推荐格式）
                        _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            Name = toolCall.Name
                        });
                    }

                    // ===== 第5步：总结反馈 =====
                    onContentChunk?.Invoke($"\n[标哥AI助手] 正在总结执行结果...\n");

                    var summaryMessages = BuildMessages(systemPrompt);
                    var summary = await _bailianClient.ChatCompletionStreamAsync(
                        messages: summaryMessages,
                        model: AgentModel,
                        onStreamChunk: chunk => onContentChunk?.Invoke(chunk)
                    );

                    _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                    {
                        Role = "assistant",
                        Content = summary.Content
                    });

                    return new AssistantResponse
                    {
                        Success = true,
                        Message = summary.Content,
                        ToolCalls = agentDecision.ToolCalls.Select(tc => new AssistantToolCall
                        {
                            Name = tc.Name,
                            Arguments = tc.Arguments
                        }).ToList()
                    };
                }

                // 如果不需要工具，直接返回Agent回复
                return new AssistantResponse
                {
                    Success = true,
                    Message = agentDecision.Content,
                    ToolCalls = new List<AssistantToolCall>()
                };
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "标哥AI助手执行失败");
                return new AssistantResponse
                {
                    Success = false,
                    Error = $"AI助手执行失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 执行工具（智能调度和执行）
        /// </summary>
        private async Task<string> ExecuteTool(ToolCall toolCall, Action<string>? onStreamChunk)
        {
            try
            {
                switch (toolCall.Name)
                {
                    case "translate_text":
                        return await ExecuteTranslateTool(toolCall.Arguments, onStreamChunk);

                    case "modify_drawing":
                        return await ExecuteModifyDrawingTool(toolCall.Arguments, onStreamChunk);

                    case "recognize_components":
                        return await ExecuteRecognitionTool(toolCall.Arguments, onStreamChunk);

                    case "query_drawing_info":
                        return await ExecuteQueryTool(toolCall.Arguments, onStreamChunk);

                    default:
                        return $"✗ 未知工具: {toolCall.Name}";
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, $"工具执行失败: {toolCall.Name}");
                return $"✗ 工具执行失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 翻译工具 - 执行翻译任务
        /// </summary>
        private async Task<string> ExecuteTranslateTool(Dictionary<string, object> args, Action<string>? onStreamChunk)
        {
            onStreamChunk?.Invoke($"  → 正在执行翻译...\n");

            var text = args["text"].ToString() ?? "";
            var targetLanguage = args["target_language"].ToString() ?? "en";

            var translated = await _bailianClient.TranslateAsync(
                text: text,
                targetLanguage: targetLanguage,
                model: BailianModelSelector.Models.QwenMTFlash
            );

            Log.Information($"翻译完成: {text} → {translated}");
            return $"✓ 翻译完成：'{text}' → '{translated}'";
        }

        /// <summary>
        /// 修改图纸工具 - 执行图纸修改任务
        /// </summary>
        private async Task<string> ExecuteModifyDrawingTool(Dictionary<string, object> args, Action<string>? onStreamChunk)
        {
            onStreamChunk?.Invoke($"  → 正在执行图纸修改...\n");

            var operation = args["operation"].ToString() ?? "";
            var original = args.ContainsKey("original_text") ? args["original_text"].ToString() : "";
            var newValue = args.ContainsKey("new_text") ? args["new_text"].ToString() : "";

            // 理解修改意图并执行
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            using (var docLock = doc.LockDocument())
            {
                var extractor = new DwgTextExtractor();
                var allTexts = extractor.ExtractAllText();

                int modifiedCount = 0;

                foreach (var textEntity in allTexts)
                {
                    var obj = tr.GetObject(textEntity.Id, OpenMode.ForWrite);

                    if (obj is DBText dbText && dbText.TextString.Contains(original ?? ""))
                    {
                        dbText.TextString = dbText.TextString.Replace(original ?? "", newValue ?? "");
                        modifiedCount++;
                    }
                    else if (obj is MText mText && mText.Contents.Contains(original ?? ""))
                    {
                        mText.Contents = mText.Contents.Replace(original ?? "", newValue ?? "");
                        modifiedCount++;
                    }
                }

                tr.Commit();

                Log.Information($"修改完成: 已修改{modifiedCount}处文本");
                return $"✓ 修改完成：已将{modifiedCount}处'{original}'改为'{newValue}'";
            }
        }

        /// <summary>
        /// 构件识别工具 - 执行构件识别任务
        /// </summary>
        private async Task<string> ExecuteRecognitionTool(Dictionary<string, object> args, Action<string>? onStreamChunk)
        {
            onStreamChunk?.Invoke($"  → 正在识别构件...\n");

            // 提取图纸文本实体用于识别
            var extractor = new DwgTextExtractor();
            var textEntities = extractor.ExtractAllText();

            var bailianClient = ServiceLocator.GetService<BailianApiClient>();
            var recognizer = new ComponentRecognizer(bailianClient!);
            var components = await recognizer.RecognizeFromTextEntitiesAsync(textEntities);

            var summary = $"✓ 识别完成：共识别{components.Count}个构件\n";
            summary += $"  - 墙: {components.Count(c => c.Type == "墙")}个\n";
            summary += $"  - 柱: {components.Count(c => c.Type == "柱")}个\n";
            summary += $"  - 梁: {components.Count(c => c.Type == "梁")}个\n";
            summary += $"  - 板: {components.Count(c => c.Type == "板")}个";

            Log.Information($"构件识别完成: {components.Count}个");
            return summary;
        }

        /// <summary>
        /// 查询图纸信息工具 - 直接查询DrawingContext
        /// ✅ 修复：添加异常处理和日志，避免卡住
        /// </summary>
        private async Task<string> ExecuteQueryTool(Dictionary<string, object> args, Action<string>? onStreamChunk)
        {
            try
            {
                onStreamChunk?.Invoke($"  → 查询图纸信息...\n");
                Log.Debug("开始执行查询图纸工具");

                var queryType = args.ContainsKey("query_type") ? args["query_type"].ToString() : "";
                Log.Debug($"查询类型: {queryType}");

                // ✅ 添加异常处理
                DrawingContext context;
                try
                {
                    context = _contextManager.GetCurrentDrawingContext();
                    Log.Debug("成功获取图纸上下文");
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "获取图纸上下文失败");
                    return $"✗ 获取图纸信息失败：{ex.Message}";
                }

                // ✅ 检查是否有错误消息
                if (!string.IsNullOrEmpty(context.ErrorMessage))
                {
                    Log.Warning($"图纸上下文包含错误: {context.ErrorMessage}");
                    return $"✗ 查询失败：{context.ErrorMessage}";
                }

                string result;
                try
                {
                    result = queryType switch
                    {
                        "layers" => $"✓ 图层信息：\n{string.Join("\n", context.Layers.Select(l => $"  - {l.Name} ({l.Color})"))}",
                        "texts" => $"✓ 文本数量：{context.TextEntities.Count}个",
                        "entities" => $"✓ 实体统计：\n{string.Join("\n", context.EntityStatistics.Select(e => $"  - {e.Key}: {e.Value}个"))}",
                        "metadata" => $"✓ 元数据：\n{string.Join("\n", context.Metadata.Select(m => $"  - {m.Key}: {m.Value}"))}",
                        _ => $"✓ 图纸摘要：\n{context.Summary}"
                    };
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "格式化查询结果失败");
                    return $"✗ 格式化结果失败：{ex.Message}";
                }

                Log.Information($"查询图纸信息完成: {queryType}, 结果长度={result.Length}");
                onStreamChunk?.Invoke($"  ✓ 查询完成\n");

                // ✅ 确保返回非空结果，避免阿里云API因空结果而卡住
                return await Task.FromResult(!string.IsNullOrEmpty(result) ? result : "✗ 查询结果为空");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "执行查询图纸工具失败");
                onStreamChunk?.Invoke($"  ✗ 查询失败: {ex.Message}\n");
                return $"✗ 查询图纸信息失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 构建Agent系统提示（使用场景化提示词）
        /// </summary>
        private string BuildAgentSystemPrompt(
            DrawingContext context,
            ScenarioPromptManager.Scenario scenario,
            bool useDeepThinking)
        {
            // 使用ScenarioPromptManager构建场景化提示词
            var prompt = ScenarioPromptManager.BuildPrompt(scenario, context, useDeepThinking);

            // 附加建筑规范知识库摘要
            prompt += "\n\n" + BuildingStandardsKnowledge.GetKnowledgeSummary();

            return prompt;
        }

        /// <summary>
        /// 获取场景显示名称
        /// </summary>
        private string GetScenarioDisplayName(ScenarioPromptManager.Scenario scenario)
        {
            return scenario switch
            {
                ScenarioPromptManager.Scenario.Translation => "翻译场景",
                ScenarioPromptManager.Scenario.Calculation => "算量场景",
                ScenarioPromptManager.Scenario.DrawingQA => "图纸问答",
                ScenarioPromptManager.Scenario.Modification => "图纸修改",
                ScenarioPromptManager.Scenario.Diagnosis => "错误诊断",
                ScenarioPromptManager.Scenario.QualityCheck => "质量检查",
                _ => "通用对话"
            };
        }

        /// <summary>
        /// 定义可用工具（阿里云官方格式）
        /// </summary>
        private List<object> GetAvailableTools()
        {
            return new List<object>
            {
                // 翻译工具
                new {
                    type = "function",
                    function = new {
                        name = "translate_text",
                        description = "翻译CAD图纸中的文本，支持92种语言。",
                        parameters = new {
                            type = "object",
                            properties = new {
                                text = new {
                                    type = "string",
                                    description = "要翻译的文本内容"
                                },
                                target_language = new {
                                    type = "string",
                                    description = "目标语言（en=英语, ja=日语, ko=韩语, fr=法语等）"
                                }
                            },
                            required = new[] { "text", "target_language" }
                        }
                    }
                },

                // 修改图纸工具
                new {
                    type = "function",
                    function = new {
                        name = "modify_drawing",
                        description = "修改CAD图纸中的文本内容。",
                        parameters = new {
                            type = "object",
                            properties = new {
                                operation = new {
                                    type = "string",
                                    description = "操作类型（replace=替换文本）"
                                },
                                original_text = new {
                                    type = "string",
                                    description = "原始文本"
                                },
                                new_text = new {
                                    type = "string",
                                    description = "新文本"
                                }
                            },
                            required = new[] { "operation", "original_text", "new_text" }
                        }
                    }
                },

                // 构件识别工具
                new {
                    type = "function",
                    function = new {
                        name = "recognize_components",
                        description = "识别CAD图纸中的建筑构件（墙、柱、梁、板等）。",
                        parameters = new {
                            type = "object",
                            properties = new {
                                component_types = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "要识别的构件类型（可选，不指定则识别所有）"
                                }
                            }
                        }
                    }
                },

                // 查询图纸信息工具
                new {
                    type = "function",
                    function = new {
                        name = "query_drawing_info",
                        description = "查询图纸的详细信息（图层、文本、实体统计、元数据等）。",
                        parameters = new {
                            type = "object",
                            properties = new {
                                query_type = new {
                                    type = "string",
                                    description = "查询类型：layers=图层, texts=文本, entities=实体统计, metadata=元数据, summary=摘要"
                                }
                            },
                            required = new[] { "query_type" }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 构建消息列表
        /// </summary>
        private List<BiaogPlugin.Services.ChatMessage> BuildMessages(string systemPrompt)
        {
            // ✅ 使用ContextLengthManager智能裁剪，防止超过252K输入限制
            var trimmedHistory = _contextLengthManager.TrimMessages(_chatHistory, systemPrompt);

            var messages = new List<BiaogPlugin.Services.ChatMessage>
            {
                new BiaogPlugin.Services.ChatMessage { Role = "system", Content = systemPrompt }
            };

            messages.AddRange(trimmedHistory);

            // 记录Token使用情况
            int estimatedTokens = _contextLengthManager.EstimateTokens(messages, "");
            Log.Debug($"消息构建完成: {messages.Count}条消息, 约{estimatedTokens:N0} tokens");

            return messages;
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
