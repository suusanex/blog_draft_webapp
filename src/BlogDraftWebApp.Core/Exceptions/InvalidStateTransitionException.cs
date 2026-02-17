namespace BlogDraftWebApp.Core.Exceptions;

public sealed class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string message) : base(message)
    {
    }

    public InvalidStateTransitionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
