using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 流式Markdown渲染器
    /// ✅ 重大修复：AutoCAD PaletteSet中立即更新，不使用定时器
    ///
    /// 核心特性：
    /// - 实时Markdown渲染（每个chunk立即更新）
    /// - 性能优化（节流机制：最快每50ms更新一次）
    /// - 支持粗体、代码块、列表等常见Markdown语法
    /// </summary>
    public class StreamingMarkdownRenderer
    {
        private RichTextBox _richTextBox;
        private StringBuilder _content = new StringBuilder();
        private DateTime _lastUpdate = DateTime.MinValue;
        private int _pendingChunks = 0;
        private readonly object _lock = new object();

        // ✅ 节流配置：最快每50ms更新一次（降低从150ms，提高响应速度）
        private const int ThrottleMs = 50;

        public StreamingMarkdownRenderer(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
            Log.Debug("StreamingMarkdownRenderer已初始化");
        }

        /// <summary>
        /// ✅ 追加流式内容块 - 立即更新模式
        /// 关键修复：调用者已通过syncContext.Post在UI线程，此处无需再dispatch
        /// </summary>
        public void AppendChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            lock (_lock)
            {
                _content.Append(chunk);
                _pendingChunks++;
            }

            // ✅ 节流更新：避免过于频繁的渲染
            var timeSinceLastUpdate = (DateTime.Now - _lastUpdate).TotalMilliseconds;

            if (timeSinceLastUpdate >= ThrottleMs || _pendingChunks == 1)
            {
                // ✅ 直接更新，无需Dispatcher（调用者已在UI线程）
                // 移除三重调度，实现真正的实时流式显示
                try
                {
                    ForceUpdate();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "流式更新失败");
                }
            }
        }

        /// <summary>
        /// 完成流式输出，强制最终更新
        /// 调用者已在UI线程，无需Dispatcher
        /// </summary>
        public void Complete()
        {
            lock (_lock)
            {
                if (_pendingChunks > 0 || _content.Length > 0)
                {
                    // ✅ 直接更新，确保所有内容都被渲染
                    ForceUpdate();
                    Log.Debug($"流式输出完成，最终内容长度: {_content.Length}");
                }
            }
        }

        /// <summary>
        /// ✅ 强制更新Markdown渲染 - 线程安全
        /// </summary>
        private void ForceUpdate()
        {
            try
            {
                string markdownText;
                lock (_lock)
                {
                    markdownText = _content.ToString();
                    _pendingChunks = 0;
                }

                if (string.IsNullOrEmpty(markdownText))
                    return;

                // 渲染Markdown为FlowDocument
                var document = MarkdownRenderer.RenderMarkdown(markdownText);

                // 更新RichTextBox
                _richTextBox.Document = document;

                // ✅ 自动滚动到底部（显示最新内容）
                _richTextBox.ScrollToEnd();

                _lastUpdate = DateTime.Now;

                Log.Verbose($"[流式] 已更新 {markdownText.Length} 字符");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "流式Markdown渲染失败");
            }
        }

        /// <summary>
        /// 清空内容
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _content.Clear();
                _pendingChunks = 0;
            }
            _lastUpdate = DateTime.MinValue;
        }

        /// <summary>
        /// 获取当前内容
        /// </summary>
        public string GetContent()
        {
            return _content.ToString();
        }
    }
}
