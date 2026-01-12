namespace BlogDraftWebApp.Core.Models;

public sealed class Draft
{
    public string Content { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public int? TokensUsed { get; init; }
}
