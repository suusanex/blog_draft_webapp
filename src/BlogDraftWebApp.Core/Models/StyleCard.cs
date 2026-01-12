namespace BlogDraftWebApp.Core.Models;

public sealed class StyleCard
{
    public string? Title { get; init; }
    public string Content { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
}
