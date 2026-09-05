using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Services;

public sealed class OpenAiLlmClient : ILlmClient
{
    private const int WorkflowMaxTimeoutSeconds = 600;
    private const int ErrorBodyMaxLength = 2000;

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiLlmClient> _logger;
    private readonly LlmOptions _options;

    public OpenAiLlmClient(HttpClient httpClient, ILogger<OpenAiLlmClient> logger, IOptions<LlmOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null)
    {
        var timeoutSeconds = _options.RequestTimeoutSeconds <= 0
            ? WorkflowMaxTimeoutSeconds
            : Math.Min(_options.RequestTimeoutSeconds, WorkflowMaxTimeoutSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? "https://api.openai.com/v1"
                : _options.BaseUrl.TrimEnd('/');

            var uri = new Uri($"{baseUrl}/chat/completions", UriKind.Absolute);
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var body = new Dictionary<string, object?>
            {
                ["model"] = _options.Model,
                ["max_completion_tokens"] = maxOutputTokens is > 0 ? maxOutputTokens.Value : _options.MaxTokens,
                ["messages"] = new object[]
                {
                    new Dictionary<string, object?> { ["role"] = "system", ["content"] = prompt.SystemMessage },
                    new Dictionary<string, object?> { ["role"] = "user", ["content"] = BuildUserContent(prompt) },
                },
            };

            if (RequiresJsonResponse(prompt))
            {
                body["response_format"] = BuildJsonResponseFormat(prompt);
            }

            if (ShouldUseLowTemperature(prompt) && SupportsCustomTemperature(_options.Model))
            {
                body["temperature"] = 0.2;
            }

            foreach (var (key, value) in _options.Parameters)
            {
                body[key] = value;
            }

            request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var responseBody = await ReadErrorBodyAsync(response, cts.Token);
                _logger.LogError(
                    "LLM authentication failed. StatusCode={StatusCode}, Body={Body}",
                    (int)response.StatusCode,
                    responseBody);
                throw new ConfigurationException("LLM authentication failed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await ReadErrorBodyAsync(response, cts.Token);
                var isRetryable = response.StatusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout
                    or HttpStatusCode.BadGateway;

                _logger.LogError(
                    "LLM request failed. StatusCode={StatusCode}, Retryable={IsRetryable}, Body={Body}",
                    (int)response.StatusCode,
                    isRetryable,
                    responseBody);

                throw new LlmException("LLM_ERROR", "LLM サービスでエラーが発生しました", isRetryable);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var choice = json.RootElement.GetProperty("choices")[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonProperty)
                ? finishReasonProperty.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(finishReason)
                && !string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("LLM output did not terminate normally. FinishReason={FinishReason}", finishReason);
            }

            var content = choice
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            int? tokens = null;
            if (json.RootElement.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("total_tokens", out var totalTokens)
                && totalTokens.TryGetInt32(out var tokenValue))
            {
                tokens = tokenValue;
            }

            return new Draft
            {
                Content = content,
                Model = _options.Model,
                GeneratedAt = DateTimeOffset.UtcNow,
                TokensUsed = tokens,
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "LLM request timed out. TimeoutSeconds={TimeoutSeconds}", timeoutSeconds);
            throw new LlmException("LLM_TIMEOUT", "生成に時間がかかりすぎています。もう一度お試しください", true, ex);
        }
        catch (Exception ex) when (ex is not ConfigurationException and not LlmException)
        {
            _logger.LogError(ex, "LLM call failed.");
            throw new LlmException("LLM_ERROR", "LLM サービスでエラーが発生しました", true, ex);
        }
    }

    private static bool ShouldUseLowTemperature(Prompt prompt)
    {
        return prompt.UserOverview.Contains(PromptComposer.OutlineHeading, StringComparison.Ordinal)
            || prompt.UserOverview.Contains(PromptComposer.OutlineRepairHeading, StringComparison.Ordinal);
    }

    private static bool RequiresJsonResponse(Prompt prompt)
    {
        return prompt.UserOverview.Contains("- JSON のみを返す", StringComparison.Ordinal);
    }

    private static object BuildJsonResponseFormat(Prompt prompt)
    {
        var isOutline = prompt.UserOverview.Contains(PromptComposer.OutlineHeading, StringComparison.Ordinal)
            || prompt.UserOverview.Contains(PromptComposer.OutlineRepairHeading, StringComparison.Ordinal);

        return new Dictionary<string, object?>
        {
            ["type"] = "json_schema",
            ["json_schema"] = new Dictionary<string, object?>
            {
                ["name"] = isOutline ? "blog_outline_response" : "blog_draft_response",
                ["strict"] = true,
                ["schema"] = isOutline ? BuildOutlineSchema() : BuildDraftSchema(),
            },
        };
    }

    private static Dictionary<string, object?> BuildDraftSchema()
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["draft"] = new Dictionary<string, object?> { ["type"] = "string" },
                ["openQuestions"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "string" },
                },
            },
            ["required"] = new[] { "draft", "openQuestions" },
        };
    }

    private static Dictionary<string, object?> BuildOutlineSchema()
    {
        var meaningElementSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["source"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
                ["role"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
            },
            ["required"] = new[] { "source", "role" },
        };
        var sectionScopeSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["heading"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
                ["covers"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "string" },
                },
                ["allowedSupplement"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
                ["doNotAdd"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
            },
            ["required"] = new[] { "heading", "covers", "allowedSupplement", "doNotAdd" },
        };
        var memoSchema = new Dictionary<string, object?>
        {
            ["type"] = new[] { "object", "null" },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["meaningElements"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = meaningElementSchema,
                },
                ["logicalRelations"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "string" },
                },
                ["articleQuestion"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
                ["readerAssumption"] = new Dictionary<string, object?> { ["type"] = new[] { "string", "null" } },
                ["scopeBySection"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = sectionScopeSchema,
                },
                ["openQuestions"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "string" },
                },
            },
            ["required"] = new[]
            {
                "meaningElements",
                "logicalRelations",
                "articleQuestion",
                "readerAssumption",
                "scopeBySection",
                "openQuestions",
            },
        };

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["outline"] = new Dictionary<string, object?> { ["type"] = "string" },
                ["editorialMemo"] = memoSchema,
            },
            ["required"] = new[] { "outline", "editorialMemo" },
        };
    }

    private static bool SupportsCustomTemperature(string model)
    {
        return !model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildUserContent(Prompt prompt)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prompt.RagContext))
        {
            parts.Add(prompt.RagContext);
        }

        parts.Add(prompt.UserOverview);
        return string.Join("\n\n", parts);
    }

    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty)";
        }

        return body.Length <= ErrorBodyMaxLength
            ? body
            : body[..ErrorBodyMaxLength] + "...";
    }
}
