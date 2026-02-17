using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public interface IPromptComposer
{
    Task<Prompt> ComposeAsync(BlogOverview overview, IReadOnlyList<RAGChunk> ragChunks, StyleCard styleCard, CancellationToken cancellationToken);

    Task<Prompt> ComposeApprovedAsync(
        BlogOverview overview,
        EditorialPlan plan,
        IReadOnlyList<RAGChunk> ragChunks,
        StyleCard styleCard,
        CancellationToken cancellationToken);

    Task<Prompt> ComposeAsync(
        WorkflowStep step,
        BlogOverview overview,
        IReadOnlyList<RAGChunk> ragChunks,
        StyleCard styleCard,
        string? outline,
        string? draft,
        CancellationToken cancellationToken);
}
