using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// Markdown渲染器 - 将Markdown文本转换为WPF FlowDocument
    /// 支持：标题、列表、加粗、代码块、分隔线、表格、链接
    /// </summary>
    public static class MarkdownRenderer
    {
        /// <summary>
        /// 渲染Markdown为FlowDocument
        /// </summary>
        public static FlowDocument RenderMarkdown(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                LineHeight = 20,
                PagePadding = new Thickness(0)
            };

            if (string.IsNullOrWhiteSpace(markdown))
                return doc;

            var lines = markdown.Split(new[] { '\n' }, StringSplitOptions.None);
            var i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                // 代码块
                if (line.TrimStart().StartsWith("```"))
                {
                    var codeBlock = new List<string>();
                    i++; // 跳过开始标记
                    while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                    {
                        codeBlock.Add(lines[i]);
                        i++;
                    }
                    i++; // 跳过结束标记
                    doc.Blocks.Add(CreateCodeBlock(string.Join("\n", codeBlock)));
                    continue;
                }

                // 表格（检测 | Header | Header |）
                if (line.Trim().StartsWith("|") && line.Trim().EndsWith("|"))
                {
                    var tableLines = new List<string>();
                    while (i < lines.Length && lines[i].Trim().StartsWith("|") && lines[i].Trim().EndsWith("|"))
                    {
                        tableLines.Add(lines[i]);
                        i++;
                    }

                    if (tableLines.Count >= 2) // 至少需要标题行和分隔行
                    {
                        doc.Blocks.Add(CreateTable(tableLines));
                        continue;
                    }
                }

                // 分隔线
                if (Regex.IsMatch(line.Trim(), @"^([-━]{3,}|[─]{3,})$"))
                {
                    doc.Blocks.Add(CreateSeparator());
                    i++;
                    continue;
                }

                // 标题
                var headerMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
                if (headerMatch.Success)
                {
                    int level = headerMatch.Groups[1].Length;
                    string headerText = headerMatch.Groups[2].Value;
                    doc.Blocks.Add(CreateHeader(headerText, level));
                    i++;
                    continue;
                }

                // 列表项（无序）
                if (Regex.IsMatch(line.TrimStart(), @"^[-•]\s+"))
                {
                    var listItems = new List<string>();
                    while (i < lines.Length && Regex.IsMatch(lines[i].TrimStart(), @"^[-•]\s+"))
                    {
                        listItems.Add(Regex.Replace(lines[i].TrimStart(), @"^[-•]\s+", ""));
                        i++;
                    }
                    doc.Blocks.Add(CreateUnorderedList(listItems));
                    continue;
                }

                // 列表项（有序）
                if (Regex.IsMatch(line.TrimStart(), @"^\d+\.\s+"))
                {
                    var listItems = new List<string>();
                    while (i < lines.Length && Regex.IsMatch(lines[i].TrimStart(), @"^\d+\.\s+"))
                    {
                        listItems.Add(Regex.Replace(lines[i].TrimStart(), @"^\d+\.\s+", ""));
                        i++;
                    }
                    doc.Blocks.Add(CreateOrderedList(listItems));
                    continue;
                }

                // 空行
                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                // 普通段落
                doc.Blocks.Add(CreateParagraph(line));
                i++;
            }

            return doc;
        }

        /// <summary>
        /// 创建标题
        /// </summary>
        private static Paragraph CreateHeader(string text, int level)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, level == 1 ? 8 : 6, 0, 4),
                FontWeight = FontWeights.Bold
            };

            // 标题大小：h1=20, h2=18, h3=16, h4-h6=14
            para.FontSize = level switch
            {
                1 => 20,
                2 => 18,
                3 => 16,
                _ => 14
            };

            // h1和h2用更亮的颜色
            if (level <= 2)
            {
                para.Foreground = Brushes.White;
            }

            AddInlineContent(para, text);
            return para;
        }

        /// <summary>
        /// 创建普通段落
        /// </summary>
        private static Paragraph CreateParagraph(string text)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            AddInlineContent(para, text);
            return para;
        }

        /// <summary>
        /// 创建无序列表
        /// </summary>
        private static List CreateUnorderedList(List<string> items)
        {
            var list = new List
            {
                Margin = new Thickness(20, 0, 0, 8),
                MarkerStyle = TextMarkerStyle.Disc
            };

            foreach (var item in items)
            {
                var listItem = new ListItem();
                var para = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                AddInlineContent(para, item);
                listItem.Blocks.Add(para);
                list.ListItems.Add(listItem);
            }

            return list;
        }

        /// <summary>
        /// 创建有序列表
        /// </summary>
        private static List CreateOrderedList(List<string> items)
        {
            var list = new List
            {
                Margin = new Thickness(20, 0, 0, 8),
                MarkerStyle = TextMarkerStyle.Decimal
            };

            foreach (var item in items)
            {
                var listItem = new ListItem();
                var para = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                AddInlineContent(para, item);
                listItem.Blocks.Add(para);
                list.ListItems.Add(listItem);
            }

            return list;
        }

        /// <summary>
        /// 创建代码块
        /// </summary>
        private static Paragraph CreateCodeBlock(string code)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 8, 0, 8),
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220))
            };

            para.Inlines.Add(new Run(code));
            return para;
        }

        /// <summary>
        /// 创建分隔线
        /// </summary>
        private static Paragraph CreateSeparator()
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            return para;
        }

        /// <summary>
        /// 添加内联内容（处理加粗、链接、行内代码等）
        /// </summary>
        private static void AddInlineContent(Paragraph paragraph, string text)
        {
            // 先处理链接 [text](url)
            var linkPattern = @"\[([^\]]+)\]\(([^\)]+)\)";
            var linkMatches = Regex.Matches(text, linkPattern);

            if (linkMatches.Count > 0)
            {
                int lastIndex = 0;
                foreach (Match match in linkMatches)
                {
                    // 添加链接前的文本
                    if (match.Index > lastIndex)
                    {
                        AddFormattedText(paragraph, text.Substring(lastIndex, match.Index - lastIndex));
                    }

                    // 创建超链接
                    string linkText = match.Groups[1].Value;
                    string url = match.Groups[2].Value;
                    var hyperlink = CreateHyperlink(linkText, url);
                    paragraph.Inlines.Add(hyperlink);

                    lastIndex = match.Index + match.Length;
                }

                // 添加剩余文本
                if (lastIndex < text.Length)
                {
                    AddFormattedText(paragraph, text.Substring(lastIndex));
                }
            }
            else
            {
                // 没有链接，直接处理其他格式
                AddFormattedText(paragraph, text);
            }
        }

        /// <summary>
        /// 添加格式化文本（加粗、行内代码）
        /// </summary>
        private static void AddFormattedText(Paragraph paragraph, string text)
        {
            // 处理加粗 **text**
            var boldPattern = @"\*\*(.+?)\*\*";
            var parts = Regex.Split(text, boldPattern);

            bool isBold = false;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    isBold = !isBold;
                    continue;
                }

                if (isBold)
                {
                    paragraph.Inlines.Add(new Run(part) { FontWeight = FontWeights.Bold });
                    isBold = false;
                }
                else
                {
                    // 检查是否有行内代码 `code`
                    var codePattern = @"`(.+?)`";
                    var codeParts = Regex.Split(part, codePattern);
                    bool isCode = false;

                    foreach (var codePart in codeParts)
                    {
                        if (string.IsNullOrEmpty(codePart))
                        {
                            isCode = !isCode;
                            continue;
                        }

                        if (isCode)
                        {
                            var run = new Run(codePart)
                            {
                                FontFamily = new FontFamily("Consolas, Courier New"),
                                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230))
                            };
                            paragraph.Inlines.Add(run);
                            isCode = false;
                        }
                        else
                        {
                            paragraph.Inlines.Add(new Run(codePart));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 创建超链接
        /// </summary>
        private static Hyperlink CreateHyperlink(string text, string url)
        {
            var hyperlink = new Hyperlink(new Run(text))
            {
                NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
                Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                TextDecorations = null
            };

            // 添加点击事件
            hyperlink.RequestNavigate += (sender, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"打开链接失败: {e.Uri}");
                }
            };

            return hyperlink;
        }

        /// <summary>
        /// 创建表格
        /// </summary>
        private static Table CreateTable(List<string> lines)
        {
            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1)
            };

            if (lines.Count < 2)
                return table;

            // 解析标题行
            var headerCells = ParseTableRow(lines[0]);
            int columnCount = headerCells.Count;

            // 创建列
            for (int i = 0; i < columnCount; i++)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            }

            // 创建表头行组
            var headerRowGroup = new TableRowGroup();
            var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)) };

            foreach (var cellText in headerCells)
            {
                var cell = new TableCell(new Paragraph(new Run(cellText))
                {
                    Margin = new Thickness(0),
                    FontWeight = FontWeights.Bold
                })
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };
                headerRow.Cells.Add(cell);
            }

            headerRowGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerRowGroup);

            // 跳过分隔行（第二行），处理数据行
            var bodyRowGroup = new TableRowGroup();
            for (int i = 2; i < lines.Count; i++)
            {
                var rowCells = ParseTableRow(lines[i]);
                var row = new TableRow();

                foreach (var cellText in rowCells)
                {
                    var para = new Paragraph { Margin = new Thickness(0) };
                    AddInlineContent(para, cellText);

                    var cell = new TableCell(para)
                    {
                        Padding = new Thickness(8, 4, 8, 4),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                        BorderThickness = new Thickness(0, 0, 1, 1)
                    };
                    row.Cells.Add(cell);
                }

                bodyRowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(bodyRowGroup);
            return table;
        }

        /// <summary>
        /// 解析表格行
        /// </summary>
        private static List<string> ParseTableRow(string line)
        {
            var cells = new List<string>();
            var parts = line.Split('|');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    cells.Add(trimmed);
                }
            }

            return cells;
        }
    }
}
