namespace BlogDraftWebApp.Core.Exceptions;

public sealed class OutlineConstraintViolationException : Exception
{
    public OutlineConstraintViolationException(string message) : base(message)
    {
    }

    public OutlineConstraintViolationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}