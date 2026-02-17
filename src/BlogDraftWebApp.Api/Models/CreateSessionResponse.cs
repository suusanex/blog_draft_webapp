namespace BlogDraftWebApp.Api.Models;

public sealed class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset DeleteAt { get; set; }
}
