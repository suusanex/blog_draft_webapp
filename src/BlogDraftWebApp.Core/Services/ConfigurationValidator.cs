using BlogDraftWebApp.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Services;

public sealed class ConfigurationValidator : IConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator> _logger;
    private readonly ConfigurationStatus _status;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly IOptions<RagOptions> _ragOptions;
    private readonly IOptions<StyleCardOptions> _styleCardOptions;
    private readonly IValidateOptions<LlmOptions> _llmValidator;
    private readonly IValidateOptions<RagOptions> _ragValidator;
    private readonly IValidateOptions<StyleCardOptions> _styleCardValidator;

    public ConfigurationValidator(
        ILogger<ConfigurationValidator> logger,
        ConfigurationStatus status,
        IOptions<LlmOptions> llmOptions,
        IOptions<RagOptions> ragOptions,
        IOptions<StyleCardOptions> styleCardOptions,
        IValidateOptions<LlmOptions> llmValidator,
        IValidateOptions<RagOptions> ragValidator,
        IValidateOptions<StyleCardOptions> styleCardValidator)
    {
        _logger = logger;
        _status = status;
        _llmOptions = llmOptions;
        _ragOptions = ragOptions;
        _styleCardOptions = styleCardOptions;
        _llmValidator = llmValidator;
        _ragValidator = ragValidator;
        _styleCardValidator = styleCardValidator;
    }

    public Task<bool> ValidateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var failures = new List<string>();

            AppendFailures(failures, _llmValidator.Validate(name: null, _llmOptions.Value));
            AppendFailures(failures, _ragValidator.Validate(name: null, _ragOptions.Value));
            AppendFailures(failures, _styleCardValidator.Validate(name: null, _styleCardOptions.Value));

            if (failures.Count == 0)
            {
                _status.IsValid = true;
                _status.Errors = Array.Empty<string>();
                _logger.LogInformation("Configuration validation succeeded.");
                return Task.FromResult(true);
            }

            _status.IsValid = false;
            _status.Errors = failures;

            _logger.LogError("Configuration validation failed. ErrorCount={ErrorCount}", failures.Count);
            foreach (var failure in failures)
            {
                _logger.LogError("Configuration error: {Failure}", failure);
            }

            return Task.FromResult(false);
        }
        catch (OptionsValidationException ex)
        {
            var failures = ex.Failures?.ToList() ?? new List<string>();
            if (failures.Count == 0)
            {
                failures.Add("Configuration validation failed.");
            }

            _status.IsValid = false;
            _status.Errors = failures;

            _logger.LogError(ex, "Configuration validation failed with OptionsValidationException. {Exception}", ex.ToString());
            _logger.LogError("Configuration validation failed. ErrorCount={ErrorCount}", failures.Count);
            foreach (var failure in failures)
            {
                _logger.LogError("Configuration error: {Failure}", failure);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _status.IsValid = false;
            _status.Errors = new[] { "Configuration validation threw an exception." };
            _logger.LogError(ex, "Configuration validation failed with exception. {Exception}", ex.ToString());
            return Task.FromResult(false);
        }
    }

    private static void AppendFailures(List<string> failures, ValidateOptionsResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.Failures is null)
        {
            failures.Add("Unknown configuration validation failure.");
            return;
        }

        failures.AddRange(result.Failures);
    }
}
