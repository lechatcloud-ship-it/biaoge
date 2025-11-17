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

        // ✅ P0修复: Agent核心模型从配置读取,而非硬编码
        // 默认: qwen3-coder-flash (代码专用,工具调用专家,1M上下文,性价比最优)
        // 参考: MODEL_SELECTION_GUIDE.md
        private readonly string _agentModel;

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

            // ✅ P0修复: 从配置读取Agent模型,支持灵活配置和升级
            _agentModel = configManager.GetString("Bailian:AgentCoreModel", "qwen3-coder-flash");
            Log.Information("AI Agent模型配置: {Model}", _agentModel);

            // ✅ 全面迁移到OpenAI SDK
            try
            {
                _openAIClient = new BailianOpenAIClient(_agentModel, configManager);
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
                            model: _agentModel,
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
        /// ✅ 路由所有AutoCAD工具到AutoCADToolExecutor
        /// 参考：AGENT_TOOLS_DESIGN.md
        /// </summary>
        private async Task<string> ExecuteTool(ToolCall toolCall, Action<string>? onStreamChunk)
        {
            try
            {
                switch (toolCall.Name)
                {
                    // ===== P0.1 绘图工具（Drawing Tools）=====
                    case "draw_line":
                        return await AutoCADToolExecutor.DrawLine(toolCall.Arguments);

                    case "draw_circle":
                        return await AutoCADToolExecutor.DrawCircle(toolCall.Arguments);

                    case "draw_rectangle":
                        return await AutoCADToolExecutor.DrawRectangle(toolCall.Arguments);

                    case "draw_polyline":
                        return await AutoCADToolExecutor.DrawPolyline(toolCall.Arguments);

                    case "draw_text":
                        return await AutoCADToolExecutor.DrawText(toolCall.Arguments);

                    // ===== P0.2 修改工具（Modify Tools）=====
                    case "delete_entity":
                        return await AutoCADToolExecutor.DeleteEntity(toolCall.Arguments);

                    case "modify_entity_properties":
                        return await AutoCADToolExecutor.ModifyEntityProperties(toolCall.Arguments);

                    case "move_entity":
                        return await AutoCADToolExecutor.MoveEntity(toolCall.Arguments);

                    case "copy_entity":
                        return await AutoCADToolExecutor.CopyEntity(toolCall.Arguments);

                    // ===== P1.1 查询工具（Query Tools）=====
                    case "measure_distance":
                        return await AutoCADToolExecutor.MeasureDistance(toolCall.Arguments);

                    case "measure_area":
                        return await AutoCADToolExecutor.MeasureArea(toolCall.Arguments);

                    case "list_entities":
                        return await AutoCADToolExecutor.ListEntities(toolCall.Arguments);

                    case "count_entities":
                        return await AutoCADToolExecutor.CountEntities(toolCall.Arguments);

                    // ===== P1.2 图层工具（Layer Tools）=====
                    case "create_layer":
                        return await AutoCADToolExecutor.CreateLayer(toolCall.Arguments);

                    case "set_current_layer":
                        return await AutoCADToolExecutor.SetCurrentLayer(toolCall.Arguments);

                    case "modify_layer_properties":
                        return await AutoCADToolExecutor.ModifyLayerProperties(toolCall.Arguments);

                    case "query_layer_info":
                        return await AutoCADToolExecutor.QueryLayerInfo(toolCall.Arguments);

                    // ===== P1.3 高级修改工具（Advanced Modify Tools）=====
                    case "rotate_entity":
                        return await AutoCADToolExecutor.RotateEntity(toolCall.Arguments);

                    case "scale_entity":
                        return await AutoCADToolExecutor.ScaleEntity(toolCall.Arguments);

                    // ===== P2.1 视图工具（View Tools）=====
                    case "zoom_extents":
                        return await AutoCADToolExecutor.ZoomExtents(toolCall.Arguments);

                    case "zoom_window":
                        return await AutoCADToolExecutor.ZoomWindow(toolCall.Arguments);

                    case "pan_view":
                        return await AutoCADToolExecutor.PanView(toolCall.Arguments);

                    // ===== P2.2 文件工具（File Tools）=====
                    case "save_drawing":
                        return await AutoCADToolExecutor.SaveDrawing(toolCall.Arguments);

                    case "export_to_pdf":
                        return await AutoCADToolExecutor.ExportToPdf(toolCall.Arguments);

                    // ===== P2.3 高级修改工具（Advanced Modify Tools）=====
                    case "mirror_entity":
                        return await AutoCADToolExecutor.MirrorEntity(toolCall.Arguments);

                    case "offset_entity":
                        return await AutoCADToolExecutor.OffsetEntity(toolCall.Arguments);

                    case "trim_entity":
                        return await AutoCADToolExecutor.TrimEntity(toolCall.Arguments);

                    case "extend_entity":
                        return await AutoCADToolExecutor.ExtendEntity(toolCall.Arguments);

                    case "fillet_entity":
                        return await AutoCADToolExecutor.FilletEntity(toolCall.Arguments);

                    case "chamfer_entity":
                        return await AutoCADToolExecutor.ChamferEntity(toolCall.Arguments);

                    // ===== 原有工具（保留兼容性）=====
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

            // ✅ P1修复：添加原始文本空值检查,防止意外的批量替换
            // 如果original为null或空字符串,Contains("")会匹配所有文本,Replace("", x)会在每个字符间插入x
            if (string.IsNullOrEmpty(original))
            {
                Log.Warning("原始文本为空,无法执行替换操作");
                return "✗ 替换失败：原始文本不能为空";
            }

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

                    // ✅ P1修复：移除null-coalescing,因为已经在上面检查过original不为空
                    if (obj is DBText dbText && dbText.TextString.Contains(original))
                    {
                        dbText.TextString = dbText.TextString.Replace(original, newValue ?? "");
                        modifiedCount++;
                    }
                    else if (obj is MText mText && mText.Contents.Contains(original))
                    {
                        mText.Contents = mText.Contents.Replace(original, newValue ?? "");
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
        /// ✅ 集成完整AutoCAD工具集 - 实现真正的Agent功能
        /// 参考：AGENT_TOOLS_DESIGN.md
        /// </summary>
        private List<object> GetAvailableTools()
        {
            return new List<object>
            {
                // ===== P0.1 绘图工具（Drawing Tools）- 5个 =====

                new {
                    type = "function",
                    function = new {
                        name = "draw_line",
                        description = "在AutoCAD中绘制一条直线",
                        parameters = new {
                            type = "object",
                            properties = new {
                                start_point = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "起点坐标[x, y, z]，单位mm"
                                },
                                end_point = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "终点坐标[x, y, z]，单位mm"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名（可选，默认'0'）"
                                },
                                color = new {
                                    type = "string",
                                    description = "颜色（可选，支持中文如'红色'，RGB如'255,0,0'，或'ByLayer'）"
                                }
                            },
                            required = new[] { "start_point", "end_point" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "draw_circle",
                        description = "在AutoCAD中绘制圆",
                        parameters = new {
                            type = "object",
                            properties = new {
                                center = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "圆心坐标[x, y, z]，单位mm"
                                },
                                radius = new {
                                    type = "number",
                                    description = "半径，单位mm"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名（可选，默认'0'）"
                                },
                                color = new {
                                    type = "string",
                                    description = "颜色（可选）"
                                }
                            },
                            required = new[] { "center", "radius" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "draw_rectangle",
                        description = "在AutoCAD中绘制矩形",
                        parameters = new {
                            type = "object",
                            properties = new {
                                corner1 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "第一个角点坐标[x, y]，单位mm"
                                },
                                corner2 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "对角点坐标[x, y]，单位mm"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名（可选，默认'0'）"
                                },
                                color = new {
                                    type = "string",
                                    description = "颜色（可选）"
                                }
                            },
                            required = new[] { "corner1", "corner2" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "draw_polyline",
                        description = "在AutoCAD中绘制多段线（连续的线段）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                points = new {
                                    type = "array",
                                    items = new {
                                        type = "array",
                                        items = new { type = "number" }
                                    },
                                    description = "顶点坐标数组[[x1,y1], [x2,y2], ...]，单位mm"
                                },
                                closed = new {
                                    type = "boolean",
                                    description = "是否闭合（可选，默认false）"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名（可选，默认'0'）"
                                },
                                color = new {
                                    type = "string",
                                    description = "颜色（可选）"
                                }
                            },
                            required = new[] { "points" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "draw_text",
                        description = "在AutoCAD中添加文本标注",
                        parameters = new {
                            type = "object",
                            properties = new {
                                position = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "文本插入点[x, y, z]，单位mm"
                                },
                                text = new {
                                    type = "string",
                                    description = "文本内容"
                                },
                                height = new {
                                    type = "number",
                                    description = "文字高度，单位mm（可选，默认2.5mm）"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名（可选，默认'0'）"
                                },
                                color = new {
                                    type = "string",
                                    description = "颜色（可选）"
                                }
                            },
                            required = new[] { "position", "text" }
                        }
                    }
                },

                // ===== P0.2 修改工具（Modify Tools）- 4个 =====

                new {
                    type = "function",
                    function = new {
                        name = "delete_entity",
                        description = "删除AutoCAD中的图形实体",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle，十六进制字符串）"
                                }
                            },
                            required = new[] { "entity_ids" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "modify_entity_properties",
                        description = "修改AutoCAD实体的属性（颜色、图层等）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                layer = new {
                                    type = "string",
                                    description = "新图层名（可选）"
                                },
                                color = new {
                                    type = "string",
                                    description = "新颜色（可选）"
                                }
                            },
                            required = new[] { "entity_ids" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "move_entity",
                        description = "移动AutoCAD实体到新位置",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                displacement = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "位移向量[dx, dy, dz]，单位mm"
                                }
                            },
                            required = new[] { "entity_ids", "displacement" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "copy_entity",
                        description = "复制AutoCAD实体到新位置",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                displacement = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "位移向量[dx, dy, dz]，单位mm"
                                }
                            },
                            required = new[] { "entity_ids", "displacement" }
                        }
                    }
                },

                // ===== P1.1 查询工具（Query Tools）- 4个 =====

                new {
                    type = "function",
                    function = new {
                        name = "measure_distance",
                        description = "测量两点之间的距离",
                        parameters = new {
                            type = "object",
                            properties = new {
                                point1 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "第一个点坐标[x, y, z]，单位mm"
                                },
                                point2 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "第二个点坐标[x, y, z]，单位mm"
                                }
                            },
                            required = new[] { "point1", "point2" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "measure_area",
                        description = "测量多边形区域的面积",
                        parameters = new {
                            type = "object",
                            properties = new {
                                points = new {
                                    type = "array",
                                    items = new {
                                        type = "array",
                                        items = new { type = "number" }
                                    },
                                    description = "多边形顶点[[x1,y1], [x2,y2], ...]，单位mm"
                                }
                            },
                            required = new[] { "points" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "list_entities",
                        description = "列出图纸中的所有实体",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_type = new {
                                    type = "string",
                                    description = "实体类型过滤（可选，如'Line', 'Circle', 'Text'等）"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名过滤（可选）"
                                }
                            }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "count_entities",
                        description = "统计图纸中的实体数量",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_type = new {
                                    type = "string",
                                    description = "实体类型过滤（可选）"
                                },
                                layer = new {
                                    type = "string",
                                    description = "图层名过滤（可选）"
                                }
                            }
                        }
                    }
                },

                // ===== P1.2 图层工具（Layer Tools）- 4个 =====

                new {
                    type = "function",
                    function = new {
                        name = "create_layer",
                        description = "创建新图层",
                        parameters = new {
                            type = "object",
                            properties = new {
                                layer_name = new {
                                    type = "string",
                                    description = "图层名称"
                                },
                                color = new {
                                    type = "string",
                                    description = "图层颜色（可选，默认白色）"
                                }
                            },
                            required = new[] { "layer_name" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "set_current_layer",
                        description = "设置当前工作图层",
                        parameters = new {
                            type = "object",
                            properties = new {
                                layer_name = new {
                                    type = "string",
                                    description = "图层名称"
                                }
                            },
                            required = new[] { "layer_name" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "modify_layer_properties",
                        description = "修改图层属性",
                        parameters = new {
                            type = "object",
                            properties = new {
                                layer_name = new {
                                    type = "string",
                                    description = "图层名称"
                                },
                                color = new {
                                    type = "string",
                                    description = "新颜色（可选）"
                                },
                                is_frozen = new {
                                    type = "boolean",
                                    description = "是否冻结（可选）"
                                },
                                is_locked = new {
                                    type = "boolean",
                                    description = "是否锁定（可选）"
                                }
                            },
                            required = new[] { "layer_name" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "query_layer_info",
                        description = "查询图层详细信息",
                        parameters = new {
                            type = "object",
                            properties = new {
                                layer_name = new {
                                    type = "string",
                                    description = "图层名称（可选，不指定则返回所有图层）"
                                }
                            }
                        }
                    }
                },

                // ===== P1.3 高级修改工具（Advanced Modify Tools）- 2个 =====

                new {
                    type = "function",
                    function = new {
                        name = "rotate_entity",
                        description = "旋转AutoCAD实体",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                base_point = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "旋转基点[x, y, z]，单位mm"
                                },
                                angle = new {
                                    type = "number",
                                    description = "旋转角度（度，逆时针为正）"
                                }
                            },
                            required = new[] { "entity_ids", "base_point", "angle" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "scale_entity",
                        description = "缩放AutoCAD实体",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                base_point = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "缩放基点[x, y, z]，单位mm"
                                },
                                scale_factor = new {
                                    type = "number",
                                    description = "缩放比例（>1放大，<1缩小）"
                                }
                            },
                            required = new[] { "entity_ids", "base_point", "scale_factor" }
                        }
                    }
                },

                // ===== P2.1 视图工具（View Tools）- 3个 =====

                new {
                    type = "function",
                    function = new {
                        name = "zoom_extents",
                        description = "全图显示（缩放到所有实体的范围）",
                        parameters = new {
                            type = "object",
                            properties = new { }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "zoom_window",
                        description = "窗口缩放（缩放到指定矩形区域）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                corner1 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "窗口第一个角点[x, y, z]，单位mm"
                                },
                                corner2 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "窗口对角点[x, y, z]，单位mm"
                                }
                            },
                            required = new[] { "corner1", "corner2" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "pan_view",
                        description = "平移视图",
                        parameters = new {
                            type = "object",
                            properties = new {
                                displacement = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "平移向量[dx, dy, dz]，单位mm"
                                }
                            },
                            required = new[] { "displacement" }
                        }
                    }
                },

                // ===== P2.2 文件工具（File Tools）- 2个 =====

                new {
                    type = "function",
                    function = new {
                        name = "save_drawing",
                        description = "保存图纸（保存或另存为）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                file_path = new {
                                    type = "string",
                                    description = "文件路径（可选，不指定则保存当前文件）"
                                }
                            }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "export_to_pdf",
                        description = "导出图纸为PDF文件",
                        parameters = new {
                            type = "object",
                            properties = new {
                                output_path = new {
                                    type = "string",
                                    description = "PDF输出路径（含文件名）"
                                }
                            },
                            required = new[] { "output_path" }
                        }
                    }
                },

                // ===== P2.3 高级修改工具（Advanced Modify Tools）- 6个 =====

                new {
                    type = "function",
                    function = new {
                        name = "mirror_entity",
                        description = "镜像AutoCAD实体",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                mirror_line_point1 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "镜像线起点[x, y, z]，单位mm"
                                },
                                mirror_line_point2 = new {
                                    type = "array",
                                    items = new { type = "number" },
                                    description = "镜像线终点[x, y, z]，单位mm"
                                },
                                erase_source = new {
                                    type = "boolean",
                                    description = "是否删除原实体（可选，默认false）"
                                }
                            },
                            required = new[] { "entity_ids", "mirror_line_point1", "mirror_line_point2" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "offset_entity",
                        description = "偏移曲线实体（Line, Circle, Arc, Polyline等）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "实体ID列表（Handle）"
                                },
                                distance = new {
                                    type = "number",
                                    description = "偏移距离，单位mm（正值向外，负值向内）"
                                }
                            },
                            required = new[] { "entity_ids", "distance" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "trim_entity",
                        description = "修剪实体（交互式操作）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                cutting_edge_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "切割边实体ID列表"
                                },
                                entity_to_trim_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "待修剪实体ID列表"
                                }
                            },
                            required = new[] { "cutting_edge_ids", "entity_to_trim_ids" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "extend_entity",
                        description = "延伸实体到边界（交互式操作）",
                        parameters = new {
                            type = "object",
                            properties = new {
                                boundary_edge_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "边界实体ID列表"
                                },
                                entity_to_extend_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "待延伸实体ID列表"
                                }
                            },
                            required = new[] { "boundary_edge_ids", "entity_to_extend_ids" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "fillet_entity",
                        description = "在两个曲线之间创建圆角",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "两个实体ID（Handle）"
                                },
                                radius = new {
                                    type = "number",
                                    description = "圆角半径，单位mm"
                                }
                            },
                            required = new[] { "entity_ids", "radius" }
                        }
                    }
                },

                new {
                    type = "function",
                    function = new {
                        name = "chamfer_entity",
                        description = "在两个曲线之间创建倒角",
                        parameters = new {
                            type = "object",
                            properties = new {
                                entity_ids = new {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "两个实体ID（Handle）"
                                },
                                distance1 = new {
                                    type = "number",
                                    description = "第一条线上的倒角距离，单位mm"
                                },
                                distance2 = new {
                                    type = "number",
                                    description = "第二条线上的倒角距离，单位mm"
                                }
                            },
                            required = new[] { "entity_ids", "distance1", "distance2" }
                        }
                    }
                },

                // ===== 原有工具（保留兼容性）=====

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
                        // 验证必需字段
                        if (!functionElement.TryGetProperty("name", out var nameElement))
                        {
                            Log.Warning("工具定义缺少name字段，跳过");
                            continue;
                        }

                        var functionName = nameElement.GetString();
                        if (string.IsNullOrWhiteSpace(functionName))
                        {
                            Log.Warning("工具name为空，跳过");
                            continue;
                        }

                        var functionDescription = functionElement.TryGetProperty("description", out var descElement)
                            ? descElement.GetString() ?? ""
                            : "";

                        // ✅ v1.0.8+修复：确保parameters不为空且为有效JSON（防止BinaryData.FromString报错）
                        var parametersJson = "{}";
                        if (functionElement.TryGetProperty("parameters", out var parameters))
                        {
                            parametersJson = parameters.GetRawText();
                            if (string.IsNullOrWhiteSpace(parametersJson))
                            {
                                parametersJson = "{}";
                                Log.Warning($"工具{functionName}的parameters为空，使用空对象");
                            }
                            else
                            {
                                // 验证是否为有效JSON
                                try
                                {
                                    JsonDocument.Parse(parametersJson);
                                }
                                catch (JsonException)
                                {
                                    Log.Warning($"工具{functionName}的parameters不是有效JSON，使用空对象");
                                    parametersJson = "{}";
                                }
                            }
                        }

                        // 创建ChatTool（所有参数已验证）
                        var chatTool = OpenAI.Chat.ChatTool.CreateFunctionTool(
                            functionName: functionName,
                            functionDescription: functionDescription,
                            functionParameters: BinaryData.FromString(parametersJson)
                        );

                        result.Add(chatTool);
                        Log.Debug($"成功转换工具定义: {functionName}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "工具定义转换失败");
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
                            var validToolCalls = new List<OpenAI.Chat.ChatToolCall>();

                            foreach (var tc in msg.ToolCalls)
                            {
                                try
                                {
                                    // ✅ v1.0.8+最终修复："数组不能为空。参数名: bytes"
                                    // 问题：JSON反序列化时Function可能为null（默认值初始化器不运行）
                                    // 解决：添加完整的null检查和JSON验证

                                    // 第1步：检查ToolCallInfo对象本身
                                    if (tc == null)
                                    {
                                        Log.Warning("跳过null的ToolCallInfo对象");
                                        continue;
                                    }

                                    // 第2步：检查Id（OpenAI要求tool_call_id必须非空）
                                    if (string.IsNullOrWhiteSpace(tc.Id))
                                    {
                                        Log.Warning("跳过缺少Id的ToolCallInfo对象");
                                        continue;
                                    }

                                    // 第3步：检查并修复Function
                                    if (tc.Function == null)
                                    {
                                        Log.Warning($"工具调用{tc.Id}的Function为null，使用空对象");
                                        tc.Function = new FunctionCallInfo { Name = "unknown", Arguments = "{}" };
                                    }

                                    // 第4步：验证并修复Arguments
                                    var args = tc.Function.Arguments;
                                    if (string.IsNullOrWhiteSpace(args))
                                    {
                                        args = "{}";
                                        Log.Debug($"工具调用{tc.Id}的Arguments为空，使用空对象");
                                    }
                                    else
                                    {
                                        // 验证是否为有效JSON
                                        try
                                        {
                                            JsonDocument.Parse(args);
                                        }
                                        catch (JsonException)
                                        {
                                            Log.Warning($"工具调用{tc.Id}的Arguments不是有效JSON: {args}，使用空对象");
                                            args = "{}";
                                        }
                                    }

                                    // 第5步：验证并修复FunctionName
                                    var functionName = tc.Function.Name;
                                    if (string.IsNullOrWhiteSpace(functionName))
                                    {
                                        functionName = "unknown";
                                        Log.Warning($"工具调用{tc.Id}的FunctionName为空，使用'unknown'");
                                    }

                                    // 第6步：创建ChatToolCall（此时所有参数都已验证）
                                    var chatToolCall = OpenAI.Chat.ChatToolCall.CreateFunctionToolCall(
                                        id: tc.Id,
                                        functionName: functionName,
                                        functionArguments: BinaryData.FromString(args)
                                    );

                                    validToolCalls.Add(chatToolCall);
                                    Log.Debug($"成功转换工具调用: {functionName} (id={tc.Id})");
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, $"转换工具调用失败，跳过: tc.Id={tc?.Id}, tc.Function.Name={tc?.Function?.Name}");
                                }
                            }

                            // 如果没有有效的工具调用，将此消息视为普通assistant消息
                            if (validToolCalls.Count == 0)
                            {
                                Log.Warning($"assistant消息声称有{msg.ToolCalls.Count}个工具调用，但全部无效，退化为普通消息");
                                result.Add(new OpenAI.Chat.AssistantChatMessage(msg.Content ?? ""));
                                break;
                            }

                            IReadOnlyList<OpenAI.Chat.ChatToolCall> toolCalls = validToolCalls;

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
