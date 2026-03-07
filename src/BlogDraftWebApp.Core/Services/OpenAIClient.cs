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
    private const int WorkflowMaxTimeoutSeconds = 60;

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
        var configuredSeconds = _options.RequestTimeoutSeconds <= 0
            ? WorkflowMaxTimeoutSeconds
            : _options.RequestTimeoutSeconds;
        var timeoutSeconds = Math.Min(configuredSeconds, WorkflowMaxTimeoutSeconds);
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

            if (ShouldUseLowTemperature(prompt))
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
                throw new ConfigurationException("LLM authentication failed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var isRetryable = response.StatusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout
                    or HttpStatusCode.BadGateway;

                throw new LlmException("LLM_ERROR", "LLM サービスでエラーが発生しました", isRetryable);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var content = json.RootElement
                .GetProperty("choices")[0]
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
        return prompt.UserOverview.Contains("アウトライン生成（厳格フォーマット）", StringComparison.Ordinal)
            || prompt.UserOverview.Contains("アウトライン再整形", StringComparison.Ordinal);
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
}
