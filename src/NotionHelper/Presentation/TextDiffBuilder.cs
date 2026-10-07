using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace NotionHelper.Presentation;

internal enum TextDiffKind
{
    Unchanged,
    Removed,
    Added
}

internal sealed record TextDiffSegment(TextDiffKind Kind, string Text);

internal sealed record TextDiffResult(IReadOnlyList<TextDiffSegment> Segments, bool IsCoarse);

internal static class TextDiffBuilder
{
    private const int MaximumLcsCells = 1_000_000;

    internal static TextDiffResult Compare(string original, string proposed)
    {
        var originalTokens = Tokenize(original);
        var proposedTokens = Tokenize(proposed);
        if ((long)(originalTokens.Count + 1) * (proposedTokens.Count + 1) > MaximumLcsCells)
        {
            return new TextDiffResult(
                [
                    new TextDiffSegment(TextDiffKind.Removed, original),
                    new TextDiffSegment(TextDiffKind.Added, proposed)
                ],
                IsCoarse: true);
        }

        var lcs = new int[originalTokens.Count + 1, proposedTokens.Count + 1];
        for (var originalIndex = originalTokens.Count - 1; originalIndex >= 0; originalIndex--)
        {
            for (var proposedIndex = proposedTokens.Count - 1; proposedIndex >= 0; proposedIndex--)
            {
                lcs[originalIndex, proposedIndex] = originalTokens[originalIndex] == proposedTokens[proposedIndex]
                    ? lcs[originalIndex + 1, proposedIndex + 1] + 1
                    : Math.Max(lcs[originalIndex + 1, proposedIndex], lcs[originalIndex, proposedIndex + 1]);
            }
        }

        var segments = new List<MutableTextDiffSegment>();
        var source = 0;
        var result = 0;
        while (source < originalTokens.Count || result < proposedTokens.Count)
        {
            if (source < originalTokens.Count && result < proposedTokens.Count &&
                originalTokens[source] == proposedTokens[result])
            {
                Append(segments, TextDiffKind.Unchanged, originalTokens[source]);
                source++;
                result++;
            }
            else if (source < originalTokens.Count &&
                (result == proposedTokens.Count || lcs[source + 1, result] >= lcs[source, result + 1]))
            {
                Append(segments, TextDiffKind.Removed, originalTokens[source]);
                source++;
            }
            else
            {
                Append(segments, TextDiffKind.Added, proposedTokens[result]);
                result++;
            }
        }

        return new TextDiffResult(
            segments.Select(segment => new TextDiffSegment(segment.Kind, segment.Text.ToString())).ToArray(),
            IsCoarse: false);
    }

    internal static FlowDocument CreateDocument(TextDiffResult diff, bool proposed)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(10),
            FontFamily = new MediaFontFamily("Segoe UI"),
            FontSize = 14
        };
        var paragraph = new Paragraph();
        foreach (var segment in diff.Segments)
        {
            if ((!proposed && segment.Kind == TextDiffKind.Added) ||
                (proposed && segment.Kind == TextDiffKind.Removed))
            {
                continue;
            }

            var lines = segment.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (lines[lineIndex].Length > 0)
                {
                    paragraph.Inlines.Add(StyleInline(new Run(lines[lineIndex]), segment.Kind));
                }

                if (lineIndex < lines.Length - 1)
                {
                    paragraph.Inlines.Add(StyleInline(new LineBreak(), segment.Kind));
                }
            }
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    private static Inline StyleInline(Inline inline, TextDiffKind kind)
    {
        switch (kind)
        {
            case TextDiffKind.Removed:
                inline.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x9F, 0x2D, 0x2D));
                inline.Background = new SolidColorBrush(MediaColor.FromRgb(0xFC, 0xE8, 0xE6));
                inline.TextDecorations = TextDecorations.Strikethrough;
                break;
            case TextDiffKind.Added:
                inline.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1E, 0x6B, 0x3A));
                inline.Background = new SolidColorBrush(MediaColor.FromRgb(0xE4, 0xF3, 0xE8));
                inline.FontWeight = FontWeights.SemiBold;
                break;
        }

        return inline;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var currentKind = TokenKind.None;
        var current = new StringBuilder();
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var kind = GetTokenKind(element);
            if (kind != currentKind && current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }

            currentKind = kind;
            current.Append(element);
            if (kind == TokenKind.Symbol)
            {
                tokens.Add(current.ToString());
                current.Clear();
                currentKind = TokenKind.None;
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static TokenKind GetTokenKind(string element)
    {
        var rune = Rune.GetRuneAt(element, 0);
        if (Rune.IsWhiteSpace(rune))
        {
            return TokenKind.Whitespace;
        }

        var category = Rune.GetUnicodeCategory(rune);
        return Rune.IsLetterOrDigit(rune) ||
            category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark ||
            rune.Value == '_'
            ? TokenKind.Word
            : TokenKind.Symbol;
    }

    private static void Append(List<MutableTextDiffSegment> segments, TextDiffKind kind, string text)
    {
        if (segments.Count > 0 && segments[^1].Kind == kind)
        {
            segments[^1].Text.Append(text);
        }
        else
        {
            segments.Add(new MutableTextDiffSegment(kind, text));
        }
    }

    private sealed class MutableTextDiffSegment(TextDiffKind kind, string text)
    {
        public TextDiffKind Kind { get; } = kind;
        public StringBuilder Text { get; } = new(text);
    }

    private enum TokenKind
    {
        None,
        Word,
        Whitespace,
        Symbol
    }
}
