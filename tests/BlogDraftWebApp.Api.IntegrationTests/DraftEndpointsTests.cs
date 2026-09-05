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
    public async Task DraftPreview_DoesNotCallLlmOrRetrieval_AndPromptMatchesComposerOutput()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a test overview (>=10 chars).";

        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(overview, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagException("関連記事の検索に失敗しました。"));

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
            factory.StyleCard,
            CancellationToken.None);

        Assert.That(payload!.Prompt, Is.EqualTo(expectedPrompt.FullPrompt));
        Assert.That(payload.RagHitCount, Is.EqualTo(0));
        Assert.That(payload.Prompt, Does.Contain("[編集方針: 入力保持・限定補足]"));
        Assert.That(payload.Prompt, Does.Contain(overview));
        Assert.That(payload.Prompt, Does.Not.Contain("過去記事からの関連情報"));
        Assert.That(payload.Warning, Is.Null);

        factory.LlmClientMock.Verify(
            x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()),
            Times.Never);
        factory.RetrievalServiceMock.Verify(
            x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Draft_Succeeds_WithoutCallingRetrieval()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a test overview (>=10 chars).";
        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(overview, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagException("関連記事の検索に失敗しました。"));

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft
            {
                Content = """{"draft":"# Title\n\nBody text for the draft.","openQuestions":["要確認"]}""",
                Model = "test",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest { Overview = overview });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<GenerateDraftResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Draft, Does.Contain("Body text for the draft."));
        Assert.That(payload.OpenQuestions, Does.Contain("要確認"));
        Assert.That(payload.RagHitCount, Is.EqualTo(0));

        factory.RetrievalServiceMock.Verify(
            x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Draft_StillWorks_WithWorkflowEndpointsRegistered()
    {
        await using var factory = new TestWebApplicationFactory();

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"single-shot\",\"openQuestions\":[]}",
                Model = "test",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var sessionResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });
        Assert.That(sessionResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var draftResponse = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest
        {
            Overview = "This is a test overview (>=10 chars).",
        });

        Assert.That(draftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await draftResponse.Content.ReadFromJsonAsync<GenerateDraftResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Draft, Is.EqualTo("single-shot"));
    }

    [Test]
    public async Task Draft_AcceptsShortOverview_AndRejectsMalformedGeneratedJsonAsRetryable()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"短い本文\",\"openQuestions\":[}",
                Model = "test",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest { Overview = "短い" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("LLM_OUTPUT_INVALID"));
        Assert.That(payload.IsRetryable, Is.True);
    }
}
