using System.Text.Json;
using NotionHelper.Models;

namespace NotionHelper.Services;

internal static class ImprovementResultValidator
{
    private const int MaximumBlocks = 120;

    private static readonly HashSet<string> AllowedTypes =
        ["paragraph", "heading", "bullet", "numbered", "quote", "code", "table"];

    private static readonly HashSet<string> AllowedColors =
        ["default", "blue", "purple", "orange", "red"];

    internal static void Validate(ImprovementResult result, ImprovementMode mode)
    {
        if (result.Blocks is null || result.Blocks.Count is 0 or > MaximumBlocks)
        {
            throw new JsonException("The local model returned an empty or excessively large response.");
        }

        foreach (var block in result.Blocks)
        {
            if (block is null || block.Type is null || block.Color is null ||
                !AllowedTypes.Contains(block.Type) || !AllowedColors.Contains(block.Color))
            {
                throw new JsonException("The local model returned an unsupported block type or color. Please try again.");
            }

            if (mode == ImprovementMode.Proofread &&
                (block.Type != "paragraph" || block.Color != "default" || block.Bold))
            {
                throw new JsonException("The model returned formatting in proofreading-only mode. Please try again.");
            }

            if (block.Type == "table")
            {
                if (block.Rows is null or { Count: < 2 or > 30 } ||
                    block.Rows.Any(row => row is null || row.Count is < 2 or > 10 || row.Any(cell => cell is null)) ||
                    block.Rows.Select(row => row.Count).Distinct().Count() != 1)
                {
                    throw new JsonException("The model returned an invalid table. Tables must be rectangular and contain 2–30 rows and 2–10 columns.");
                }
            }
            else if (block.Rows is not null)
            {
                throw new JsonException("The model returned table rows on a non-table block. Please try again.");
            }
            else if (string.IsNullOrWhiteSpace(block.Text))
            {
                throw new JsonException("The model returned an empty content block. Please try again.");
            }
        }
    }
}
