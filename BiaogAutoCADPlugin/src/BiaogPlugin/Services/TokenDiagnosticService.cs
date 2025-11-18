using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// Token使用诊断服务 - 验证translation_options实际token消耗
    ///
    /// 核心问题：用户反馈"如果输入提示词就占了6K提示词那剩下翻译还有多少呢"
    /// 目标：实际测量 terms、tm_list、domains 占用多少 tokens
    /// </summary>
    public class TokenDiagnosticService
    {
        /// <summary>
        /// 共享HttpClient实例 - 防止Socket耗尽
        /// </summary>
        /// <remarks>
        /// HttpClient设计为单例使用，避免每次请求创建新实例导致socket耗尽。
        /// 参考：https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
        /// </remarks>
        private static readonly HttpClient _sharedHttpClient = new HttpClient();

        /// <summary>
        /// Token使用报告
        /// </summary>
        public class TokenUsageReport
        {
            public int PromptTokens { get; set; }
            public int CompletionTokens { get; set; }
            public int TotalTokens { get; set; }
            public int EstimatedSystemTokens { get; set; }  // 估算的系统参数tokens
            public int AvailableForContent { get; set; }    // 剩余可用于内容的tokens
            public string TestInput { get; set; } = "";
            public string Model { get; set; } = "";
        }

        /// <summary>
        /// ✅ 核心测试：测量translation_options实际token消耗
        ///
        /// 测试方法：
        /// 1. 调用API翻译一个短文本（10字符）
        /// 2. 查看返回的usage.prompt_tokens
        /// 3. 计算：系统参数tokens = prompt_tokens - 实际文本tokens
        /// </summary>
        public static async Task<TokenUsageReport> MeasureTranslationOptionsTokens()
        {
            try
            {
                var apiClient = ServiceLocator.Get<BailianApiClient>();
                if (apiClient == null)
                {
                    throw new InvalidOperationException("BailianApiClient未注册");
                }

                // 测试用的短文本（确保很短，方便计算系统参数占用）
                var testText = "测试文本ABC123";  // 10字符，约10-15 tokens
                var targetLang = "English";
                var sourceLang = "Chinese";

                Log.Information("开始Token诊断测试...");

                // 准备请求体
                var requestBody = new
                {
                    model = "qwen-mt-flash",
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = testText
                        }
                    },
                    translation_options = new
                    {
                        source_lang = sourceLang,
                        target_lang = targetLang,
                        domains = EngineeringTranslationConfig.DomainPrompt,
                        terms = EngineeringTranslationConfig.GetApiTerms(sourceLang, targetLang),
                        tm_list = EngineeringTranslationConfig.GetApiTranslationMemory(sourceLang, targetLang)
                    },
                    temperature = 0.3
                };

                // 获取API密钥
                var config = ServiceLocator.Get<ConfigManager>();
                var apiKey = config?.Config?.BailianApiKey ?? "";

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("API密钥未配置");
                }

                // 发送请求（使用共享HttpClient）
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions")
                {
                    Content = JsonContent.Create(requestBody)
                };
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await _sharedHttpClient.SendAsync(httpRequest);
                var responseJson = await response.Content.ReadAsStringAsync();

                Log.Debug($"Token诊断响应: {responseJson}");

                // 解析响应
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("usage", out var usage))
                {
                    Log.Warning("响应中未找到usage信息");
                    return new TokenUsageReport { TestInput = testText, Model = "qwen-mt-flash" };
                }

                var promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                var completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                var totalTokens = usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;

                // 估算系统参数占用（粗略估计：测试文本10字符约15 tokens）
                var testTextTokens = 15;
                var systemTokens = Math.Max(0, promptTokens - testTextTokens);

                var report = new TokenUsageReport
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = totalTokens,
                    EstimatedSystemTokens = systemTokens,
                    AvailableForContent = Math.Max(0, 8192 - systemTokens - 500), // 留500 tokens余量
                    TestInput = testText,
                    Model = "qwen-mt-flash"
                };

                Log.Information($"📊 Token使用报告:");
                Log.Information($"  输入Tokens: {promptTokens}");
                Log.Information($"  输出Tokens: {completionTokens}");
                Log.Information($"  总计Tokens: {totalTokens}");
                Log.Information($"  估算系统参数Tokens: {systemTokens}");
                Log.Information($"  可用于内容Tokens: {report.AvailableForContent}");

                return report;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Token诊断测试失败");
                throw;
            }
        }

        /// <summary>
        /// 对比测试：有无translation_options的token差异
        /// </summary>
        public static async Task<(int withOptions, int withoutOptions, int difference)> CompareWithAndWithoutOptions()
        {
            try
            {
                var config = ServiceLocator.Get<ConfigManager>();
                var apiKey = config?.Config?.BailianApiKey ?? "";
                var testText = "测试ABC";

                // 测试1：带translation_options
                var requestWith = new
                {
                    model = "qwen-mt-flash",
                    messages = new[] { new { role = "user", content = testText } },
                    translation_options = new
                    {
                        source_lang = "Chinese",
                        target_lang = "English",
                        domains = EngineeringTranslationConfig.DomainPrompt,
                        terms = EngineeringTranslationConfig.GetApiTerms("Chinese", "English"),
                        tm_list = EngineeringTranslationConfig.GetApiTranslationMemory("Chinese", "English")
                    }
                };

                // 测试2：不带translation_options
                var requestWithout = new
                {
                    model = "qwen-mt-flash",
                    messages = new[] { new { role = "user", content = testText } },
                    translation_options = new
                    {
                        source_lang = "Chinese",
                        target_lang = "English"
                        // 不带 domains, terms, tm_list
                    }
                };

                // 请求1（使用共享HttpClient）
                var req1 = new HttpRequestMessage(HttpMethod.Post, "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions")
                {
                    Content = JsonContent.Create(requestWith)
                };
                req1.Headers.Add("Authorization", $"Bearer {apiKey}");
                var resp1 = await _sharedHttpClient.SendAsync(req1);
                var json1 = await resp1.Content.ReadAsStringAsync();
                var tokens1 = ExtractPromptTokens(json1);

                // 请求2
                var req2 = new HttpRequestMessage(HttpMethod.Post, "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions")
                {
                    Content = JsonContent.Create(requestWithout)
                };
                req2.Headers.Add("Authorization", $"Bearer {apiKey}");
                var resp2 = await _sharedHttpClient.SendAsync(req2);
                var json2 = await resp2.Content.ReadAsStringAsync();
                var tokens2 = ExtractPromptTokens(json2);

                var difference = tokens1 - tokens2;

                Log.Information($"📊 对比测试结果:");
                Log.Information($"  带完整options: {tokens1} tokens");
                Log.Information($"  仅基础options: {tokens2} tokens");
                Log.Information($"  差值（系统参数占用）: {difference} tokens");

                return (tokens1, tokens2, difference);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "对比测试失败");
                throw;
            }
        }

        private static int ExtractPromptTokens(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var pt))
                    {
                        return pt.GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "解析prompt_tokens失败");
            }
            return 0;
        }
    }
}
