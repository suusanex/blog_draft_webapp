namespace BlogDraftWebApp.Api.Models;

public sealed class GenerateStepResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string Generated { get; set; } = string.Empty;
    public int RagHitCount { get; set; }
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public string? Warning { get; set; }
}
