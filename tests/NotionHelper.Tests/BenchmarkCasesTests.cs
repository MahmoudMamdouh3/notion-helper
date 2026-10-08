using ModelBenchmark;
using NotionHelper.Models;

namespace NotionHelper.Tests;

public sealed class BenchmarkCasesTests
{
    [Fact]
    public void All_CoversTheIntendedSyntheticWritingBehaviors()
    {
        Assert.Equal(8, BenchmarkCases.All.Count);
        Assert.Equal(8, BenchmarkCases.All.Select(benchmarkCase => benchmarkCase.Name).Distinct().Count());
        Assert.Equal(2, BenchmarkCases.All.Count(benchmarkCase => benchmarkCase.Category == "proofread"));
        Assert.Equal(6, BenchmarkCases.All.Count(benchmarkCase => benchmarkCase.Category == "structure"));
        Assert.Contains(BenchmarkCases.All, benchmarkCase => benchmarkCase.Name == "structure-consistent-data-table");
        Assert.Contains(BenchmarkCases.All, benchmarkCase => benchmarkCase.Name == "structure-explicit-quotation");
        Assert.Contains(BenchmarkCases.All, benchmarkCase => benchmarkCase.Name == "structure-actual-shell-commands");
    }

    [Fact]
    public void Evaluators_PassExpectedSyntheticContracts()
    {
        var proofreading = Find("proofreading-common-errors");
        Assert.Empty(proofreading.Evaluate(Result(
            new ContentBlock { Text = "I received the package yesterday." })));

        var steps = Find("structure-actionable-steps");
        Assert.Empty(steps.Evaluate(Result(
            new ContentBlock { Type = "numbered", Text = "Run the test suite." },
            new ContentBlock { Type = "numbered", Text = "Build the installer." },
            new ContentBlock { Type = "numbered", Text = "Publish the package." })));

        var table = Find("structure-consistent-data-table");
        Assert.Empty(table.Evaluate(Result(new ContentBlock
        {
            Type = "table",
            Text = "Support queue",
            Rows =
            [
                ["Owner", "Open requests"],
                ["Ana", "4"],
                ["Bo", "2"],
                ["Cy", "5"]
            ]
        })));

        var quote = Find("structure-explicit-quotation");
        Assert.Empty(quote.Evaluate(Result(
            new ContentBlock { Type = "quote", Text = "Ship only after the checks pass." })));

        var commands = Find("structure-actual-shell-commands");
        Assert.Empty(commands.Evaluate(Result(
            new ContentBlock
            {
                Type = "code",
                Text = "dotnet test .\\tests\\NotionHelper.Tests\\NotionHelper.Tests.csproj -c Release\n" +
                    "dotnet build .\\src\\NotionHelper\\NotionHelper.csproj -c Release"
            })));
    }

    [Fact]
    public void Evaluators_ReportMissingRequirementsAndUnrequestedProofreadingFormatting()
    {
        var proofreading = Find("proofreading-factual-anchors");
        var issues = proofreading.Evaluate(Result(
            new ContentBlock { Type = "heading", Text = "14 March" },
            new ContentBlock { Text = "Lina send two file." }));

        Assert.Contains(issues, issue => issue.Contains("proofread-only mode added formatting", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("missing content: Omar", StringComparison.Ordinal));

        Assert.Empty(proofreading.Evaluate(Result(
            new ContentBlock { Text = "On March 14, Lina sent 3 files to Omar." })));

        var ordinaryProse = Find("ordinary-prose-no-decoration");
        Assert.NotEmpty(ordinaryProse.Evaluate(Result(
            new ContentBlock { Type = "quote", Text = "The new keyboard arrived today." })));
    }

    private static BenchmarkCase Find(string name) =>
        Assert.Single(BenchmarkCases.All, benchmarkCase => benchmarkCase.Name == name);

    private static ImprovementResult Result(params ContentBlock[] blocks) => new() { Blocks = [.. blocks] };
}
