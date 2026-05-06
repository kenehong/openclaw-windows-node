using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace OpenClawTray.Services.Chat;

/// <summary>
/// Lightweight, streaming-friendly Markdown renderer for assistant bubbles.
///
/// Supported subset (per docs/NATIVE_CHAT_MIGRATION.md §4.1):
///   • Paragraphs (blank-line separated)
///   • Fenced code blocks (```lang … ```), language label optional
///   • Inline code (`code`)
///   • Links [text](url)
///
/// Out of scope for M1: lists, tables, headings, syntax highlighting, raw HTML.
/// Re-render is destructive (clear+rebuild) — fine for stream rates we hit.
/// </summary>
public static class ChatMarkdownRenderer
{
    private static readonly Regex InlineCodeOrLink = new(
        @"(?<code>`[^`\n]+`)|(?<link>\[(?<linktext>[^\]]+)\]\((?<url>[^\)\s]+)\))",
        RegexOptions.Compiled);

    /// <summary>
    /// Render markdown into a sequence of UIElements. Caller appends them into a vertical
    /// StackPanel / parent. Caller is responsible for clearing previous output before each
    /// re-render of a streaming bubble.
    /// </summary>
    public static IList<UIElement> Render(string? text)
    {
        var output = new List<UIElement>();
        if (string.IsNullOrEmpty(text)) return output;

        // First pass: split off fenced code blocks. Anything between the fences is preserved verbatim.
        var blocks = SplitFences(text);
        foreach (var block in blocks)
        {
            if (block.IsCode)
            {
                output.Add(BuildCodeBlock(block.Language, block.Content));
            }
            else
            {
                // Split into paragraphs on blank lines.
                var paragraphs = Regex.Split(block.Content, @"\n\s*\n");
                foreach (var paragraph in paragraphs)
                {
                    var trimmed = paragraph.TrimEnd();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    output.Add(BuildParagraph(trimmed));
                }
            }
        }
        return output;
    }

    private record struct Block(bool IsCode, string Language, string Content);

    private static List<Block> SplitFences(string text)
    {
        var result = new List<Block>();
        int i = 0;
        while (i < text.Length)
        {
            // Find next ```
            int fence = text.IndexOf("```", i, StringComparison.Ordinal);
            if (fence < 0)
            {
                if (i < text.Length) result.Add(new Block(false, "", text[i..]));
                break;
            }
            if (fence > i) result.Add(new Block(false, "", text[i..fence]));

            int afterOpen = fence + 3;
            // Capture optional language up to newline.
            int newline = text.IndexOf('\n', afterOpen);
            string lang = "";
            int contentStart;
            if (newline < 0)
            {
                // Unterminated open fence — treat the rest as still-streaming code.
                lang = text[afterOpen..].Trim();
                result.Add(new Block(true, lang, ""));
                return result;
            }
            else
            {
                lang = text[afterOpen..newline].Trim();
                contentStart = newline + 1;
            }

            int closing = text.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (closing < 0)
            {
                // Streaming: code block isn't closed yet.
                result.Add(new Block(true, lang, text[contentStart..]));
                return result;
            }
            // Trim trailing newline before closing fence if present.
            int contentEnd = closing;
            if (contentEnd > contentStart && text[contentEnd - 1] == '\n') contentEnd--;
            result.Add(new Block(true, lang, text[contentStart..contentEnd]));
            i = closing + 3;
            if (i < text.Length && text[i] == '\n') i++;
        }
        return result;
    }

    private static UIElement BuildCodeBlock(string language, string code)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x1E, 0x1E, 0x1E)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 4)
        };
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        if (!string.IsNullOrWhiteSpace(language))
        {
            stack.Children.Add(new TextBlock
            {
                Text = language,
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x9C, 0x9C, 0x9C)),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }
        stack.Children.Add(new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE6, 0xE6, 0xE6)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            IsTextSelectionEnabled = true
        });
        border.Child = stack;
        return border;
    }

    private static UIElement BuildParagraph(string text)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Margin = new Thickness(0, 2, 0, 2)
        };

        int last = 0;
        foreach (Match m in InlineCodeOrLink.Matches(text))
        {
            if (m.Index > last)
                tb.Inlines.Add(new Run { Text = text[last..m.Index] });

            if (m.Groups["code"].Success)
            {
                var raw = m.Groups["code"].Value;
                var inner = raw.Substring(1, raw.Length - 2);
                tb.Inlines.Add(new Run
                {
                    Text = inner,
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC8, 0x32, 0x6E))
                });
            }
            else if (m.Groups["link"].Success)
            {
                try
                {
                    var hyper = new Hyperlink { NavigateUri = new Uri(m.Groups["url"].Value) };
                    hyper.Inlines.Add(new Run { Text = m.Groups["linktext"].Value });
                    tb.Inlines.Add(hyper);
                }
                catch
                {
                    tb.Inlines.Add(new Run { Text = m.Value });
                }
            }
            last = m.Index + m.Length;
        }
        if (last < text.Length)
            tb.Inlines.Add(new Run { Text = text[last..] });

        return tb;
    }
}
