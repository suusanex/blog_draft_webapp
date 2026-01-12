namespace BlogDraftWebApp.Core.Services;

public interface IConfigurationValidator
{
    Task<bool> ValidateAsync(CancellationToken cancellationToken);
}
