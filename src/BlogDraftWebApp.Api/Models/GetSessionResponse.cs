using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Api.Models;

public sealed class GetSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastAccessedAt { get; set; }
    public DateTimeOffset DeleteAt { get; set; }
    public string InitialInput { get; set; } = string.Empty;
    public string? RagSnapshotId { get; set; }
    public string? OutlineGenerated { get; set; }
    public string? OutlineEdited { get; set; }
    public string? OutlineConfirmed { get; set; }
    public string? EditorialMemoJson { get; set; }
    public string? DraftGenerated { get; set; }
    public string? DraftEdited { get; set; }
    public string? DraftConfirmed { get; set; }
    public List<string>? OpenQuestions { get; set; }
    public List<TitleHook>? TitleHookOptions { get; set; }
    public TitleHook? TitleHookSelected { get; set; }
    public TitleHook? TitleHookConfirmed { get; set; }
}
