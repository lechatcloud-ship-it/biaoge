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
    /// 专门用于AI助手的实时流式输出，支持逐块接收内容并实时渲染Markdown
    ///
    /// 核心特性：
    /// - 实时Markdown渲染（每150ms更新一次）
    /// - 性能优化（Throttling机制避免过度渲染）
    /// - 支持粗体、代码块、列表等常见Markdown语法
    /// </summary>
    public class StreamingMarkdownRenderer
    {
        private RichTextBox _richTextBox;
        private StringBuilder _content = new StringBuilder();
        private DispatcherTimer _updateTimer;
        private bool _isDirty = false;
        private DateTime _lastUpdate = DateTime.MinValue;

        // Throttling配置：最快每150ms更新一次
        private const int UpdateIntervalMs = 150;

        public StreamingMarkdownRenderer(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;

            // 初始化更新定时器
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs)
            };
            _updateTimer.Tick += OnUpdateTick;
        }

        /// <summary>
        /// 追加流式内容块
        /// </summary>
        public void AppendChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            _content.Append(chunk);
            _isDirty = true;

            // 启动定时器（如果未运行）
            if (!_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }

            // 如果距离上次更新超过阈值，立即更新
            if ((DateTime.Now - _lastUpdate).TotalMilliseconds > UpdateIntervalMs * 2)
            {
                ForceUpdate();
            }
        }

        /// <summary>
        /// 完成流式输出，强制更新
        /// </summary>
        public void Complete()
        {
            _updateTimer.Stop();
            if (_isDirty)
            {
                ForceUpdate();
            }
        }

        /// <summary>
        /// 定时器触发更新
        /// </summary>
        private void OnUpdateTick(object sender, EventArgs e)
        {
            if (_isDirty)
            {
                ForceUpdate();
            }
        }

        /// <summary>
        /// 强制更新Markdown渲染
        /// </summary>
        private void ForceUpdate()
        {
            try
            {
                var markdownText = _content.ToString();

                // 渲染Markdown为FlowDocument
                var document = MarkdownRenderer.RenderMarkdown(markdownText);

                // 更新RichTextBox
                _richTextBox.Document = document;

                _isDirty = false;
                _lastUpdate = DateTime.Now;

                Log.Verbose($"流式Markdown已更新，内容长度: {markdownText.Length}");
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
            _content.Clear();
            _isDirty = false;
            _updateTimer.Stop();
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
