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
        private readonly BailianOpenAIClient? _openAIClient;  // ✅ 添加OpenAI SDK客户端
        private readonly ConfigManager _configManager;
        private readonly DrawingContextManager _contextManager;
        private readonly ContextLengthManager _contextLengthManager;
        private readonly bool _useOpenAISDK;  // ✅ 控制是否使用OpenAI SDK

        // Agent核心模型配置（2025-11-16升级到qwen3-coder-flash）
        // qwen3-coder-flash: 代码专用，工具调用专家，1M上下文，性价比最优
        // 参考: MODEL_SELECTION_GUIDE.md
        private const string AgentModel = "qwen3-coder-flash";

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

            // ✅ 全面迁移到OpenAI SDK
            try
            {
                _openAIClient = new BailianOpenAIClient(AgentModel, configManager);
                _useOpenAISDK = true;
                Log.Information("标哥AI助手初始化完成 - 使用OpenAI SDK（流式优化）");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OpenAI SDK初始化失败");
                throw new InvalidOperationException("AI助手初始化失败，请检查API密钥配置", ex);
            }
        }

        /// <summary>
        /// Agent对话 - 智能分析和执行任务
        /// </summary>
        /// <param name="userMessage">用户消息</param>
        /// <param name="onContentChunk">正文内容流式回调</param>
        public async Task<AssistantResponse> ChatStreamAsync(
            string userMessage,
            Action<string>? onContentChunk = null)
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
                var systemPrompt = BuildAgentSystemPrompt(drawingContext, detectedScenario, false);

                _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                {
                    Role = "user",
                    Content = userMessage
                });

                var messages = BuildMessages(systemPrompt);

                // ===== 第3步：Agent决策 =====
                Log.Information("开始Agent决策（深度思考模式）...");

                // ✅ 使用 OpenAI SDK 进行流式调用（HttpClient 不支持流式）
                // ⚠️ 注意：OpenAI SDK 不支持 reasoning_content，无法读取思考过程
                // 解决方案：UI 显示"深度思考中..."状态即可
                ChatCompletionResult agentDecision;

                if (_useOpenAISDK && _openAIClient != null)
                {
                    // 转换工具定义为 OpenAI SDK 格式
                    var openAITools = ConvertToOpenAIChatTools(tools);

                    agentDecision = await _openAIClient.CompleteStreamingAsync(
                        messages: messages,
                        onChunk: chunk => onContentChunk?.Invoke(chunk),
                        temperature: 0.7f,
                        tools: openAITools,
                        enableThinking: false  // OpenAI SDK 不支持 enable_thinking 参数
                    );
                }
                else
                {
                    throw new InvalidOperationException("必须使用 OpenAI SDK 以支持流式输出");
                }

                // ✅ CRITICAL FIX: 保存assistant消息时必须包含ToolCalls信息
                // 参考：阿里云百炼Function Calling规范 - assistant消息如果调用工具必须包含tool_calls数组
                var assistantMessage = new BiaogPlugin.Services.ChatMessage
                {
                    Role = "assistant",
                    Content = agentDecision.Content
                };

                // ✅ 如果有工具调用，必须保存完整的ToolCalls信息（防止会话恢复时丢失导致API错误）
                if (agentDecision.ToolCalls.Count > 0)
                {
                    assistantMessage.ToolCalls = agentDecision.ToolCalls
                        .Select((tc, index) =>
                        {
                            // ✅ v1.0.7修复：确保Arguments永远不为空（防止BinaryData.FromString报错）
                            var serializedArgs = System.Text.Json.JsonSerializer.Serialize(tc.Arguments ?? new Dictionary<string, object>());
                            var safeArgs = string.IsNullOrWhiteSpace(serializedArgs) ? "{}" : serializedArgs;

                            return new ToolCallInfo
                            {
                                Id = tc.Id,
                                Type = "function",
                                Function = new FunctionCallInfo
                                {
                                    Name = tc.Name,
                                    Arguments = safeArgs
                                },
                                Index = index
                            };
                        })
                        .ToList();

                    Log.Debug($"保存assistant消息（含{assistantMessage.ToolCalls.Count}个工具调用）");
                }

                _chatHistory.Add(assistantMessage);

                // ===== 第4步：工具执行 =====
                if (agentDecision.ToolCalls.Count > 0)
                {
                    onContentChunk?.Invoke($"\n[标哥AI助手] 需要调用{agentDecision.ToolCalls.Count}个工具执行任务\n");

                    foreach (var toolCall in agentDecision.ToolCalls)
                    {
                        Log.Information($"执行工具: {toolCall.Name}");
                        onContentChunk?.Invoke($"\n[工具调用] {toolCall.Name}\n");

                        string toolResult = await ExecuteTool(toolCall, onContentChunk);

                        // ✅ 商业级最佳实践：添加工具结果到历史（阿里云百炼官方格式）
                        // 参考：https://help.aliyun.com/zh/model-studio/qwen-function-calling
                        _chatHistory.Add(new BiaogPlugin.Services.ChatMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            Name = toolCall.Name,
                            ToolCallId = toolCall.Id // ✅ CRITICAL: 必须包含tool_call_id对应工具调用
                        });
                    }

                    // ===== 第5步：总结反馈 =====
                    onContentChunk?.Invoke($"\n[标哥AI助手] 正在总结执行结果...\n");

                    var summaryMessages = BuildMessages(systemPrompt);
                    ChatCompletionResult summary;

                    if (_useOpenAISDK && _openAIClient != null)
                    {
                        // ✅ 使用OpenAI SDK进行流式总结
                        summary = await _openAIClient.CompleteStreamingAsync(
                            messages: summaryMessages,
                            onChunk: chunk => onContentChunk?.Invoke(chunk),
                            temperature: 0.7f
                        );
                    }
                    else
                    {
                        // 降级方案
                        summary = await _bailianClient.ChatCompletionStreamAsync(
                            messages: summaryMessages,
                            model: AgentModel,
                            onStreamChunk: chunk => onContentChunk?.Invoke(chunk)
                        );
                    }

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
        /// 安全获取字典值（防御JSON反序列化null问题）
        /// </summary>
        /// <param name="args">参数字典（可能为null）</param>
        /// <param name="key">键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>字符串值</returns>
        private string GetArgSafe(Dictionary<string, object>? args, string key, string defaultValue = "")
        {
            if (args == null)
            {
                Log.Warning($"参数字典为null，使用默认值: {key}={defaultValue}");
                return defaultValue;
            }

            if (!args.ContainsKey(key))
            {
                Log.Warning($"参数字典缺少键: {key}，使用默认值: {defaultValue}");
                return defaultValue;
            }

            var value = args[key];
            if (value == null)
            {
                Log.Warning($"参数值为null: {key}，使用默认值: {defaultValue}");
                return defaultValue;
            }

            return value.ToString() ?? defaultValue;
        }

        /// <summary>
        /// 翻译工具 - 执行翻译任务
        /// </summary>
        private async Task<string> ExecuteTranslateTool(Dictionary<string, object> args, Action<string>? onStreamChunk)
        {
            onStreamChunk?.Invoke($"  → 正在执行翻译...\n");

            // ✅ v1.0.8修复：使用安全方法访问参数（防止args为null）
            var text = GetArgSafe(args, "text");
            var targetLanguage = GetArgSafe(args, "target_language", "en");

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

            // ✅ v1.0.8修复：使用安全方法访问参数（防止args为null）
            var operation = GetArgSafe(args, "operation");
            var original = GetArgSafe(args, "original_text");
            var newValue = GetArgSafe(args, "new_text");

            // 理解修改意图并执行
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            // ✅ AutoCAD 2022最佳实践: 文档锁必须在事务之前
            using (var docLock = doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
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

                // ✅ v1.0.8修复：使用安全方法访问参数（防止args为null）
                var queryType = GetArgSafe(args, "query_type");
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
        /// ✅ 获取最优的思考Token预算（根据场景复杂度动态调整）
        ///
        /// 基于阿里云百炼官方最佳实践：
        /// - thinking_budget用于限制推理过程的最大Token数
        /// - 过高：延迟增加、成本上升、思考过程冗长
        /// - 过低：推理深度不足、质量下降
        /// - 最佳实践：根据任务复杂度动态调整
        ///
        /// 参考：https://help.aliyun.com/zh/model-studio/deep-thinking
        /// </summary>
        /// <param name="scenario">工作场景</param>
        /// <returns>最优思考Token预算</returns>
        private int GetOptimalThinkingBudget(ScenarioPromptManager.Scenario scenario)
        {
            return scenario switch
            {
                // 算量：需要深度推理（多步骤计算、精确度验证）
                ScenarioPromptManager.Scenario.Calculation => 5000,

                // 质量检查：需要全面分析（多维度检查、规范对比）
                ScenarioPromptManager.Scenario.QualityCheck => 4000,

                // 错误诊断：需要中等推理（原因分析、解决方案）
                ScenarioPromptManager.Scenario.Diagnosis => 3000,

                // 图纸问答：需要简单推理（信息查找、关联分析）
                ScenarioPromptManager.Scenario.DrawingQA => 2000,

                // 图纸修改：需要简单推理（操作规划、验证）
                ScenarioPromptManager.Scenario.Modification => 1500,

                // 翻译：需要最小推理（术语理解、上下文）
                ScenarioPromptManager.Scenario.Translation => 1000,

                // 通用场景：使用中等预算
                _ => 2000
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
            // ✅ 使用ContextLengthManager智能裁剪，防止超过1M输入限制
            var trimmedHistory = _contextLengthManager.TrimMessages(_chatHistory, systemPrompt);

            var messages = new List<BiaogPlugin.Services.ChatMessage>
            {
                new BiaogPlugin.Services.ChatMessage { Role = "system", Content = systemPrompt }
            };

            messages.AddRange(trimmedHistory);

            // ✅ v1.0.7最终修复：验证消息链是否符合Function Calling规范
            if (!ValidateMessageChain(messages, out string validationError))
            {
                Log.Error($"消息链验证失败: {validationError}");
                throw new InvalidOperationException($"消息历史不符合Function Calling规范: {validationError}");
            }

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
        /// 加载对话历史（用于切换会话）
        /// </summary>
        /// <remarks>
        /// ✅ v1.0.7最终修复：根据阿里云百炼官方规范正确处理消息历史
        /// 参考：https://help.aliyun.com/zh/model-studio/qwen-function-calling
        ///
        /// 关键规则：
        /// 1. 保留完整的消息链：user → assistant(with tool_calls) → tool → assistant(summary)
        /// 2. 只删除"孤立"的tool消息（前面没有对应tool_calls的assistant）
        /// 3. 不能简单过滤所有tool消息，这会破坏Function Calling的上下文
        /// </remarks>
        public void LoadHistory(List<BiaogPlugin.Services.ChatMessage> messages)
        {
            _chatHistory.Clear();

            if (messages == null || messages.Count == 0)
            {
                Log.Information("加载对话历史: 无历史消息");
                return;
            }

            // ✅ 正确的消息验证和过滤逻辑
            var validatedMessages = new List<BiaogPlugin.Services.ChatMessage>();
            BiaogPlugin.Services.ChatMessage? lastAssistantWithToolCalls = null;

            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];

                switch (msg.Role.ToLower())
                {
                    case "system":
                    case "user":
                        // system和user消息总是保留
                        validatedMessages.Add(msg);
                        lastAssistantWithToolCalls = null; // 重置tool_calls追踪
                        break;

                    case "assistant":
                        // assistant消息总是保留
                        validatedMessages.Add(msg);

                        // 如果包含tool_calls，记录下来供后续tool消息验证
                        if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                        {
                            lastAssistantWithToolCalls = msg;
                            Log.Debug($"记录assistant消息（含{msg.ToolCalls.Count}个tool_calls）");
                        }
                        else
                        {
                            lastAssistantWithToolCalls = null;
                        }
                        break;

                    case "tool":
                        // ✅ CRITICAL: tool消息必须紧跟在包含tool_calls的assistant消息之后
                        if (lastAssistantWithToolCalls != null)
                        {
                            // 验证tool_call_id是否匹配
                            bool hasMatchingToolCall = lastAssistantWithToolCalls.ToolCalls!
                                .Any(tc => tc.Id == msg.ToolCallId);

                            if (hasMatchingToolCall)
                            {
                                validatedMessages.Add(msg);
                                Log.Debug($"保留有效tool消息: {msg.Name}, tool_call_id={msg.ToolCallId}");
                            }
                            else
                            {
                                Log.Warning($"跳过tool消息（tool_call_id不匹配）: {msg.Name}, " +
                                          $"tool_call_id={msg.ToolCallId}");
                            }
                        }
                        else
                        {
                            Log.Warning($"跳过孤立的tool消息（前面没有对应的assistant with tool_calls）: " +
                                      $"{msg.Name}, tool_call_id={msg.ToolCallId}");
                        }
                        break;

                    default:
                        Log.Warning($"跳过未知角色的消息: {msg.Role}");
                        break;
                }
            }

            _chatHistory.AddRange(validatedMessages);
            Log.Information($"加载对话历史: {messages.Count}条原始消息 → {validatedMessages.Count}条有效消息");
        }

        /// <summary>
        /// 验证消息历史是否符合阿里云百炼Function Calling规范
        /// </summary>
        /// <remarks>
        /// 验证规则：
        /// 1. tool消息必须紧跟在包含tool_calls的assistant消息之后
        /// 2. tool_call_id必须与assistant消息中的tool_calls[].id匹配
        /// 3. 不应存在孤立的tool消息
        /// </remarks>
        private bool ValidateMessageChain(List<BiaogPlugin.Services.ChatMessage> messages, out string error)
        {
            error = string.Empty;
            BiaogPlugin.Services.ChatMessage? lastAssistant = null;

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

        /// <summary>
        /// 转换匿名对象工具定义为OpenAI SDK的ChatTool
        /// </summary>
        private List<OpenAI.Chat.ChatTool> ConvertToOpenAIChatTools(List<object> tools)
        {
            var result = new List<OpenAI.Chat.ChatTool>();

            foreach (var tool in tools)
            {
                try
                {
                    // 将匿名对象序列化为JSON
                    var toolJson = JsonSerializer.Serialize(tool);
                    var toolDoc = JsonDocument.Parse(toolJson);

                    // 提取function对象
                    if (toolDoc.RootElement.TryGetProperty("function", out var functionElement))
                    {
                        var functionName = functionElement.GetProperty("name").GetString();
                        var functionDescription = functionElement.GetProperty("description").GetString();
                        var parameters = functionElement.GetProperty("parameters");

                        // ✅ v1.0.8修复：确保parameters不为空（防止BinaryData.FromString报错）
                        var parametersJson = parameters.GetRawText();
                        if (string.IsNullOrWhiteSpace(parametersJson))
                        {
                            parametersJson = "{}";
                            Log.Warning($"工具{functionName}的parameters为空，使用空对象");
                        }

                        // 创建ChatTool
                        var chatTool = OpenAI.Chat.ChatTool.CreateFunctionTool(
                            functionName: functionName,
                            functionDescription: functionDescription,
                            functionParameters: BinaryData.FromString(parametersJson)
                        );

                        result.Add(chatTool);
                        Log.Debug($"转换工具: {functionName}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "工具转换失败");
                }
            }

            return result;
        }

        /// <summary>
        /// 转换内部ChatMessage为OpenAI SDK的ChatMessage
        /// </summary>
        private List<OpenAI.Chat.ChatMessage> ConvertToOpenAIChatMessages(List<BiaogPlugin.Services.ChatMessage> messages)
        {
            var result = new List<OpenAI.Chat.ChatMessage>();

            foreach (var msg in messages)
            {
                switch (msg.Role.ToLower())
                {
                    case "system":
                        result.Add(new OpenAI.Chat.SystemChatMessage(msg.Content));
                        break;
                    case "user":
                        result.Add(new OpenAI.Chat.UserChatMessage(msg.Content));
                        break;
                    case "assistant":
                        // ✅ CRITICAL FIX: assistant消息如果包含工具调用，必须传递ToolCalls参数
                        // 参考：阿里云百炼Function Calling - assistant消息调用工具时必须包含tool_calls数组
                        // 参考：OpenAI .NET SDK - AssistantChatMessage构造函数仅接受IReadOnlyList<ChatToolCall>
                        if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                        {
                            // 转换为OpenAI SDK的ChatToolCall格式
                            IReadOnlyList<OpenAI.Chat.ChatToolCall> toolCalls = msg.ToolCalls
                                .Select(tc =>
                                {
                                    // ✅ v1.0.8最终修复："数组不能为空。参数名: bytes"
                                    // 问题：JSON反序列化时Function可能为null（默认值初始化器不运行）
                                    // 解决：添加Function和Arguments的null检查
                                    if (tc.Function == null)
                                    {
                                        Log.Warning($"工具调用{tc.Id}的Function为null，使用空对象");
                                        tc.Function = new FunctionCallInfo { Name = "", Arguments = "{}" };
                                    }

                                    var args = string.IsNullOrWhiteSpace(tc.Function.Arguments) ? "{}" : tc.Function.Arguments;
                                    var functionName = string.IsNullOrWhiteSpace(tc.Function.Name) ? "unknown" : tc.Function.Name;

                                    return OpenAI.Chat.ChatToolCall.CreateFunctionToolCall(
                                        id: tc.Id,
                                        functionName: functionName,
                                        functionArguments: BinaryData.FromString(args)
                                    );
                                })
                                .ToList();

                            // 使用工具调用创建消息
                            var assistantMessage = new OpenAI.Chat.AssistantChatMessage(toolCalls);

                            // 如果有文本内容，添加到Content集合
                            if (!string.IsNullOrEmpty(msg.Content))
                            {
                                assistantMessage.Content.Add(
                                    OpenAI.Chat.ChatMessageContentPart.CreateTextPart(msg.Content)
                                );
                            }

                            result.Add(assistantMessage);
                            Log.Debug($"添加assistant消息（含{toolCalls.Count}个工具调用）");
                        }
                        else
                        {
                            result.Add(new OpenAI.Chat.AssistantChatMessage(msg.Content));
                        }
                        break;
                    case "tool":
                        // ✅ 商业级最佳实践：正确处理工具消息（Function Calling必需）
                        // 参考：OpenAI .NET SDK - ToolChatMessage requires toolCallId
                        if (!string.IsNullOrEmpty(msg.ToolCallId))
                        {
                            result.Add(new OpenAI.Chat.ToolChatMessage(msg.ToolCallId, msg.Content));
                            Log.Debug($"添加工具消息: {msg.Name}, tool_call_id={msg.ToolCallId}");
                        }
                        else
                        {
                            Log.Warning($"跳过工具消息（缺少tool_call_id）: {msg.Name}");
                        }
                        break;
                    default:
                        Log.Warning($"未知消息角色: {msg.Role}");
                        break;
                }
            }

            return result;
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
