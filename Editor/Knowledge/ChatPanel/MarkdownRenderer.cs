#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// 将 Markdown 文本转换为 Unity IMGUI Rich Text。
    /// 支持：粗体、斜体、行内代码、标题、无序/有序列表、代码块、表格、引用块、分隔线。
    /// 安全约束：仅使用 &lt;b&gt;/&lt;i&gt;/&lt;color&gt; 标签，禁止 &lt;size&gt;（会导致 wordWrap 布局震荡）。
    /// </summary>
    public static class MarkdownRenderer
    {
        // ── 颜色常量 ──
        private const string CodeColor       = "#9CDCFE";   // 行内代码
        private const string CodeBlockColor  = "#C8C8C8";   // 代码块文本
        private const string CodeLangColor   = "#6A9955";   // 代码块语言标签
        private const string HeadingColor    = "#E0E8F0";   // 标题
        private const string TableHeadColor  = "#7EC8E3";   // 表头
        private const string TableBorderColor = "#555555";  // 表格边框
        private const string BlockquoteColor = "#8ABEB7";   // 引用块
        private const string ListBullet      = "  \u2022 "; // • 圆点

        // ── 表格分隔行正则 ──
        private static readonly Regex TableSepRegex = new(@"^\|?[\s\-:|]+\|[\s\-:|]*\|?$");

        /// <summary>
        /// 将 Markdown 文本转换为 Unity Rich Text（单 Label 渲染用）。
        /// 直接线性扫描，输出纯 RichText，不使用 &lt;size&gt; 标签，确保 IMGUI 布局安全。
        /// </summary>
        public static string Convert(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";

            var sb = new StringBuilder(markdown.Length + 256);
            var lines = markdown.Split('\n');
            bool inCodeBlock = false;
            string? codeBlockLang = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimEnd('\r').TrimStart();

                // ── 代码块 ``` ──
                if (trimmed.StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                        codeBlockLang = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : null;
                        // 代码块起始标记：显示语言标签
                        if (!string.IsNullOrEmpty(codeBlockLang))
                            sb.AppendLine($"<color={CodeLangColor}>[{codeBlockLang}]</color>");
                        sb.Append($"<color={CodeBlockColor}>");
                    }
                    else
                    {
                        inCodeBlock = false;
                        codeBlockLang = null;
                        sb.AppendLine("</color>");
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    // 代码块内：保持原样缩进，仅着色（外层已有 <color>）
                    sb.AppendLine($"  {line.TrimEnd('\r')}");
                    continue;
                }

                // ── 标题 # ## ### ──（不使用 <size>，仅用 <b><color> 区分）
                if (trimmed.StartsWith("### "))
                {
                    sb.AppendLine($"<b><color={HeadingColor}>{ConvertInline(trimmed.Substring(4))}</color></b>");
                    continue;
                }
                if (trimmed.StartsWith("## "))
                {
                    sb.AppendLine($"<b><color={HeadingColor}>\u25A0 {ConvertInline(trimmed.Substring(3))}</color></b>");
                    continue;
                }
                if (trimmed.StartsWith("# "))
                {
                    sb.AppendLine($"<b><color={HeadingColor}>\u25A0 {ConvertInline(trimmed.Substring(2))}</color></b>");
                    continue;
                }

                // ── 引用块 > ──
                if (trimmed.StartsWith("> "))
                {
                    sb.AppendLine($"<color={BlockquoteColor}>  \u2502 {ConvertInline(trimmed.Substring(2))}</color>");
                    continue;
                }
                if (trimmed == ">")
                {
                    sb.AppendLine($"<color={BlockquoteColor}>  \u2502</color>");
                    continue;
                }

                // ── 无序列表 - / * ──
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    sb.AppendLine($"{ListBullet}{ConvertInline(trimmed.Substring(2))}");
                    continue;
                }

                // ── 有序列表 1. 2. 等 ──
                var orderedMatch = Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
                if (orderedMatch.Success)
                {
                    sb.AppendLine($"  {orderedMatch.Groups[1].Value}. {ConvertInline(orderedMatch.Groups[2].Value)}");
                    continue;
                }

                // ── 分隔线 --- / *** / ___ ──
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    sb.AppendLine($"<color={TableBorderColor}>\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500</color>");
                    continue;
                }

                // ── 表格分隔行（|---|---| 等）→ 绘制分隔线 ──
                if (TableSepRegex.IsMatch(trimmed))
                {
                    sb.AppendLine($"<color={TableBorderColor}>  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500</color>");
                    continue;
                }

                // ── 表格行 | cell | cell | ──
                if (trimmed.StartsWith("|"))
                {
                    var cells = ParseTableRow(trimmed);
                    if (cells.Count > 0)
                    {
                        // 判断是否是表头（下一行是分隔行）
                        bool isHeader = i + 1 < lines.Length &&
                                        TableSepRegex.IsMatch(lines[i + 1].TrimEnd('\r').TrimStart());
                        var rowSb = new StringBuilder();
                        rowSb.Append("  ");
                        for (int c = 0; c < cells.Count; c++)
                        {
                            if (c > 0) rowSb.Append($" <color={TableBorderColor}>\u2502</color> ");
                            string cellText = ConvertInline(cells[c]);
                            if (isHeader)
                                rowSb.Append($"<b><color={TableHeadColor}>{cellText}</color></b>");
                            else
                                rowSb.Append(cellText);
                        }
                        sb.AppendLine(rowSb.ToString());
                        continue;
                    }
                }

                // ── 普通段落 ──
                sb.AppendLine(ConvertInline(line.TrimEnd('\r')));
            }

            // 未闭合的代码块：补充关闭标签
            if (inCodeBlock)
                sb.AppendLine("</color>");

            // 移除末尾多余换行
            while (sb.Length > 0 && (sb[sb.Length - 1] == '\n' || sb[sb.Length - 1] == '\r'))
                sb.Length--;

            return sb.ToString();
        }

        /// <summary>
        /// 处理行内 Markdown：粗体、斜体、行内代码。
        /// </summary>
        private static string ConvertInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // 行内代码 `code` → <color>code</color>（必须在粗体/斜体之前处理）
            text = Regex.Replace(text, @"`([^`]+)`", $"<color={CodeColor}>$1</color>");

            // 粗体 **text** → <b>text</b>
            text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "<b>$1</b>");

            // 斜体 *text* → <i>text</i>（排除已处理的粗体）
            text = Regex.Replace(text, @"(?<!\*)\*([^*]+)\*(?!\*)", "<i>$1</i>");

            return text;
        }

        /// <summary>解析表格行：| cell1 | cell2 | → ["cell1", "cell2"]</summary>
        private static System.Collections.Generic.List<string> ParseTableRow(string line)
        {
            var cells = new System.Collections.Generic.List<string>();
            // 去掉首尾的 |
            if (line.StartsWith("|")) line = line.Substring(1);
            if (line.EndsWith("|")) line = line.Substring(0, line.Length - 1);
            var parts = line.Split('|');
            foreach (var p in parts)
                cells.Add(p.Trim());
            return cells;
        }
    }
}
