using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public sealed record RetrievalResult(IReadOnlyList<RAGChunk> Chunks, string? Warning);

public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string query, CancellationToken cancellationToken);
}
