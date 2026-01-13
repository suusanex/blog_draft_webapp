namespace BlogDraftWebApp.Core.Configuration;

public sealed class StyleCardOptions
{
    public string? Title { get; set; }
    public string? FilePath { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
}
