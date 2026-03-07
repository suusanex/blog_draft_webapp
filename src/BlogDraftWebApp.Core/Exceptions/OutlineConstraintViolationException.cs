namespace BlogDraftWebApp.Core.Exceptions;

public enum OutlineViolationSource
{
    UserInput = 0,
    LlmGenerated = 1,
}

public sealed class OutlineConstraintViolationException : Exception
{
    public OutlineConstraintViolationException(string message, OutlineViolationSource source = OutlineViolationSource.UserInput)
        : base(message)
    {
        SourceKind = source;
    }

    public OutlineConstraintViolationException(string message, Exception innerException, OutlineViolationSource source = OutlineViolationSource.UserInput)
        : base(message, innerException)
    {
        SourceKind = source;
    }

    public OutlineViolationSource SourceKind { get; }
}
