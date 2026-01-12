namespace BlogDraftWebApp.Core.Models;

public sealed class RAGChunk
{
    public string Text { get; init; } = string.Empty;
    public double Score { get; init; }
    public string? SourceTitle { get; init; }
    public string? SourceUrl { get; init; }
}
