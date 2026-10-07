using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using NotionHelper.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace NotionHelper.Presentation;

public static class PreviewDocumentBuilder
{
    public static FlowDocument Create(ImprovementResult result)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(12),
            FontFamily = new MediaFontFamily("Segoe UI"),
            FontSize = 14
        };

        for (var index = 0; index < result.Blocks.Count; index++)
        {
            var block = result.Blocks[index];
            switch (block.Type)
            {
                case "paragraph":
                    document.Blocks.Add(CreateParagraph(block));
                    break;
                case "heading":
                    document.Blocks.Add(CreateHeading(block));
                    break;
                case "bullet":
                    document.Blocks.Add(CreateList(result.Blocks, ref index, "bullet"));
                    break;
                case "numbered":
                    document.Blocks.Add(CreateList(result.Blocks, ref index, "numbered"));
                    break;
                case "quote":
                    document.Blocks.Add(CreateQuote(block));
                    break;
                case "code":
                    document.Blocks.Add(CreateCode(block));
                    break;
                case "table":
                    document.Blocks.Add(CreateTable(block));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported preview block type: {block.Type}");
            }
        }

        return document;
    }

    private static Paragraph CreateParagraph(ContentBlock block)
    {
        var paragraph = new Paragraph { Foreground = ForegroundFor(block.Color) };
        AppendText(paragraph, block.Text, block.Bold);
        return paragraph;
    }

    private static Paragraph CreateHeading(ContentBlock block)
    {
        var paragraph = new Paragraph
        {
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = ForegroundFor(block.Color),
            Margin = new Thickness(0, 8, 0, 6)
        };
        AppendText(paragraph, block.Text, bold: true);
        return paragraph;
    }

    private static List CreateList(IReadOnlyList<ContentBlock> blocks, ref int index, string type)
    {
        var list = new List
        {
            MarkerStyle = type == "bullet" ? TextMarkerStyle.Disc : TextMarkerStyle.Decimal,
            Margin = new Thickness(0, 2, 0, 8)
        };

        while (index < blocks.Count && blocks[index].Type == type)
        {
            var block = blocks[index];
            var paragraph = new Paragraph { Foreground = ForegroundFor(block.Color) };
            AppendText(paragraph, block.Text, block.Bold);
            list.ListItems.Add(new ListItem(paragraph));

            if (index + 1 < blocks.Count && blocks[index + 1].Type == type)
            {
                index++;
            }
            else
            {
                break;
            }
        }

        return list;
    }

    private static Paragraph CreateQuote(ContentBlock block)
    {
        var paragraph = new Paragraph
        {
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0xD9, 0xDE, 0xD6)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 2, 2),
            Margin = new Thickness(8, 4, 0, 8),
            Foreground = ForegroundFor(block.Color),
            FontStyle = FontStyles.Italic
        };
        AppendText(paragraph, block.Text, block.Bold);
        return paragraph;
    }

    private static Paragraph CreateCode(ContentBlock block)
    {
        var paragraph = new Paragraph
        {
            Background = new SolidColorBrush(MediaColor.FromRgb(0xF1, 0xF1, 0xEF)),
            Padding = new Thickness(8),
            FontFamily = new MediaFontFamily("Consolas"),
            Margin = new Thickness(0, 4, 0, 8)
        };
        AppendText(paragraph, block.Text, block.Bold);
        return paragraph;
    }

    private static Table CreateTable(ContentBlock block)
    {
        var rows = block.Rows
            ?? throw new InvalidOperationException("A table preview requires validated rows.");
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 8) };
        var columnCount = rows[0].Count;
        for (var index = 0; index < columnCount; index++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new TableRow();
            foreach (var value in rows[rowIndex])
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(2),
                    FontWeight = rowIndex == 0 ? FontWeights.SemiBold : FontWeights.Normal
                };
                AppendText(paragraph, value, bold: rowIndex == 0);
                row.Cells.Add(new TableCell(paragraph)
                {
                    BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0xD9, 0xDE, 0xD6)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(6)
                });
            }

            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static void AppendText(Paragraph paragraph, string text, bool bold)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            var run = new Run(lines[index]) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
            paragraph.Inlines.Add(run);
        }
    }

    private static MediaBrush ForegroundFor(string color) => color switch
    {
        "blue" => new SolidColorBrush(MediaColor.FromRgb(0x33, 0x7E, 0xA9)),
        "purple" => new SolidColorBrush(MediaColor.FromRgb(0x90, 0x65, 0xB0)),
        "orange" => new SolidColorBrush(MediaColor.FromRgb(0xD9, 0x73, 0x0D)),
        "red" => new SolidColorBrush(MediaColor.FromRgb(0xD4, 0x4C, 0x47)),
        _ => MediaBrushes.Black
    };
}
