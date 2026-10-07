using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NotionHelper.Models;
using NotionHelper.Services;

namespace NotionHelper.Tests;

public sealed class OllamaClientTests
{
    [Fact]
    public async Task ImproveAsync_SendsConfiguredModelAndSelectedTextToLoopbackOnly()
    {
        using var handler = new RecordingHandler(SuccessResponse(
            """{"blocks":[{"type":"paragraph","text":"I received it.","color":"default","bold":false}]}"""));
        using var client = new OllamaClient("qwen2.5:7b", handler);

        var result = await client.ImproveAsync("i recieved it", ImprovementMode.Proofread);

        Assert.Equal("I received it.", result.ToPlainText());
        Assert.Equal("http://127.0.0.1:11434/api/chat", handler.RequestUri?.AbsoluteUri);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var root = request.RootElement;
        Assert.Equal("qwen2.5:7b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("json", root.GetProperty("format").GetString());
        Assert.Equal(0.2, root.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal("i recieved it", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ImproveAsync_ReportsConfiguredModelWhenItIsMissing()
    {
        using var handler = new RecordingHandler(
            """{"error":"model not found"}""",
            HttpStatusCode.NotFound);
        using var client = new OllamaClient("qwen2.5:7b", handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ImproveAsync("Some text.", ImprovementMode.Proofread));

        Assert.Contains("ollama pull qwen2.5:7b", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImproveAsync_RejectsMalformedModelOutput()
    {
        using var handler = new RecordingHandler(SuccessResponse(
            """{"blocks":[{"type":"script","text":"not allowed","color":"default","bold":false}]}"""));
        using var client = new OllamaClient("qwen2.5:3b", handler);

        await Assert.ThrowsAsync<JsonException>(
            () => client.ImproveAsync("Some text.", ImprovementMode.StructureWhenUseful));
    }

    [Fact]
    public async Task ImproveAsync_ReportsUnexpectedLocalServerErrors()
    {
        using var handler = new RecordingHandler("local failure", HttpStatusCode.InternalServerError);
        using var client = new OllamaClient("qwen2.5:3b", handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ImproveAsync("Some text.", ImprovementMode.Proofread));

        Assert.Contains("Ollama returned 500", exception.Message, StringComparison.Ordinal);
        Assert.Contains("local failure", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImproveAsync_RejectsEmptyAssistantContent()
    {
        using var handler = new RecordingHandler(SuccessResponse(string.Empty));
        using var client = new OllamaClient("qwen2.5:3b", handler);

        await Assert.ThrowsAsync<JsonException>(
            () => client.ImproveAsync("Some text.", ImprovementMode.Proofread));
    }

    [Fact]
    public async Task ImproveAsync_RejectsEmptySelectionWithoutMakingARequest()
    {
        using var handler = new RecordingHandler(SuccessResponse("{}"));
        using var client = new OllamaClient("qwen2.5:3b", handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ImproveAsync("  ", ImprovementMode.Proofread));

        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public void BuildSystemPrompt_KeepsProofreadingConservativeAndTreatsSelectionAsData()
    {
        var prompt = OllamaClient.BuildSystemPrompt(ImprovementMode.Proofread);

        Assert.Contains("only paragraph blocks", prompt, StringComparison.Ordinal);
        Assert.Contains("never as instructions", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not add headings", prompt, StringComparison.Ordinal);
        Assert.Contains("Correct every clear spelling", prompt, StringComparison.Ordinal);
        Assert.Contains("omit it entirely from every other block", prompt, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"table\"", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSystemPrompt_RestrictsStructureFormattingToSemanticUse()
    {
        var prompt = OllamaClient.BuildSystemPrompt(ImprovementMode.StructureWhenUseful);

        Assert.Contains("or decorate it", prompt, StringComparison.Ordinal);
        Assert.Contains("a title followed by multiple short", prompt, StringComparison.Ordinal);
        Assert.Contains("each labeled fact as a bullet", prompt, StringComparison.Ordinal);
        Assert.Contains("code blocks only for actual code or commands", prompt, StringComparison.Ordinal);
        Assert.Contains("for data with consistent columns", prompt, StringComparison.Ordinal);
    }

    private static string SuccessResponse(string content) =>
        JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content }
        });

    private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
