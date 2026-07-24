using System.Net;
using System.Net.Http.Json;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Exceptions;
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

    [Test]
    public async Task DraftPreview_AndDraftGeneration_UseTheSamePrompt()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a central point for the article.";
        var chunks = new List<RAGChunk>
        {
            new()
            {
                Text = "A past article example.",
                Score = 0.9,
                SourceTitle = "example",
                SourceUrl = "https://example.com/article",
            },
        };
        Prompt? generationPrompt = null;

        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(overview, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(chunks, null));
        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .Callback<Prompt, CancellationToken>((prompt, _) => generationPrompt = prompt)
            .ReturnsAsync(new Draft
            {
                Content = new string('x', 101),
                Model = "test-model",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var draftResponse = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest { Overview = overview });
        var previewResponse = await http.PostAsJsonAsync("/draft/preview", new PreviewPromptRequest { Overview = overview });

        Assert.That(draftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(previewResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewPromptResponse>();

        Assert.That(generationPrompt, Is.Not.Null);
        Assert.That(preview, Is.Not.Null);
        Assert.That(preview!.Prompt, Is.EqualTo(generationPrompt!.FullPrompt));
    }

    private static IEnumerable<TestCaseData> InvalidOverviewCases()
    {
        foreach (var endpoint in new[] { "/draft", "/draft/preview" })
        {
            yield return new TestCaseData(endpoint, " ", BlogOverview.RequiredErrorMessage)
                .SetName($"{endpoint}_EmptyOverview_ReturnsUnifiedMessage");
            yield return new TestCaseData(endpoint, new string('x', BlogOverview.MinimumLength - 1), BlogOverview.MinimumLengthErrorMessage)
                .SetName($"{endpoint}_ShortOverview_ReturnsUnifiedMessage");
            yield return new TestCaseData(endpoint, new string('x', BlogOverview.MaximumLength + 1), BlogOverview.MaximumLengthErrorMessage)
                .SetName($"{endpoint}_LongOverview_ReturnsUnifiedMessage");
        }
    }

    [TestCaseSource(nameof(InvalidOverviewCases))]
    public async Task DraftEndpoints_InvalidOverview_ReturnsUnifiedMessage(string endpoint, string overview, string expectedMessage)
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync(endpoint, new { Overview = overview });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public async Task Draft_ReturnsRagError_WhenRetrievalFails()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a test overview (>=10 chars).";
        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(overview, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagException("関連記事の検索に失敗しました。"));

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest { Overview = overview });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("RAG_ERROR"));
        Assert.That(payload.IsRetryable, Is.True);
    }
}
