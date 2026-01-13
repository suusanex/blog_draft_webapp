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

        // Content は FilePath または直接の指定のいずれかが必須
        var hasContent = !string.IsNullOrWhiteSpace(options.Content);
        var hasFilePath = !string.IsNullOrWhiteSpace(options.FilePath);

        if (!hasContent && !hasFilePath)
        {
            failures.Add("StyleCard:Content or StyleCard:FilePath must be specified.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
