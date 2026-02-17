namespace BlogDraftWebApp.Api.Models;

public sealed class PreviewPromptResponse
{
    public string? SessionId { get; set; }
    public string? Step { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public int RagHitCount { get; set; }
    public string? RagSnapshotId { get; set; }
    public string? Warning { get; set; }
}
