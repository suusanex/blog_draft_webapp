using System.Net;
using System.Text.Json;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using BlogDraftWebApp.Tests.Common.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class OpenAiLlmClientTests
{
    [Test]
    public async Task GenerateAsync_成功時_Draftを返す()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            const string json = "{\"choices\":[{\"message\":{\"content\":\"# hello\"}}],\"usage\":{\"total_tokens\":123}}";
            return Task.FromResult(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-test",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-test",
            RequestTimeoutSeconds = 30,
        });

        var client = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, options);
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "user" };

        var draft = await client.GenerateAsync(prompt, CancellationToken.None);

        Assert.That(draft.Content, Is.EqualTo("# hello"));
        Assert.That(draft.Model, Is.EqualTo("gpt-test"));
        Assert.That(draft.TokensUsed, Is.EqualTo(123));
    }

    [Test]
    public void GenerateAsync_タイムアウト時_LlmExceptionを投げる()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-test",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-test",
            RequestTimeoutSeconds = 1,
        });

        var client = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, options);
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "user" };

        var ex = Assert.ThrowsAsync<LlmException>(async () => await client.GenerateAsync(prompt, CancellationToken.None));
        Assert.That(ex!.ErrorCode, Is.EqualTo("LLM_TIMEOUT"));
        Assert.That(ex.IsRetryable, Is.True);
    }

    [Test]
    public void GenerateAsync_503時_LlmExceptionでRetryable()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(StubHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}")));

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-test",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-test",
            RequestTimeoutSeconds = 30,
        });

        var client = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, options);
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "user" };

        var ex = Assert.ThrowsAsync<LlmException>(async () => await client.GenerateAsync(prompt, CancellationToken.None));
        Assert.That(ex!.ErrorCode, Is.EqualTo("LLM_ERROR"));
        Assert.That(ex.IsRetryable, Is.True);
    }

    [Test]
    public async Task GenerateAsync_OutlineMaxOutputTokens指定時_リクエストへ反映する()
    {
        int? actualMaxTokens = null;

        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("max_completion_tokens", out var tokenProp)
                && tokenProp.TryGetInt32(out var value))
            {
                actualMaxTokens = value;
            }

            const string response = "{\"choices\":[{\"message\":{\"content\":\"- a\\n- b\\n- c\\n- d\\n- e\"}}]}";
            return StubHttpMessageHandler.Json(HttpStatusCode.OK, response);
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-test",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-test",
            MaxTokens = 4096,
            RequestTimeoutSeconds = 30,
        });

        var client = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, options);
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "user" };

        _ = await client.GenerateAsync(prompt, CancellationToken.None, maxOutputTokens: 350);

        Assert.That(actualMaxTokens, Is.EqualTo(350));
    }
}
