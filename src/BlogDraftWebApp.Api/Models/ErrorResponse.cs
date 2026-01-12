namespace BlogDraftWebApp.Api.Models;

public sealed class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
}
