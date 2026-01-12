namespace BlogDraftWebApp.Api.Models;

public sealed class PreviewPromptResponse
{
    public string Prompt { get; set; } = string.Empty;
    public int RagHitCount { get; set; }
    public string? Warning { get; set; }
}
