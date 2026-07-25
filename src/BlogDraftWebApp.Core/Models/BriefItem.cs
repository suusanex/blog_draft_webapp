namespace BlogDraftWebApp.Core.Models;

public sealed class BriefItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public BriefItemOrigin Origin { get; set; }
    public string? SourceExcerpt { get; set; }
}
