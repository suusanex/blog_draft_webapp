using System.Net;
using System.Net.Http.Json;
using BlogDraftWebApp.Api.IntegrationTests.TestFixtures;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BlogDraftWebApp.Api.IntegrationTests;

public sealed class WorkflowEndpointsTests
{
    private const string ValidOutline = "- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論";

    [Test]
    public async Task CreateSession_ReturnsSessionInfo()
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var payload = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CurrentStep, Is.EqualTo("Step1_Outline"));
        Assert.That(payload.SessionId, Is.Not.Empty);
    }

    [Test]
    public async Task GenerateOutline_ReturnsGeneratedContent()
    {
        await using var factory = new TestWebApplicationFactory();

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft { Content = ValidOutline, Model = "test", GeneratedAt = DateTimeOffset.UtcNow });

        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(createPayload, Is.Not.Null);

        var response = await http.PostAsJsonAsync($"/workflow/sessions/{createPayload!.SessionId}/steps/outline/generate", new GenerateStepRequest
        {
            Regenerate = false,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<GenerateStepResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Generated, Is.EqualTo(ValidOutline));
    }

    [Test]
    public async Task GetSession_ReturnsGone_WhenExpired()
    {
        await using var factory = new TestWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
            var session = new WorkflowSession
            {
                SessionId = "expired-session",
                InitialInput = new BlogOverview("0123456789"),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-31),
                LastAccessedAt = DateTimeOffset.UtcNow.AddDays(-31),
                DeleteAt = DateTimeOffset.UtcNow.AddDays(-1),
            };

            await repository.CreateSessionAsync(session, CancellationToken.None);
        }

        using var http = factory.CreateClient();
        var response = await http.GetAsync("/workflow/sessions/expired-session");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("SESSION_EXPIRED"));
    }

    [Test]
    public async Task GenerateDraft_ReturnsBadRequest_WhenOutlineNotConfirmed()
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(createPayload, Is.Not.Null);

        var response = await http.PostAsJsonAsync($"/workflow/sessions/{createPayload!.SessionId}/steps/draft/generate", new GenerateStepRequest
        {
            Regenerate = false,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("INVALID_STEP_TRANSITION"));
    }

    [Test]
    public async Task PreviewOutline_ReturnsPrompt_AndDoesNotCallLlm()
    {
        await using var factory = new TestWebApplicationFactory();

        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(createPayload, Is.Not.Null);

        var response = await http.PostAsync($"/workflow/sessions/{createPayload!.SessionId}/steps/outline/preview", content: null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<PreviewPromptResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Prompt, Does.Contain("アウトラインと編集メモの生成"));
        Assert.That(payload.Prompt, Does.Contain("[編集方針: 入力保持・限定補足]"));
        Assert.That(payload.Prompt, Does.Not.Contain("過去記事からの関連情報"));
        Assert.That(payload.RagHitCount, Is.EqualTo(0));

        factory.LlmClientMock.Verify(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
        factory.RetrievalServiceMock.Verify(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PreviewAndGenerateOutline_PromptAreEquivalent()
    {
        await using var factory = new TestWebApplicationFactory();

        Prompt? executedPrompt = null;
        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Callback<Prompt, CancellationToken, int?>((prompt, _, _) => executedPrompt = prompt)
            .ReturnsAsync(new Draft { Content = ValidOutline, Model = "test", GeneratedAt = DateTimeOffset.UtcNow });

        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(createPayload, Is.Not.Null);

        var previewResponse = await http.PostAsync($"/workflow/sessions/{createPayload!.SessionId}/steps/outline/preview", content: null);
        Assert.That(previewResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewPromptResponse>();
        Assert.That(preview, Is.Not.Null);

        var generateResponse = await http.PostAsJsonAsync($"/workflow/sessions/{createPayload.SessionId}/steps/outline/generate", new GenerateStepRequest
        {
            Regenerate = false,
        });
        Assert.That(generateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(executedPrompt, Is.Not.Null);
        Assert.That(preview!.Prompt, Is.EqualTo(executedPrompt!.FullPrompt));
    }

    [Test]
    public async Task ConcurrentGenerateSameSession_ReturnsSessionBusy409()
    {
        await using var factory = new TestWebApplicationFactory();

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Returns(async () =>
            {
                await Task.Delay(300);
                return new Draft { Content = ValidOutline, Model = "test", GeneratedAt = DateTimeOffset.UtcNow };
            });

        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(createPayload, Is.Not.Null);

        var endpoint = $"/workflow/sessions/{createPayload!.SessionId}/steps/outline/generate";
        var first = http.PostAsJsonAsync(endpoint, new GenerateStepRequest { Regenerate = false });
        await Task.Delay(50);
        var second = http.PostAsJsonAsync(endpoint, new GenerateStepRequest { Regenerate = false });

        var responses = await Task.WhenAll(first, second);
        var statuses = responses.Select(x => x.StatusCode).ToList();
        Assert.That(statuses, Does.Contain(HttpStatusCode.OK));
        Assert.That(statuses, Does.Contain(HttpStatusCode.Conflict));

        var busyResponse = responses.First(x => x.StatusCode == HttpStatusCode.Conflict);
        var payload = await busyResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("SESSION_BUSY"));
    }

    [Test]
    public async Task ErrorResponse_DoesNotContainRequestBodyContent_Smoke()
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        const string sensitive = "SENSITIVE-PAYLOAD-12345";
        var invalidOverview = new string('A', 5001) + sensitive;
        var response = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = invalidOverview,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Not.Contain(sensitive));
    }

    [Test]
    public async Task ListAndDeleteSessions_Works()
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(created, Is.Not.Null);

        var listResponse = await http.GetAsync("/workflow/sessions?page=1&pageSize=20");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listPayload = await listResponse.Content.ReadFromJsonAsync<SessionListResponse>();
        Assert.That(listPayload, Is.Not.Null);
        Assert.That(listPayload!.Sessions.Any(x => x.SessionId == created!.SessionId), Is.True);

        var deleteResponse = await http.DeleteAsync($"/workflow/sessions/{created.SessionId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var listAfterDelete = await http.GetAsync("/workflow/sessions?page=1&pageSize=20");
        var listAfterPayload = await listAfterDelete.Content.ReadFromJsonAsync<SessionListResponse>();
        Assert.That(listAfterPayload, Is.Not.Null);
        Assert.That(listAfterPayload!.Sessions.Any(x => x.SessionId == created.SessionId), Is.False);
    }

    [Test]
    public async Task RagRefresh_ShowsDiff_AndCanConfirm()
    {
        await using var factory = new TestWebApplicationFactory();

        var retrievalQueue = new Queue<RetrievalResult>(new[]
        {
            new RetrievalResult(new List<RAGChunk>
            {
                new()
                {
                    Text = "chunk-a",
                    Score = 0.9,
                    SourceTitle = "a",
                    SourceUrl = "https://example.com/a",
                },
            }, null),
            new RetrievalResult(new List<RAGChunk>
            {
                new()
                {
                    Text = "chunk-b",
                    Score = 0.95,
                    SourceTitle = "b",
                    SourceUrl = "https://example.com/b",
                },
            }, null),
        });

        factory.RetrievalServiceMock
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => retrievalQueue.Dequeue());

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft { Content = ValidOutline, Model = "test", GeneratedAt = DateTimeOffset.UtcNow });

        using var http = factory.CreateClient();
        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(created, Is.Not.Null);

        var generateResponse = await http.PostAsJsonAsync($"/workflow/sessions/{created!.SessionId}/steps/outline/generate", new GenerateStepRequest
        {
            Regenerate = false,
        });
        Assert.That(generateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        factory.RetrievalServiceMock.Verify(
            x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var firstRefresh = await http.PostAsync($"/workflow/sessions/{created.SessionId}/rag/refresh", content: null);
        Assert.That(firstRefresh.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var firstConfirm = await http.PostAsync($"/workflow/sessions/{created.SessionId}/rag/refresh/confirm", content: null);
        Assert.That(firstConfirm.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var refreshResponse = await http.PostAsync($"/workflow/sessions/{created.SessionId}/rag/refresh", content: null);
        Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var refresh = await refreshResponse.Content.ReadFromJsonAsync<RefreshRagSnapshotResponse>();
        Assert.That(refresh, Is.Not.Null);
        Assert.That(refresh!.AddedChunkCount, Is.EqualTo(1));
        Assert.That(refresh.RemovedChunkCount, Is.EqualTo(1));
        Assert.That(refresh.Confirmed, Is.False);

        var confirmResponse = await http.PostAsync($"/workflow/sessions/{created.SessionId}/rag/refresh/confirm", content: null);
        Assert.That(confirmResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<RefreshRagSnapshotResponse>();
        Assert.That(confirmed, Is.Not.Null);
        Assert.That(confirmed!.Confirmed, Is.True);
        Assert.That(confirmed.NewSnapshotId, Is.EqualTo(refresh.NewSnapshotId));
    }

    [Test]
    public async Task GenerateOutline_StripsPreambleAndCodeFence_WhenGeneratedContentIsSalvageable()
    {
        await using var factory = new TestWebApplicationFactory();

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "以下のアウトラインです\n```markdown\n- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論\n```",
                Model = "test",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(created, Is.Not.Null);

        var response = await http.PostAsJsonAsync($"/workflow/sessions/{created!.SessionId}/steps/outline/generate", new GenerateStepRequest
        {
            Regenerate = false,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<GenerateStepResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Generated, Is.EqualTo("- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論"));
    }

    [Test]
    public async Task GenerateOutline_ReturnsRetryableGenerationError_WhenGeneratedFormatInvalid()
    {
        await using var factory = new TestWebApplicationFactory();

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "これはアウトラインではありません。説明文だけです。",
                Model = "test",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(created, Is.Not.Null);

        var response = await http.PostAsJsonAsync($"/workflow/sessions/{created!.SessionId}/steps/outline/generate", new GenerateStepRequest
        {
            Regenerate = false,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ErrorCode, Is.EqualTo("OUTLINE_GENERATION_INVALID"));
        Assert.That(payload.IsRetryable, Is.True);
    }

    [TestCase("# 見出し\n- a\n- b\n- c\n- d")]
    [TestCase("1. 番号\n- a\n- b\n- c\n- d")]
    [TestCase("- a\n  - b\n    - c\n      - d\n- e")]
    public async Task SaveOutline_AllowsInvalidFormatsUntilConfirm(string invalidOutline)
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/workflow/sessions", new CreateSessionRequest
        {
            Overview = "0123456789",
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.That(created, Is.Not.Null);

        var response = await http.PostAsJsonAsync($"/workflow/sessions/{created!.SessionId}/steps/outline/save", new SaveStepRequest
        {
            EditedContent = invalidOutline,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var sessionResponse = await http.GetAsync($"/workflow/sessions/{created.SessionId}");
        Assert.That(sessionResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var session = await sessionResponse.Content.ReadFromJsonAsync<GetSessionResponse>();
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.OutlineEdited, Is.EqualTo(invalidOutline));
    }
}

