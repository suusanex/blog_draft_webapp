using System.Collections.Generic;

namespace BlogDraftWebApp.Core.Configuration;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 4096;
    public int RequestTimeoutSeconds { get; set; } = 120;
    public Dictionary<string, string> Parameters { get; set; } = new();
}
