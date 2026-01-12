using System.Net;
using System.Net.Http.Json;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Moq;

namespace BlogDraftWebApp.Api.IntegrationTests;

public sealed class E2ETests
{
    [Test]
    public async Task Draft_EndToEnd_WorksWithMocks()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a test overview (>=10 chars).";
        var chunks = new List<RAGChunk>
        {
            new()
            {
                Text = "chunk1",
                Score = 0.9,
                SourceTitle = "t1",
                SourceUrl = "https://example.com/1",
            },
        };

        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(overview, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(chunks, null));

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft
            {
                Content = "# Title\n\nThis is a generated draft used for integration testing. " +
                          "It must be sufficiently long so the server does not add the short-output warning message. " +
                          "Additional filler text to exceed 100 characters.",
                Model = "test-model",
                GeneratedAt = DateTimeOffset.UtcNow,
                TokensUsed = 123,
            });

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest { Overview = overview });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<GenerateDraftResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Draft, Does.Contain("generated draft"));
        Assert.That(payload.RagHitCount, Is.EqualTo(1));
        Assert.That(payload.Warning, Is.Null);

        factory.LlmClientMock.Verify(
            x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task DraftPreview_EndToEnd_WorksWithMocks_WithoutCallingLlm()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a test overview (>=10 chars).";
        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(overview, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(Array.Empty<RAGChunk>(), null));

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("LLM should not be called in preview."));

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft/preview", new PreviewPromptRequest { Overview = overview });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<PreviewPromptResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Prompt, Does.Contain("[システムプロンプト]"));
        Assert.That(payload.RagHitCount, Is.EqualTo(0));

        factory.LlmClientMock.Verify(
            x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Health_ReturnsOk_EvenWhenMisconfigured()
    {
        await using var factory = new TestWebApplicationFactory(includeValidConfiguration: false);
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Draft_ReturnsConfigError_WhenMisconfigured()
    {
        await using var factory = new TestWebApplicationFactory(includeValidConfiguration: false);
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest
        {
            Overview = "This is a test overview (>=10 chars).",
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("CONFIG_ERROR"));
        Assert.That(payload.IsRetryable, Is.False);
    }
}
