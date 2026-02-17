namespace BlogDraftWebApp.Core.Exceptions;

public sealed class WorkflowStorageException : Exception
{
    public WorkflowStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
