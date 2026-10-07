using System.Net;
using System.Text;
using NotionHelper.Models;

namespace NotionHelper.Services;

public static class HtmlClipboardFormatter
{
    public static string ToClipboardHtml(ImprovementResult result)
    {
        var fragment = new StringBuilder();
        fragment.Append("<html><body><!--StartFragment-->");

        for (var blockIndex = 0; blockIndex < result.Blocks.Count; blockIndex++)
        {
            var block = result.Blocks[blockIndex];
            var colorStyle = ColorStyle(block.Color);
            var text = WebUtility.HtmlEncode(block.Text);
            if (block.Bold)
            {
                text = $"<strong>{text}</strong>";
            }

            switch (block.Type)
            {
                case "paragraph":
                    fragment.Append($"<p style=\"{colorStyle}\">{text}</p>");
                    break;
                case "heading":
                    fragment.Append($"<h2 style=\"{colorStyle}\">{text}</h2>");
                    break;
                case "bullet":
                    AppendList(fragment, result.Blocks, ref blockIndex, "bullet");
                    break;
                case "numbered":
                    AppendList(fragment, result.Blocks, ref blockIndex, "numbered");
                    break;
                case "quote":
                    fragment.Append($"<blockquote style=\"{colorStyle}\">{text}</blockquote>");
                    break;
                case "code":
                    fragment.Append($"<pre style=\"background-color:#F1F1EF;padding:8px\"><code>{text}</code></pre>");
                    break;
                case "table":
                    AppendTable(fragment, block.Rows);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported content block type: {block.Type}");
            }
        }

        fragment.Append("<!--EndFragment--></body></html>");
        return AddClipboardHeader(fragment.ToString());
    }

    private static void AppendList(StringBuilder html, IReadOnlyList<ContentBlock> blocks, ref int index, string type)
    {
        var tag = type == "bullet" ? "ul" : "ol";
        html.Append('<').Append(tag).Append('>');
        while (index < blocks.Count && blocks[index].Type == type)
        {
            var block = blocks[index];
            var text = WebUtility.HtmlEncode(block.Text);
            if (block.Bold)
            {
                text = $"<strong>{text}</strong>";
            }

            html.Append("<li style=\"").Append(ColorStyle(block.Color)).Append("\">")
                .Append(text).Append("</li>");
            if (index + 1 < blocks.Count && blocks[index + 1].Type == type)
            {
                index++;
            }
            else
            {
                break;
            }
        }

        html.Append("</").Append(tag).Append('>');
    }

    private static void AppendTable(StringBuilder html, List<List<string>>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return;
        }

        html.Append("<table><tbody>");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            html.Append("<tr>");
            foreach (var cell in rows[rowIndex])
            {
                var tag = rowIndex == 0 ? "th" : "td";
                html.Append('<').Append(tag).Append('>')
                    .Append(WebUtility.HtmlEncode(cell))
                    .Append("</").Append(tag).Append('>');
            }

            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    private static string ColorStyle(string color) => color switch
    {
        "blue" => "color:#337EA9",
        "purple" => "color:#9065B0",
        "orange" => "color:#D9730D",
        "red" => "color:#D44C47",
        _ => string.Empty
    };

    private static string AddClipboardHeader(string html)
    {
        const string placeholder = "0000000000";
        var header = $"Version:1.0\r\nStartHTML:{placeholder}\r\nEndHTML:{placeholder}\r\nStartFragment:{placeholder}\r\nEndFragment:{placeholder}\r\n";
        var startHtml = Encoding.UTF8.GetByteCount(header);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount("<html><body><!--StartFragment-->");
        var endFragment = startHtml + Encoding.UTF8.GetByteCount(html) - Encoding.UTF8.GetByteCount("<!--EndFragment--></body></html>");
        var endHtml = startHtml + Encoding.UTF8.GetByteCount(html);

        header = header
            .Replace($"StartHTML:{placeholder}", $"StartHTML:{startHtml:D10}", StringComparison.Ordinal)
            .Replace($"EndHTML:{placeholder}", $"EndHTML:{endHtml:D10}", StringComparison.Ordinal)
            .Replace($"StartFragment:{placeholder}", $"StartFragment:{startFragment:D10}", StringComparison.Ordinal)
            .Replace($"EndFragment:{placeholder}", $"EndFragment:{endFragment:D10}", StringComparison.Ordinal);

        return header + html;
    }
}
