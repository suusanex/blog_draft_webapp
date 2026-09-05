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

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"# Title\\n\\nThis is a generated draft used for integration testing.\",\"openQuestions\":[]}",
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
        Assert.That(payload.RagHitCount, Is.EqualTo(0));
        Assert.That(payload.Warning, Is.Null);

        factory.LlmClientMock.Verify(
            x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()),
            Times.Once);
        factory.RetrievalServiceMock.Verify(
            x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task DraftPreview_EndToEnd_WorksWithMocks_WithoutCallingLlm()
    {
        await using var factory = new TestWebApplicationFactory();

        var overview = "This is a test overview (>=10 chars).";

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("LLM should not be called in preview."));

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft/preview", new PreviewPromptRequest { Overview = overview });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<PreviewPromptResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Prompt, Does.Contain("[システムプロンプト]"));
        Assert.That(payload.Prompt, Does.Contain("[編集方針: 入力保持・限定補足]"));
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

    [Test]
    public async Task Draft_RemainsAvailable_WhenOnlyRagConfigurationIsIncomplete()
    {
        await using var factory = new TestWebApplicationFactory(invalidRagConfiguration: true);
        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"短い本文\",\"openQuestions\":[]}",
                Model = "test-model",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/draft", new GenerateDraftRequest { Overview = "短い" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
