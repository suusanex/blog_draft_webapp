using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Api.Models;

public sealed class GenerateTitleHookResponse
{
    public List<TitleHook> Options { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public string? Warning { get; set; }
}
