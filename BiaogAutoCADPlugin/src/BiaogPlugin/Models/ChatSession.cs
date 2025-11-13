using System;
using System.Collections.Generic;
using BiaogPlugin.Services;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// AI助手对话会话
    /// </summary>
    public class ChatSession
    {
        /// <summary>
        /// 会话唯一ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 会话标题（自动从第一条消息生成或用户自定义）
        /// </summary>
        public string Title { get; set; } = "新对话";

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 对话消息历史
        /// </summary>
        public List<ChatMessage> Messages { get; set; } = new();

        /// <summary>
        /// 是否启用深度思考模式
        /// </summary>
        public bool DeepThinkingEnabled { get; set; } = false;

        /// <summary>
        /// 获取会话摘要（用于列表显示）
        /// </summary>
        public string GetSummary()
        {
            if (Messages.Count == 0)
                return "空对话";

            // 返回第一条用户消息的前30个字符
            foreach (var msg in Messages)
            {
                if (msg.Role == "user")
                {
                    return msg.Content.Length > 30
                        ? msg.Content.Substring(0, 30) + "..."
                        : msg.Content;
                }
            }

            return "无内容";
        }

        /// <summary>
        /// 自动生成标题（从第一条用户消息）
        /// </summary>
        public void AutoGenerateTitle()
        {
            if (Messages.Count == 0)
            {
                Title = "新对话";
                return;
            }

            foreach (var msg in Messages)
            {
                if (msg.Role == "user")
                {
                    // 取前15个字符作为标题
                    Title = msg.Content.Length > 15
                        ? msg.Content.Substring(0, 15) + "..."
                        : msg.Content;
                    return;
                }
            }

            Title = $"对话 {CreateTime:yyyy-MM-dd HH:mm}";
        }
    }
}
