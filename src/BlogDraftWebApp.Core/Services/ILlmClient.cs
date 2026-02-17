using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public interface ILlmClient
{
    Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null);
}
