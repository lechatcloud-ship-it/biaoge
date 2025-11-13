using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 智能翻译策略服务 - 基于图纸理解的智能翻译判断
    ///
    /// 核心思想：
    /// 1. 先用 qwen3-vl-flash 分析图纸类型和内容
    /// 2. 根据建筑工程知识判断哪些文字应该翻译
    /// 3. 生成翻译策略（白名单/黑名单）
    /// </summary>
    public class SmartTranslationStrategy
    {
        private readonly BailianApiClient _bailianClient;
        private readonly DrawingContextManager _drawingContextManager;

        // 翻译黑名单：这些模式的文本不应该翻译
        private static readonly List<Regex> TranslationBlacklist = new()
        {
            // 纯数字（尺寸标注）
            new Regex(@"^[\d\.\,\-\+\/\s]+$"),

            // 轴线标号（A, B, C, 1, 2, 3等）
            new Regex(@"^[A-Z]$|^[0-9]$|^[A-Z]-[0-9]$"),

            // 坐标标注
            new Regex(@"^[\d\.]+(,|，)[\d\.]+$"),

            // 标高标记
            new Regex(@"^[±\+\-]?[\d\.]+[m|M]?$"),

            // 比例尺
            new Regex(@"^1[:：/]\d+$"),

            // 图号
            new Regex(@"^[A-Z\d]+-[\d\-]+$"),

            // 日期格式
            new Regex(@"^\d{4}[-/]\d{1,2}[-/]\d{1,2}$"),

            // 纯符号
            new Regex(@"^[\W_]+$"),
        };

        // 常见的需要翻译的图纸文字类别
        private static readonly Dictionary<string, List<string>> TranslationWhitelist = new()
        {
            ["房间名称"] = new() { "BEDROOM", "LIVING ROOM", "KITCHEN", "BATHROOM", "TOILET" },
            ["建筑构件"] = new() { "WALL", "COLUMN", "BEAM", "SLAB", "WINDOW", "DOOR" },
            ["材料名称"] = new() { "CONCRETE", "STEEL", "WOOD", "GLASS", "BRICK" },
            ["说明文字"] = new() { "NOTE", "SECTION", "DETAIL", "ELEVATION", "PLAN" },
        };

        public SmartTranslationStrategy()
        {
            _bailianClient = ServiceLocator.GetService<BailianApiClient>()!;
            _drawingContextManager = new DrawingContextManager();
        }

        /// <summary>
        /// 分析图纸并生成智能翻译策略
        /// </summary>
        public async Task<TranslationStrategy> AnalyzeDrawingAndGenerateStrategy()
        {
            try
            {
                Log.Information("正在分析图纸内容，生成智能翻译策略...");

                // 1. 获取图纸上下文信息
                var drawingContext = _drawingContextManager.GetCurrentDrawingContext();

                if (drawingContext.TextEntities.Count == 0)
                {
                    Log.Warning("图纸中没有文本，无法生成翻译策略");
                    return new TranslationStrategy
                    {
                        IsValid = false,
                        ErrorMessage = "图纸中没有文本内容"
                    };
                }

                // 2. 准备图纸分析提示词
                var analysisPrompt = BuildDrawingAnalysisPrompt(drawingContext);

                // 3. 调用 AI 分析图纸（使用视觉模型）
                var aiAnalysis = await AnalyzeDrawingWithAI(analysisPrompt, drawingContext);

                // 4. 基于 AI 分析结果生成翻译策略
                var strategy = GenerateStrategyFromAnalysis(aiAnalysis, drawingContext);

                Log.Information($"智能翻译策略生成完成: 应翻译 {strategy.TextsToTranslate.Count} 项, 保留 {strategy.TextsToPreserve.Count} 项");

                return strategy;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "生成智能翻译策略失败");
                return new TranslationStrategy
                {
                    IsValid = false,
                    ErrorMessage = $"分析失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 构建图纸分析提示词
        /// </summary>
        private string BuildDrawingAnalysisPrompt(DrawingContext context)
        {
            var prompt = $@"# 建筑图纸智能翻译分析任务

## 图纸基本信息
- 文件名: {context.FileName}
- 文本数量: {context.TextEntities.Count}
- 图层数量: {context.Layers.Count}
- 实体统计: {string.Join(", ", context.EntityStatistics.Select(kv => $"{kv.Key}: {kv.Value}"))}

## 图纸中的文字内容（按图层分组）
{GenerateTextListByLayer(context)}

## 任务要求
作为建筑工程领域的专家，请分析这张图纸并回答：

1. **图纸类型判断**：这是什么类型的图纸？（建筑平面图、立面图、剖面图、结构图、设备图等）

2. **文字分类**：将图纸中的文字分为以下类别：
   - ✅ **应该翻译**：房间名称、构件名称、材料说明、注释文字等
   - ❌ **不应翻译**：尺寸标注、轴线编号、标高数值、坐标、图号、比例尺等
   - ⚠️ **需要判断**：可能需要根据上下文判断的文字

3. **翻译建议**：对于应该翻译的文字，给出具体的翻译建议和原因

## 输出格式
请以 JSON 格式输出：
```json
{{
  ""drawingType"": ""图纸类型"",
  ""shouldTranslate"": [""文字1"", ""文字2"", ...],
  ""shouldPreserve"": [""文字3"", ""文字4"", ...],
  ""needsContext"": [""文字5"", ""文字6"", ...],
  ""translationSuggestions"": {{
    ""文字1"": {{
      ""category"": ""类别"",
      ""reason"": ""原因"",
      ""suggestedTranslation"": ""建议翻译""
    }}
  }}
}}
```
";

            return prompt;
        }

        /// <summary>
        /// 生成按图层分组的文字列表
        /// </summary>
        private string GenerateTextListByLayer(DrawingContext context)
        {
            var textsByLayer = context.TextEntities
                .GroupBy(t => t.Layer)
                .OrderByDescending(g => g.Count())
                .Take(10);  // 只取前10个最重要的图层

            var result = "";
            foreach (var group in textsByLayer)
            {
                result += $"\n### 图层: {group.Key} ({group.Count()} 个文字)\n";
                var uniqueTexts = group.Select(t => t.Content).Distinct().Take(20);  // 每个图层最多20个示例
                foreach (var text in uniqueTexts)
                {
                    result += $"  - {text}\n";
                }
            }

            return result;
        }

        /// <summary>
        /// 使用 AI 分析图纸
        /// </summary>
        private async Task<DrawingAnalysisResult> AnalyzeDrawingWithAI(string prompt, DrawingContext context)
        {
            try
            {
                // TODO: 如果有图纸图片，使用 qwen3-vl-flash 视觉模型
                // 目前先用纯文本模型分析文字内容

                var messages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = "你是建筑工程领域的专家，擅长分析CAD图纸并判断哪些文字需要翻译，哪些应该保留原样。"
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = prompt
                    }
                };

                // 使用对话模型分析
                var response = await _bailianClient.ChatCompletionAsync(
                    messages,
                    model: BailianModelSelector.Models.Qwen3MaxPreview,  // 使用最强模型分析
                    temperature: 0.3  // 低温度确保分析稳定
                );

                // 解析 AI 返回的 JSON
                var analysisResult = ParseAIAnalysisResponse(response.Content);

                return analysisResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AI 分析图纸失败");
                throw;
            }
        }

        /// <summary>
        /// 解析 AI 分析响应
        /// </summary>
        private DrawingAnalysisResult ParseAIAnalysisResponse(string response)
        {
            // 提取 JSON 内容（可能包含在 ```json...``` 代码块中）
            var jsonMatch = Regex.Match(response, @"```json\s*([\s\S]*?)\s*```");
            var jsonString = jsonMatch.Success ? jsonMatch.Groups[1].Value : response;

            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<DrawingAnalysisResult>(jsonString);
                return result ?? new DrawingAnalysisResult();
            }
            catch
            {
                Log.Warning("无法解析 AI 分析结果，使用默认策略");
                return new DrawingAnalysisResult();
            }
        }

        /// <summary>
        /// 基于 AI 分析结果生成翻译策略
        /// </summary>
        private TranslationStrategy GenerateStrategyFromAnalysis(DrawingAnalysisResult analysis, DrawingContext context)
        {
            var strategy = new TranslationStrategy
            {
                IsValid = true,
                DrawingType = analysis.DrawingType,
                TextsToTranslate = new List<string>(),
                TextsToPreserve = new List<string>(),
                TranslationHints = new Dictionary<string, string>()
            };

            // 合并 AI 建议和规则引擎判断
            foreach (var textEntity in context.TextEntities)
            {
                var text = textEntity.Content;

                // 1. 先检查黑名单规则（不应翻译）
                if (MatchesBlacklist(text))
                {
                    strategy.TextsToPreserve.Add(text);
                    continue;
                }

                // 2. 检查 AI 的建议
                if (analysis.ShouldTranslate != null && analysis.ShouldTranslate.Contains(text))
                {
                    strategy.TextsToTranslate.Add(text);

                    // 添加翻译提示
                    if (analysis.TranslationSuggestions != null &&
                        analysis.TranslationSuggestions.TryGetValue(text, out var suggestion))
                    {
                        strategy.TranslationHints[text] = suggestion.SuggestedTranslation;
                    }
                    continue;
                }

                if (analysis.ShouldPreserve != null && analysis.ShouldPreserve.Contains(text))
                {
                    strategy.TextsToPreserve.Add(text);
                    continue;
                }

                // 3. 检查白名单（常见需要翻译的类别）
                if (MatchesWhitelist(text))
                {
                    strategy.TextsToTranslate.Add(text);
                    continue;
                }

                // 4. 默认：长度大于等于3且包含字母的文本认为可能需要翻译
                if (text.Length >= 3 && text.Any(char.IsLetter))
                {
                    strategy.TextsToTranslate.Add(text);
                }
                else
                {
                    strategy.TextsToPreserve.Add(text);
                }
            }

            return strategy;
        }

        /// <summary>
        /// 检查是否匹配黑名单（不应翻译）
        /// </summary>
        private bool MatchesBlacklist(string text)
        {
            return TranslationBlacklist.Any(regex => regex.IsMatch(text.Trim()));
        }

        /// <summary>
        /// 检查是否匹配白名单（应该翻译）
        /// </summary>
        private bool MatchesWhitelist(string text)
        {
            var upperText = text.ToUpper().Trim();
            return TranslationWhitelist.Values.Any(list =>
                list.Any(keyword => upperText.Contains(keyword)));
        }

        /// <summary>
        /// 快速判断单个文本是否应该翻译（不调用 AI）
        /// </summary>
        public bool ShouldTranslateText(string text)
        {
            // 黑名单优先
            if (MatchesBlacklist(text))
                return false;

            // 白名单
            if (MatchesWhitelist(text))
                return true;

            // 基本规则
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
                return false;

            // 包含字母且长度合理
            return text.Any(char.IsLetter) && text.Length >= 3;
        }
    }

    #region 数据模型

    /// <summary>
    /// 翻译策略
    /// </summary>
    public class TranslationStrategy
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string DrawingType { get; set; } = "";
        public List<string> TextsToTranslate { get; set; } = new();
        public List<string> TextsToPreserve { get; set; } = new();
        public Dictionary<string, string> TranslationHints { get; set; } = new();
    }

    /// <summary>
    /// AI 图纸分析结果
    /// </summary>
    public class DrawingAnalysisResult
    {
        public string DrawingType { get; set; } = "";
        public List<string>? ShouldTranslate { get; set; }
        public List<string>? ShouldPreserve { get; set; }
        public List<string>? NeedsContext { get; set; }
        public Dictionary<string, TranslationSuggestion>? TranslationSuggestions { get; set; }
    }

    /// <summary>
    /// 翻译建议
    /// </summary>
    public class TranslationSuggestion
    {
        public string Category { get; set; } = "";
        public string Reason { get; set; } = "";
        public string SuggestedTranslation { get; set; } = "";
    }

    #endregion
}
