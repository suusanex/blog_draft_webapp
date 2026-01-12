using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Configuration;

public sealed class RagOptionsValidator : IValidateOptions<RagOptions>
{
    public ValidateOptionsResult Validate(string? name, RagOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            failures.Add("AzureAISearch:Endpoint is required when AzureAISearch:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("AzureAISearch:ApiKey is required when AzureAISearch:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.IndexName))
        {
            failures.Add("AzureAISearch:IndexName is required when AzureAISearch:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.TextFieldName))
        {
            failures.Add("AzureAISearch:TextFieldName is required when AzureAISearch:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.VectorFieldName))
        {
            failures.Add("AzureAISearch:VectorFieldName is required when AzureAISearch:Enabled is true.");
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint) && !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            failures.Add("AzureAISearch:Endpoint must be an absolute URI.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
