using System.Text;

namespace NotionHelper.Models;

public enum ImprovementMode
{
    Proofread,
    StructureWhenUseful
}

public sealed class ImprovementResult
{
    public List<ContentBlock> Blocks { get; init; } = [];

    public string ToPlainText()
    {
        var output = new StringBuilder();
        foreach (var block in Blocks)
        {
            if (output.Length > 0)
            {
                output.AppendLine();
                output.AppendLine();
            }

            if (block.Type == "table" && block.Rows is { Count: > 0 })
            {
                for (var rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
                {
                    if (rowIndex > 0)
                    {
                        output.AppendLine();
                    }

                    output.Append(string.Join("\t", block.Rows[rowIndex]));
                }
            }
            else
            {
                output.Append(block.Text);
            }
        }

        return output.ToString();
    }
}

public sealed class ContentBlock
{
    public string Type { get; init; } = "paragraph";
    public string Text { get; init; } = string.Empty;
    public string Color { get; init; } = "default";
    public bool Bold { get; init; }
    public List<List<string>>? Rows { get; init; }
}
