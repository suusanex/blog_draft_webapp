namespace BlogDraftWebApp.Api.Models;

public sealed class SessionListResponse
{
    public List<SessionSummaryItem> Sessions { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class SessionSummaryItem
{
    public string SessionId { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastAccessedAt { get; set; }
    public DateTimeOffset DeleteAt { get; set; }
}
