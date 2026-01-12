using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Configuration;

public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("OpenAI:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("OpenAI:Model is required.");
        }

        if (options.MaxTokens <= 0)
        {
            failures.Add("OpenAI:MaxTokens must be greater than 0.");
        }

        if (!string.IsNullOrWhiteSpace(options.BaseUrl) && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add("OpenAI:BaseUrl must be an absolute URI when provided.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
