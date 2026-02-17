using LiteDB;

namespace BlogDraftWebApp.Core.Models;

public sealed class RagSnapshot
{
    [BsonId]
    public string SnapshotId { get; init; } = string.Empty;
    public string SearchQuery { get; init; } = string.Empty;
    public DateTimeOffset SearchExecutedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<RAGChunk> Chunks { get; init; } = new();
    public int ChunkCount { get; init; }
}
