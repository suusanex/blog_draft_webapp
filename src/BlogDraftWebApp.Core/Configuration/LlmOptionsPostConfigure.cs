using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Configuration;

public sealed class LlmOptionsPostConfigure : IPostConfigureOptions<LlmOptions>
{
    public void PostConfigure(string? name, LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RequestTimeoutSeconds <= 0)
        {
            options.RequestTimeoutSeconds = 600;
        }
    }
}
