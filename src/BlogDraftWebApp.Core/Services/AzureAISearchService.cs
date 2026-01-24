using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Services;

public sealed record AzureSearchHit(double Score, IReadOnlyDictionary<string, object?> Fields);

public interface IAzureSearchClient
{
    Task<IReadOnlyList<AzureSearchHit>> SearchAsync(string query, int topK, CancellationToken cancellationToken);
}

public interface IAzureSearchClientFactory
{
    IAzureSearchClient CreateClient(RagOptions options);
}

public sealed class DefaultAzureSearchClientFactory : IAzureSearchClientFactory
{
    public IAzureSearchClient CreateClient(RagOptions options)
    {
        var searchClient = new SearchClient(new Uri(options.Endpoint), options.IndexName, new AzureKeyCredential(options.ApiKey));
        return new AzureSearchClientAdapter(searchClient, options);
    }

    private sealed class AzureSearchClientAdapter : IAzureSearchClient
    {
        private readonly SearchClient _client;
        private readonly RagOptions _options;

        public AzureSearchClientAdapter(SearchClient client, RagOptions options)
        {
            _client = client;
            _options = options;
        }

        public async Task<IReadOnlyList<AzureSearchHit>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
        {
            var searchOptions = new SearchOptions
            {
                Size = topK,
            };

            searchOptions.Select.Add(_options.TextFieldName);
            searchOptions.Select.Add("title");
            searchOptions.Select.Add("url");

            var response = await _client.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);

            var hits = new List<AzureSearchHit>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in result.Document)
                {
                    fields[kvp.Key] = kvp.Value;
                }

                hits.Add(new AzureSearchHit(result.Score ?? 0d, fields));
            }

            return hits;
        }
    }
}

public sealed class AzureAISearchService : IRetrievalService
{
    private const int MaxChunkLength = 500;

    private readonly ILogger<AzureAISearchService> _logger;
    private readonly RagOptions _options;
    private readonly IAzureSearchClientFactory _searchClientFactory;

    public AzureAISearchService(
        ILogger<AzureAISearchService> logger,
        IOptions<RagOptions> options,
        IAzureSearchClientFactory searchClientFactory)
    {
        _logger = logger;
        _options = options.Value;
        _searchClientFactory = searchClientFactory;
    }

    public async Task<RetrievalResult> RetrieveAsync(string query, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new RetrievalResult(Array.Empty<RAGChunk>(), null);
        }

        try
        {
            var client = _searchClientFactory.CreateClient(_options);
            var hits = await client.SearchAsync(query, _options.TopK, cancellationToken);

            var results = new List<RAGChunk>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hit in hits)
            {
                if (hit.Score < _options.MinimumScore)
                {
                    continue;
                }

                var text = TryGetString(hit.Fields, _options.TextFieldName);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                text = NormalizeChunkText(text);
                var sourceTitle = TryGetString(hit.Fields, "title");
                var sourceUrl = TryGetString(hit.Fields, "url");

                var dedupeKey = !string.IsNullOrWhiteSpace(sourceUrl)
                    ? sourceUrl
                    : (!string.IsNullOrWhiteSpace(sourceTitle) ? sourceTitle : text[..Math.Min(64, text.Length)]);

                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                results.Add(new RAGChunk
                {
                    Text = text,
                    Score = hit.Score,
                    SourceTitle = sourceTitle,
                    SourceUrl = sourceUrl,
                });
            }

            return new RetrievalResult(results, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed and cannot continue.");
            throw new RagException("関連記事の検索に失敗しました。", ex);
        }
    }

    private static string NormalizeChunkText(string text)
    {
        text = text.Trim();
        if (text.Length <= MaxChunkLength)
        {
            return text;
        }

        return text[..MaxChunkLength] + "...";
    }

    private static string? TryGetString(IReadOnlyDictionary<string, object?> fields, string key)
    {
        if (fields.TryGetValue(key, out var value) && value is not null)
        {
            return value.ToString();
        }

        return null;
    }
}
