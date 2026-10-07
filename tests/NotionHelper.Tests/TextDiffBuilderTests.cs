using System.Windows.Documents;
using System.Windows;
using NotionHelper.Presentation;

namespace NotionHelper.Tests;

public sealed class TextDiffBuilderTests
{
    [Fact]
    public void Compare_SeparatesWordAndPunctuationChangesAndPreservesText()
    {
        var diff = TextDiffBuilder.Compare("teh cat.", "the cat!");

        Assert.False(diff.IsCoarse);
        Assert.Collection(
            diff.Segments,
            segment => Assert.Equal((TextDiffKind.Removed, "teh"), (segment.Kind, segment.Text)),
            segment => Assert.Equal((TextDiffKind.Added, "the"), (segment.Kind, segment.Text)),
            segment => Assert.Equal((TextDiffKind.Unchanged, " cat"), (segment.Kind, segment.Text)),
            segment => Assert.Equal((TextDiffKind.Removed, "."), (segment.Kind, segment.Text)),
            segment => Assert.Equal((TextDiffKind.Added, "!"), (segment.Kind, segment.Text)));
        Assert.Equal("teh cat.", string.Concat(diff.Segments
            .Where(segment => segment.Kind != TextDiffKind.Added)
            .Select(segment => segment.Text)));
        Assert.Equal("the cat!", string.Concat(diff.Segments
            .Where(segment => segment.Kind != TextDiffKind.Removed)
            .Select(segment => segment.Text)));
    }

    [Fact]
    public void Compare_HandlesUnicodeTextElementsWithoutSplittingSurrogatePairs()
    {
        var diff = TextDiffBuilder.Compare("café 😀", "café 🙂");

        Assert.Equal(
            [
                new TextDiffSegment(TextDiffKind.Unchanged, "café "),
                new TextDiffSegment(TextDiffKind.Removed, "😀"),
                new TextDiffSegment(TextDiffKind.Added, "🙂")
            ],
            diff.Segments);
    }

    [Fact]
    public void Compare_UsesCompleteReplacementWhenDetailedDiffWouldExceedWorkBudget()
    {
        var original = string.Join(' ', Enumerable.Range(0, 1_100).Select(index => $"old{index}"));
        var proposed = string.Join(' ', Enumerable.Range(0, 1_100).Select(index => $"new{index}"));

        var diff = TextDiffBuilder.Compare(original, proposed);

        Assert.True(diff.IsCoarse);
        Assert.Equal(
            [
                new TextDiffSegment(TextDiffKind.Removed, original),
                new TextDiffSegment(TextDiffKind.Added, proposed)
            ],
            diff.Segments);
    }

    [Fact]
    public void CreateDocument_ShowsOnlyChangesForItsSide()
    {
        RunOnSta(() =>
        {
            var diff = TextDiffBuilder.Compare("old word", "new word");

            var original = TextDiffBuilder.CreateDocument(diff, proposed: false);
            var proposed = TextDiffBuilder.CreateDocument(diff, proposed: true);
            var originalText = new TextRange(original.ContentStart, original.ContentEnd).Text.TrimEnd('\r', '\n');
            var proposedText = new TextRange(proposed.ContentStart, proposed.ContentEnd).Text.TrimEnd('\r', '\n');

            Assert.Equal("old word", originalText);
            Assert.Equal("new word", proposedText);
            Assert.Contains(
                original.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>(),
                run => run.Text == "old" &&
                    run.TextDecorations?.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough) == true);
            Assert.Contains(
                proposed.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>(),
                run => run.Text == "new" && run.FontWeight == System.Windows.FontWeights.SemiBold);
        });
    }

    [Fact]
    public void CreateDocument_PreservesLineBreaksInBothComparisonPanes()
    {
        RunOnSta(() =>
        {
            var diff = TextDiffBuilder.Compare("First line\nsecond", "First line\nthird");
            var original = TextDiffBuilder.CreateDocument(diff, proposed: false);
            var proposed = TextDiffBuilder.CreateDocument(diff, proposed: true);
            var originalText = NormalizeLineEndings(new TextRange(original.ContentStart, original.ContentEnd).Text).TrimEnd('\n');
            var proposedText = NormalizeLineEndings(new TextRange(proposed.ContentStart, proposed.ContentEnd).Text).TrimEnd('\n');

            Assert.Equal("First line\nsecond", originalText);
            Assert.Equal("First line\nthird", proposedText);
        });
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }
}
