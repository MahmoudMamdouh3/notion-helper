using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NotionHelper.Interop;
using NotionHelper.Models;
using NotionHelper.Services;

namespace NotionHelper.Tests;

public sealed class HtmlClipboardFormatterTests
{
    [Fact]
    public void ToClipboardHtml_UsesUtf8ByteOffsetsForUnicodeFragment()
    {
        var result = new ImprovementResult
        {
            Blocks =
            [
                new ContentBlock { Type = "paragraph", Text = "Zażółć gęślą jaźń — こんにちは 🌻", Color = "default" }
            ]
        };

        var payload = Encoding.UTF8.GetBytes(HtmlClipboardFormatter.ToClipboardHtml(result));
        var startHtml = ReadOffset(payload, "StartHTML");
        var endHtml = ReadOffset(payload, "EndHTML");
        var startFragment = ReadOffset(payload, "StartFragment");
        var endFragment = ReadOffset(payload, "EndFragment");
        var completeHtml = Encoding.UTF8.GetString(payload, startHtml, endHtml - startHtml);
        var fragment = Encoding.UTF8.GetString(payload, startFragment, endFragment - startFragment);

        Assert.StartsWith("<html><body><!--StartFragment-->", completeHtml, StringComparison.Ordinal);
        Assert.Contains("こんにちは", fragment, StringComparison.Ordinal);
        Assert.EndsWith("</p>", fragment, StringComparison.Ordinal);
        Assert.True(startHtml < startFragment);
        Assert.True(startFragment < endFragment);
        Assert.True(endFragment < endHtml);
    }

    [Fact]
    public void ToClipboardHtml_EncodesUntrustedTextInsteadOfEmittingMarkup()
    {
        var result = new ImprovementResult
        {
            Blocks = [new ContentBlock { Type = "paragraph", Text = "<script>alert('x')</script> & text" }]
        };

        var html = HtmlClipboardFormatter.ToClipboardHtml(result);

        Assert.Contains("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt; &amp; text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToClipboardHtml_GroupsConsecutiveNumberedItemsIntoOneOrderedList()
    {
        var result = new ImprovementResult
        {
            Blocks =
            [
                new ContentBlock { Type = "numbered", Text = "First" },
                new ContentBlock { Type = "numbered", Text = "Second" },
                new ContentBlock { Type = "paragraph", Text = "Afterwards" },
                new ContentBlock { Type = "numbered", Text = "Restarted" }
            ]
        };

        var html = HtmlClipboardFormatter.ToClipboardHtml(result);

        Assert.Equal(2, Count(html, "<ol>"));
        Assert.Contains("<ol><li style=\"\">First</li><li style=\"\">Second</li></ol>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPlainText_RendersTableRowsAsTabSeparatedFallback()
    {
        var result = new ImprovementResult
        {
            Blocks =
            [
                new ContentBlock
                {
                    Type = "table",
                    Rows = [["Name", "Role"], ["Ada", "Engineer"]]
                }
            ]
        };

        Assert.Equal("Name\tRole\r\nAda\tEngineer", result.ToPlainText());
    }

    [Fact]
    public void Validator_RejectsFormattingInProofreadMode()
    {
        var result = new ImprovementResult
        {
            Blocks = [new ContentBlock { Type = "heading", Text = "Unexpected heading" }]
        };

        Assert.Throws<JsonException>(() =>
            ImprovementResultValidator.Validate(result, ImprovementMode.Proofread));
    }

    [Fact]
    public void Validator_RejectsNonRectangularTables()
    {
        var result = new ImprovementResult
        {
            Blocks =
            [
                new ContentBlock
                {
                    Type = "table",
                    Rows = [["Name", "Role"], ["Ada"]]
                }
            ]
        };

        Assert.Throws<JsonException>(() =>
            ImprovementResultValidator.Validate(result, ImprovementMode.StructureWhenUseful));
    }

    [Fact]
    public void Validator_RejectsNullBlockCollectionsFromModelJson()
    {
        var result = JsonSerializer.Deserialize<ImprovementResult>("{\"blocks\":null}")!;

        Assert.Throws<JsonException>(() =>
            ImprovementResultValidator.Validate(result, ImprovementMode.Proofread));
    }

    [Fact]
    public void Validator_RejectsTableRowsOnNonTableBlocks()
    {
        var result = new ImprovementResult
        {
            Blocks =
            [
                new ContentBlock
                {
                    Type = "paragraph",
                    Text = "A paragraph.",
                    Rows = [["unexpected"]]
                }
            ]
        };

        Assert.Throws<JsonException>(() =>
            ImprovementResultValidator.Validate(result, ImprovementMode.StructureWhenUseful));
    }

    [Fact]
    public void KeyboardInputStruct_MatchesWindowsInputAbi()
    {
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expectedSize, System.Runtime.InteropServices.Marshal.SizeOf<Input>());
    }

    private static int ReadOffset(byte[] payload, string name)
    {
        var prefixLength = Math.Min(payload.Length, 256);
        var prefix = Encoding.UTF8.GetString(payload, 0, prefixLength);
        var startHtmlMatch = Regex.Match(prefix, @"^StartHTML:(\d+)\r?$", RegexOptions.Multiline);
        Assert.True(startHtmlMatch.Success, "Missing StartHTML offset in clipboard header.");
        var headerLength = int.Parse(startHtmlMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var header = Encoding.UTF8.GetString(payload, 0, headerLength);
        var match = Regex.Match(header, $@"^{name}:(\d+)\r?$", RegexOptions.Multiline);
        Assert.True(match.Success, $"Missing {name} offset in clipboard header.");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int Count(string value, string token) =>
        Regex.Matches(value, Regex.Escape(token)).Count;
}
