using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Configuration;

public sealed class StyleCardOptionsValidator : IValidateOptions<StyleCardOptions>
{
    public ValidateOptionsResult Validate(string? name, StyleCardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SystemPrompt))
        {
            failures.Add("StyleCard:SystemPrompt is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Content))
        {
            failures.Add("StyleCard:Content is required.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
