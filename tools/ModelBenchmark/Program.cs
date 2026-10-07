using System.Diagnostics;
using NotionHelper.Models;
using NotionHelper.Services;

var models = args.Length == 0
    ? [AppSettings.DefaultModel]
    : args;

var cases = new[]
{
    new BenchmarkCase(
        "proofreading",
        "i recieved the pakage yesterdya.",
        ImprovementMode.Proofread,
        result =>
        {
            var text = result.ToPlainText();
            var issues = result.Blocks
                .Where(block => block.Type != "paragraph" || block.Color != "default" || block.Bold)
                .Select(block => "unexpected formatting");
            var missing = new[] { "received", "package", "yesterday" }
                .Where(term => !text.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(term => $"missing correction: {term}");
            return string.Join(", ", issues.Concat(missing));
        }),
    new BenchmarkCase(
        "useful-structure",
        "Weekly project update\nowner: Maya Chen\nstatus: blocked\nnext step: request security review by Friday",
        ImprovementMode.StructureWhenUseful,
        result =>
        {
            var text = result.ToPlainText();
            var issues = new[] { "Maya Chen", "blocked", "security review", "Friday" }
                .Where(term => !text.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(term => $"missing content: {term}")
                .ToList();
            if (!result.Blocks.Any(block => block.Type is "heading" or "bullet" or "numbered" or "table"))
            {
                issues.Add("no semantic structure");
            }

            return string.Join(", ", issues);
        }),
    new BenchmarkCase(
        "ordinary-prose-no-decoration",
        "The new keyboard arrived today. It is quiet and comfortable to use.",
        ImprovementMode.StructureWhenUseful,
        result =>
        {
            var issues = result.Blocks
                .Where(block => block.Type != "paragraph" || block.Color != "default" || block.Bold)
                .Select(block => $"unneeded {block.Type}/{block.Color} formatting");
            return string.Join(", ", issues);
        })
};

var failedRequests = 0;
Console.WriteLine("Local Ollama quality check — synthetic text only; results are not saved.");
Console.WriteLine("This tool sends requests only to 127.0.0.1 through the app's fixed Ollama client.");

foreach (var model in models)
{
    using var client = new OllamaClient(model);
    var elapsedTotal = TimeSpan.Zero;
    var passed = 0;
    Console.WriteLine($"\nModel: {model}");

    foreach (var benchmarkCase in cases)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await client.ImproveAsync(benchmarkCase.Input, benchmarkCase.Mode);
            stopwatch.Stop();
            elapsedTotal += stopwatch.Elapsed;
            var issue = benchmarkCase.Evaluate(result);
            var success = issue.Length == 0;
            if (success)
            {
                passed++;
            }

            Console.WriteLine($"  {(success ? "PASS" : "CHECK")} {benchmarkCase.Name,-30} {stopwatch.Elapsed.TotalMilliseconds,8:F0} ms" +
                (success ? string.Empty : $" — {issue}"));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            stopwatch.Stop();
            elapsedTotal += stopwatch.Elapsed;
            failedRequests++;
            Console.WriteLine($"  ERROR {benchmarkCase.Name,-29} {exception.Message}");
        }
    }

    var averageMs = elapsedTotal.TotalMilliseconds / cases.Length;
    Console.WriteLine($"  Quality checks: {passed}/{cases.Length}; mean response time: {averageMs:F0} ms");
}

return failedRequests == 0 ? 0 : 1;

internal sealed record BenchmarkCase(
    string Name,
    string Input,
    ImprovementMode Mode,
    Func<ImprovementResult, string> Evaluate);
