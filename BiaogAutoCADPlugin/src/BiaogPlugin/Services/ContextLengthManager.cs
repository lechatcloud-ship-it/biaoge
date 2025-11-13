using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 上下文长度管理器 - 防止超过模型输入限制
    ///
    /// 阿里云百炼qwen3-max-preview限制：
    /// - 上下文长度: 256K tokens
    /// - 最大输入长度: 252K tokens
    /// - 思考模式最大输出: 32K tokens
    /// - 非思考模式最大输出: 64K tokens
    /// </summary>
    public class ContextLengthManager
    {
        // 保守估计，保持在200K以内，留出足够的输出空间
        private const int MaxInputTokens = 200_000;

        // 最少保留的消息对数（user + assistant）
        private const int MinMessagePairs = 3;

        /// <summary>
        /// 裁剪消息历史，确保不超过最大输入长度
        /// </summary>
        /// <param name="messages">原始消息列表</param>
        /// <param name="systemPrompt">系统提示词（始终保留）</param>
        /// <returns>裁剪后的消息列表</returns>
        public List<ChatMessage> TrimMessages(
            List<ChatMessage> messages,
            string systemPrompt)
        {
            if (messages == null || messages.Count == 0)
                return new List<ChatMessage>();

            // 估算总Token数
            int estimatedTokens = EstimateTokens(messages, systemPrompt);

            // 如果未超限，直接返回
            if (estimatedTokens <= MaxInputTokens)
            {
                Log.Debug($"上下文长度正常: {estimatedTokens} tokens ({messages.Count} 条消息)");
                return messages.ToList();
            }

            Log.Information($"上下文超限: {estimatedTokens} tokens，开始裁剪...");

            // 裁剪策略：保留最近的消息
            var trimmedMessages = TrimFromOldest(messages, systemPrompt);

            int finalTokens = EstimateTokens(trimmedMessages, systemPrompt);
            Log.Information($"裁剪完成: {finalTokens} tokens ({trimmedMessages.Count} 条消息，原{messages.Count}条)");

            return trimmedMessages;
        }

        /// <summary>
        /// 从最旧的消息开始裁剪
        /// </summary>
        private List<ChatMessage> TrimFromOldest(
            List<ChatMessage> messages,
            string systemPrompt)
        {
            // 计算system prompt的token数
            int systemTokens = EstimateTokens(systemPrompt);
            int remainingTokens = MaxInputTokens - systemTokens;

            // 从最新的消息开始往回取
            var result = new List<ChatMessage>();
            int currentTokens = 0;
            int messagePairs = 0;

            // 倒序遍历，优先保留最近的消息
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                int messageTokens = EstimateTokens(message.Content);

                // 检查是否还有空间
                if (currentTokens + messageTokens > remainingTokens)
                {
                    // 如果至少保留了MinMessagePairs对，可以停止
                    if (messagePairs >= MinMessagePairs)
                        break;
                }

                result.Insert(0, message);
                currentTokens += messageTokens;

                // 统计消息对数（user-assistant）
                if (message.Role == "assistant")
                    messagePairs++;
            }

            // 确保至少保留MinMessagePairs对消息
            if (result.Count < MinMessagePairs * 2)
            {
                Log.Warning($"保留消息数过少({result.Count})，强制保留最近{MinMessagePairs}对");
                int targetCount = Math.Min(MinMessagePairs * 2, messages.Count);
                result = messages.Skip(messages.Count - targetCount).ToList();
            }

            return result;
        }

        /// <summary>
        /// 估算Token数量
        ///
        /// 简化估算规则：
        /// - 中文字符: 1字符 ≈ 1.5 tokens
        /// - 英文单词: 1单词 ≈ 1.3 tokens
        /// - 空格、标点: 按0.5 token计算
        /// </summary>
        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int chineseChars = 0;
            int englishWords = 0;
            int otherChars = 0;

            bool inWord = false;
            foreach (char c in text)
            {
                if (IsChinese(c))
                {
                    chineseChars++;
                    inWord = false;
                }
                else if (char.IsLetter(c))
                {
                    if (!inWord)
                    {
                        englishWords++;
                        inWord = true;
                    }
                }
                else
                {
                    otherChars++;
                    inWord = false;
                }
            }

            // 估算公式
            double tokens = (chineseChars * 1.5) + (englishWords * 1.3) + (otherChars * 0.5);
            return (int)Math.Ceiling(tokens);
        }

        /// <summary>
        /// 估算消息列表的Token总数
        /// </summary>
        public int EstimateTokens(List<ChatMessage> messages, string systemPrompt)
        {
            int total = EstimateTokens(systemPrompt);

            foreach (var message in messages)
            {
                total += EstimateTokens(message.Content);
                // 加上消息元数据的开销（role等）
                total += 10;
            }

            return total;
        }

        /// <summary>
        /// 判断是否为中文字符
        /// </summary>
        private bool IsChinese(char c)
        {
            return c >= 0x4E00 && c <= 0x9FA5;
        }

        /// <summary>
        /// 获取上下文使用率
        /// </summary>
        /// <param name="currentTokens">当前Token数</param>
        /// <returns>使用率（0-1）</returns>
        public double GetUsageRate(int currentTokens)
        {
            return (double)currentTokens / MaxInputTokens;
        }

        /// <summary>
        /// 检查是否需要裁剪
        /// </summary>
        public bool ShouldTrim(int currentTokens)
        {
            return currentTokens > MaxInputTokens;
        }

        /// <summary>
        /// 获取最大输入Token数
        /// </summary>
        public int GetMaxInputTokens() => MaxInputTokens;

        /// <summary>
        /// 获取Token使用统计信息
        /// </summary>
        public string GetUsageInfo(List<ChatMessage> messages, string systemPrompt)
        {
            int tokens = EstimateTokens(messages, systemPrompt);
            double rate = GetUsageRate(tokens);
            int maxOutput = tokens <= 200_000 ? 32_000 : 0; // 思考模式输出限制

            return $"Token使用: {tokens:N0} / {MaxInputTokens:N0} ({rate:P1})\n" +
                   $"消息数: {messages.Count}\n" +
                   $"可用输出: {maxOutput:N0} tokens";
        }
    }
}
