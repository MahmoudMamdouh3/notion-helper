using NotionHelper.Models;

namespace ModelBenchmark;

internal sealed record BenchmarkCase(
    string Name,
    string Category,
    string Input,
    ImprovementMode Mode,
    Func<ImprovementResult, IReadOnlyList<string>> Evaluate);

internal static class BenchmarkCases
{
    internal static IReadOnlyList<BenchmarkCase> All { get; } =
    [
        new(
            "proofreading-common-errors",
            "proofread",
            "i recieved the pakage yesterdya.",
            ImprovementMode.Proofread,
            result =>
            {
                var issues = CheckProofreadFormatting(result);
                AddMissingAnchors(result.ToPlainText(), issues, "received", "package", "yesterday");
                return issues;
            }),
        new(
            "proofreading-factual-anchors",
            "proofread",
            "On 14 March, Lina send 3 file to Omar.",
            ImprovementMode.Proofread,
            result =>
            {
                var issues = CheckProofreadFormatting(result);
                var text = result.ToPlainText();
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    text,
                    @"\b(?:14\s+March|March\s+14)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                {
                    issues.Add("missing date: 14 March");
                }

                AddMissingAnchors(text, issues, "Lina", "3", "files", "Omar");
                return issues;
            }),
        new(
            "ordinary-prose-no-decoration",
            "structure",
            "The new keyboard arrived today. It is quiet and comfortable to use.",
            ImprovementMode.StructureWhenUseful,
            result => result.Blocks
                .Where(block => block.Type != "paragraph" || block.Color != "default" || block.Bold)
                .Select(block => $"unneeded {block.Type}/{block.Color} formatting")
                .ToArray()),
        new(
            "useful-structure-key-value-update",
            "structure",
            "Weekly project update\nowner: Maya Chen\nstatus: blocked\nnext step: request security review by Friday",
            ImprovementMode.StructureWhenUseful,
            result =>
            {
                var issues = new List<string>();
                AddMissingAnchors(result.ToPlainText(), issues, "Maya Chen", "blocked", "security review", "Friday");
                if (!result.Blocks.Any(block => block.Type is "heading" or "bullet" or "numbered" or "table"))
                {
                    issues.Add("no semantic structure");
                }

                return issues;
            }),
        new(
            "structure-actionable-steps",
            "structure",
            "Release checklist: run the test suite, build the installer, then publish the package.",
            ImprovementMode.StructureWhenUseful,
            result =>
            {
                var issues = new List<string>();
                AddMissingAnchors(result.ToPlainText(), issues, "test suite", "installer", "publish", "package");
                if (!result.Blocks.Any(block => block.Type is "numbered" or "bullet"))
                {
                    issues.Add("action sequence was not represented as a list");
                }

                return issues;
            }),
        new(
            "structure-consistent-data-table",
            "structure",
            "Support queue: Ana owns 4 open requests; Bo owns 2 open requests; Cy owns 5 open requests.",
            ImprovementMode.StructureWhenUseful,
            result =>
            {
                var issues = new List<string>();
                var table = result.Blocks.FirstOrDefault(block => block.Type == "table");
                if (table?.Rows is null)
                {
                    issues.Add("consistent records were not represented as a table");
                }
                else
                {
                    AddMissingAnchors(
                        string.Join(" ", table.Rows.SelectMany(row => row)),
                        issues,
                        "Ana",
                        "4",
                        "Bo",
                        "2",
                        "Cy",
                        "5");
                }

                return issues;
            }),
        new(
            "structure-explicit-quotation",
            "structure",
            "Mina wrote, \"Ship only after the checks pass.\"",
            ImprovementMode.StructureWhenUseful,
            result =>
            {
                var issues = new List<string>();
                var quote = result.Blocks.FirstOrDefault(block => block.Type == "quote");
                if (quote is null)
                {
                    issues.Add("explicit quotation was not represented as a quote");
                }
                else
                {
                    AddMissingAnchors(quote.Text, issues, "Ship only after the checks pass");
                }

                return issues;
            }),
        new(
            "structure-actual-shell-commands",
            "structure",
            "Run these commands from the repository root: dotnet test .\\tests\\NotionHelper.Tests\\NotionHelper.Tests.csproj -c Release; dotnet build .\\src\\NotionHelper\\NotionHelper.csproj -c Release.",
            ImprovementMode.StructureWhenUseful,
            result =>
            {
                var code = result.Blocks.FirstOrDefault(block => block.Type == "code");
                if (code is null)
                {
                    return ["actual shell commands were not represented as a code block"];
                }

                var issues = new List<string>();
                AddMissingAnchors(code.Text, issues, "dotnet test", "NotionHelper.Tests.csproj", "dotnet build", "NotionHelper.csproj");
                return issues;
            })
    ];

    private static List<string> CheckProofreadFormatting(ImprovementResult result)
    {
        var issues = result.Blocks
            .Where(block => block.Type != "paragraph" || block.Color != "default" || block.Bold)
            .Select(_ => "proofread-only mode added formatting")
            .ToList();
        return issues;
    }

    private static void AddMissingAnchors(string text, ICollection<string> issues, params string[] anchors)
    {
        foreach (var anchor in anchors)
        {
            if (!text.Contains(anchor, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"missing content: {anchor}");
            }
        }
    }
}
