using System.Net;
using System.Net.Http.Json;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Moq;

namespace BlogDraftWebApp.Api.IntegrationTests;

public sealed class DraftEndpointsTests
{
    [Test]
    public async Task DraftPreview_DoesNotCallLlm_AndPromptMatchesComposerOutput()
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

        // Preview must not call the LLM at all.
        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("LLM should not be called in preview."));

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft/preview", new PreviewPromptRequest { Overview = overview });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<PreviewPromptResponse>();
        Assert.That(payload, Is.Not.Null);

        var expectedPrompt = await new PromptComposer().ComposeAsync(
            new BlogOverview(overview),
            chunks,
            factory.StyleCard,
            CancellationToken.None);

        Assert.That(payload!.Prompt, Is.EqualTo(expectedPrompt.FullPrompt));
        Assert.That(payload.RagHitCount, Is.EqualTo(1));
        Assert.That(payload.Warning, Is.Null);

        factory.LlmClientMock.Verify(
            x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
