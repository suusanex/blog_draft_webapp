namespace BlogDraftWebApp.Core.Models;

public sealed class PlannedSection
{
    public string Id { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string> SourceItemIds { get; set; } = [];
    public List<string> ExcludedScopeItemIds { get; set; } = [];
}
