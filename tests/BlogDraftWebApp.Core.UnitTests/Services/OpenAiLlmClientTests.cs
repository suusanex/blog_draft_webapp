using System.Net;
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
}
