using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using NotionHelper.Models;
using NotionHelper.Presentation;

namespace NotionHelper.Tests;

public sealed class PreviewDocumentBuilderTests
{
    [Fact]
    public void Create_RendersAllSupportedBlocksWithoutInterpretingTextAsMarkup()
    {
        RunOnSta(() =>
        {
            var result = new ImprovementResult
            {
                Blocks =
                [
                    new ContentBlock { Type = "paragraph", Text = "<script>plain text</script>", Color = "blue", Bold = true },
                    new ContentBlock { Type = "heading", Text = "Section" },
                    new ContentBlock { Type = "bullet", Text = "First item" },
                    new ContentBlock { Type = "bullet", Text = "Second item" },
                    new ContentBlock { Type = "numbered", Text = "Step" },
                    new ContentBlock { Type = "quote", Text = "Quoted text", Color = "purple" },
                    new ContentBlock { Type = "code", Text = "echo safe" },
                    new ContentBlock { Type = "table", Text = "Data", Rows = [["Name", "Role"], ["Ada", "Engineer"]] }
                ]
            };

            var document = PreviewDocumentBuilder.Create(result);

            Assert.Equal(7, document.Blocks.Count);
            var paragraph = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
            Assert.Equal("<script>plain text</script>", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n'));
            Assert.Equal(Color.FromRgb(0x33, 0x7E, 0xA9), ((SolidColorBrush)paragraph.Foreground).Color);
            Assert.True(Assert.IsType<Run>(paragraph.Inlines.FirstInline).FontWeight == FontWeights.Bold);

            var blocks = document.Blocks.ToArray();
            var heading = Assert.IsType<Paragraph>(blocks[1]);
            Assert.Equal(20, heading.FontSize);
            var bulletList = Assert.IsType<List>(blocks[2]);
            Assert.Collection(
                bulletList.ListItems,
                item => Assert.IsType<Paragraph>(item.Blocks.FirstBlock),
                item => Assert.IsType<Paragraph>(item.Blocks.FirstBlock));
            Assert.Single(Assert.IsType<List>(blocks[3]).ListItems);
            Assert.Equal(FontStyles.Italic, Assert.IsType<Paragraph>(blocks[4]).FontStyle);
            Assert.Equal("Consolas", Assert.IsType<Paragraph>(blocks[5]).FontFamily.Source);
            Assert.IsType<Table>(blocks[6]);
        });
    }

    [Fact]
    public void Create_RendersRectangularTableWithHeaderEmphasis()
    {
        RunOnSta(() =>
        {
            var result = new ImprovementResult
            {
                Blocks =
                [
                    new ContentBlock
                    {
                        Type = "table",
                        Text = "Table",
                        Rows = [["Name", "Role"], ["Ada", "Engineer"]]
                    }
                ]
            };

            var table = Assert.IsType<Table>(PreviewDocumentBuilder.Create(result).Blocks.FirstBlock);

            Assert.Equal(2, table.Columns.Count);
            Assert.Equal(2, table.RowGroups[0].Rows.Count);
            Assert.Equal(FontWeights.SemiBold, table.RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock.FontWeight);
        });
    }

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
