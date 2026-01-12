namespace BlogDraftWebApp.Core.Exceptions;

public sealed class RagException : Exception
{
    public RagException(string message)
        : base(message)
    {
    }

    public RagException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
