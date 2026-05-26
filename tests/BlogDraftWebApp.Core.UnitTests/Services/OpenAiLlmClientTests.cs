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
    public async Task GenerateAsync_StructuredOutput設定時_ResponseFormatを送信する()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            const string json = "{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}";
            return StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        });
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-test",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-test",
            StructuredOutputsEnabled = true,
        });
        var client = new OpenAiLlmClient(new HttpClient(handler), NullLogger<OpenAiLlmClient>.Instance, options);

        await client.GenerateAsync(new Prompt
        {
            SystemMessage = "sys",
            UserOverview = "user",
            StructuredOutput = EditorialPlanJsonContract.For(PlanGenerationMode.Full),
        }, CancellationToken.None);

        using var body = JsonDocument.Parse(requestBody!);
        var format = body.RootElement.GetProperty("response_format");
        Assert.That(format.GetProperty("type").GetString(), Is.EqualTo("json_schema"));
        Assert.That(format.GetProperty("json_schema").GetProperty("name").GetString(), Is.EqualTo("editorial_plan"));
        Assert.That(format.GetProperty("json_schema").GetProperty("strict").GetBoolean(), Is.True);

        var schema = format.GetProperty("json_schema").GetProperty("schema").GetRawText();
        Assert.That(schema, Does.Not.Contain("minLength"));
        Assert.That(schema, Does.Not.Contain("minItems"));
        Assert.That(schema, Does.Not.Contain("uniqueItems"));
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
            Model = "gpt-4.1-test",
            MaxTokens = 4096,
            RequestTimeoutSeconds = 30,
        });

        var client = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, options);
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "user" };

        _ = await client.GenerateAsync(prompt, CancellationToken.None, maxOutputTokens: 350);

        Assert.That(actualMaxTokens, Is.EqualTo(350));
    }

    [Test]
    public async Task GenerateAsync_アウトライン生成時_温度を低めに設定する()
    {
        double? actualTemperature = null;

        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("temperature", out var temperatureProp))
            {
                actualTemperature = temperatureProp.GetDouble();
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
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "## アウトライン生成（厳格フォーマット）\n- rule" };

        _ = await client.GenerateAsync(prompt, CancellationToken.None, maxOutputTokens: 350);

        Assert.That(actualTemperature, Is.EqualTo(0.2).Within(0.0001));
    }

    [Test]
    public async Task GenerateAsync_gpt5系モデルでは温度を送信しない()
    {
        var hasTemperature = false;

        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            hasTemperature = json.RootElement.TryGetProperty("temperature", out JsonElement _);

            const string response = "{\"choices\":[{\"message\":{\"content\":\"- a\\n- b\\n- c\\n- d\\n- e\"}}]}";
            return StubHttpMessageHandler.Json(HttpStatusCode.OK, response);
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-test",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-5.5",
            MaxTokens = 4096,
            RequestTimeoutSeconds = 30,
        });

        var client = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, options);
        var prompt = new Prompt { SystemMessage = "sys", UserOverview = "## アウトライン生成（厳格フォーマット）\n- rule" };

        _ = await client.GenerateAsync(prompt, CancellationToken.None, maxOutputTokens: 350);

        Assert.That(hasTemperature, Is.False);
    }
}
