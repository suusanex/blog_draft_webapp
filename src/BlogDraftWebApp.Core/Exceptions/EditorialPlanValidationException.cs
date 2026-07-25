namespace BlogDraftWebApp.Core.Exceptions;

public sealed class EditorialPlanValidationException : Exception
{
    public EditorialPlanValidationException(string message)
        : base(message)
    {
    }
}
