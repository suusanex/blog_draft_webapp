namespace BlogDraftWebApp.Core.Services;

public sealed class ConfigurationStatus
{
    public bool IsValid { get; set; } = true;

    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}
