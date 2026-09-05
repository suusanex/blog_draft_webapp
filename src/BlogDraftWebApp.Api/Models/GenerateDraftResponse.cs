namespace BlogDraftWebApp.Api.Models;

public sealed class GenerateDraftResponse
{
    public string Draft { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public int RagHitCount { get; set; }
    public string? Warning { get; set; }
    public List<string> OpenQuestions { get; set; } = new();
}
