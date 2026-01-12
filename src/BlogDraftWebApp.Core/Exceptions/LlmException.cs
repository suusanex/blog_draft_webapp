namespace BlogDraftWebApp.Core.Exceptions;

public sealed class LlmException : Exception
{
    public LlmException(string errorCode, string message, bool isRetryable, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }

    public string ErrorCode { get; }
    public bool IsRetryable { get; }
}
