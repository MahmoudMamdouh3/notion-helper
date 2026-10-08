using System.Diagnostics;
using ModelBenchmark;
using NotionHelper.Models;
using NotionHelper.Services;

var models = args.Length == 0
    ? [AppSettings.DefaultModel]
    : args;

var cases = BenchmarkCases.All;
var failedRequests = 0;
Console.WriteLine($"Local Ollama quality check — {cases.Count} synthetic cases; results are not saved.");
Console.WriteLine("This tool sends requests only to 127.0.0.1 through the app's fixed Ollama client.");
Console.WriteLine("Checks cover proofreading, fact retention, semantic structure, and restraint; they are not a correctness guarantee.");

foreach (var model in models)
{
    using var client = new OllamaClient(model);
    var elapsedTotal = TimeSpan.Zero;
    var passed = 0;
    var categoryTotals = cases
        .GroupBy(benchmarkCase => benchmarkCase.Category)
        .ToDictionary(group => group.Key, group => group.Count());
    var categoryPassed = categoryTotals.Keys.ToDictionary(category => category, _ => 0);
    Console.WriteLine($"\nModel: {model}");

    foreach (var benchmarkCase in cases)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await client.ImproveAsync(benchmarkCase.Input, benchmarkCase.Mode);
            stopwatch.Stop();
            elapsedTotal += stopwatch.Elapsed;
            var issues = benchmarkCase.Evaluate(result);
            var success = issues.Count == 0;
            if (success)
            {
                passed++;
                categoryPassed[benchmarkCase.Category]++;
            }

            Console.WriteLine($"  {(success ? "PASS" : "CHECK")} {benchmarkCase.Name,-30} {stopwatch.Elapsed.TotalMilliseconds,8:F0} ms" +
                (success ? string.Empty : $" — {string.Join(", ", issues)}"));
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

    var averageMs = elapsedTotal.TotalMilliseconds / cases.Count;
    foreach (var (category, totals) in categoryTotals)
    {
        Console.WriteLine($"  {category}: {categoryPassed[category]}/{totals}");
    }

    Console.WriteLine($"  Quality checks: {passed}/{cases.Count}; mean response time: {averageMs:F0} ms");
}

return failedRequests == 0 ? 0 : 1;
