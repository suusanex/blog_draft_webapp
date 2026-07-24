using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.QualityEvaluation;

public sealed class LiveDependencies : IDisposable
{
    public required LlmOptions LlmOptions { get; init; }
    public required RagOptions RagOptions { get; init; }
    public required StyleCard StyleCard { get; init; }
    public required ILlmClient LlmClient { get; init; }
    public IRetrievalService? RetrievalService { get; init; }
    public string? RagSkipReason { get; init; }
    public required HttpClient HttpClient { get; init; }

    public void Dispose() => HttpClient.Dispose();
}

public static class LiveConfiguration
{
    public static LiveDependencies? Load(string repositoryRoot, IReadOnlyList<string> configurationArguments, out string? skipReason)
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";
        var applicationDirectory = Path.Combine(repositoryRoot, "src", "BlogDraftWebApp");
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(applicationDirectory);
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        configuration.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);

        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            configuration.AddUserSecrets<UserSecretsMarker>(optional: true);
        }

        configuration.AddEnvironmentVariables();
        var vaultUri = configuration["KeyVault:VaultUri"];
        if (!string.IsNullOrWhiteSpace(vaultUri) && Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri))
        {
            configuration.AddAzureKeyVault(new SecretClient(uri, new DefaultAzureCredential()), new KeyVaultSecretManager());
        }

        if (configurationArguments.Count > 0)
        {
            configuration.AddCommandLine(configurationArguments.ToArray());
        }

        return Create(configuration, out skipReason);
    }

    public static LiveDependencies? Create(IConfiguration configuration, out string? skipReason)
    {
        var llmOptions = configuration.GetSection("OpenAI").Get<LlmOptions>() ?? new LlmOptions();
        new LlmOptionsPostConfigure().PostConfigure(null, llmOptions);
        if (string.IsNullOrWhiteSpace(llmOptions.ApiKey))
        {
            skipReason = "Live evaluation skipped because OpenAI:ApiKey is not configured.";
            return null;
        }

        ValidateOptions("OpenAI", new LlmOptionsValidator().Validate(null, llmOptions));

        var styleOptions = configuration.GetSection("StyleCard").Get<StyleCardOptions>() ?? new StyleCardOptions();
        new StyleCardPostConfigure(NullLogger<StyleCardPostConfigure>.Instance).PostConfigure(null, styleOptions);
        ValidateOptions("StyleCard", new StyleCardOptionsValidator().Validate(null, styleOptions));
        if (string.IsNullOrWhiteSpace(styleOptions.SystemPrompt))
        {
            throw new InvalidOperationException("StyleCard:SystemPrompt is required for live evaluation.");
        }

        var ragOptions = configuration.GetSection("AzureAISearch").Get<RagOptions>() ?? new RagOptions { Enabled = false };
        var ragValidation = new RagOptionsValidator().Validate(null, ragOptions);
        string? ragSkipReason = null;
        IRetrievalService? retrievalService = null;
        if (!ragOptions.Enabled)
        {
            ragSkipReason = "RAG enabled evaluation skipped because AzureAISearch:Enabled is false.";
        }
        else if (ragValidation.Failed)
        {
            ragSkipReason = $"RAG enabled evaluation skipped: {string.Join("; ", ragValidation.Failures)}";
        }
        else
        {
            retrievalService = new AzureAISearchService(
                NullLogger<AzureAISearchService>.Instance,
                Options.Create(ragOptions),
                new DefaultAzureSearchClientFactory());
        }

        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(600) };
        skipReason = null;
        return new LiveDependencies
        {
            LlmOptions = llmOptions,
            RagOptions = ragOptions,
            StyleCard = new StyleCard
            {
                Title = styleOptions.Title,
                Content = styleOptions.Content,
                SystemPrompt = styleOptions.SystemPrompt,
            },
            LlmClient = new OpenAiLlmClient(httpClient, NullLogger<OpenAiLlmClient>.Instance, Options.Create(llmOptions)),
            RetrievalService = retrievalService,
            RagSkipReason = ragSkipReason,
            HttpClient = httpClient,
        };
    }

    private static void ValidateOptions(string section, ValidateOptionsResult result)
    {
        if (result.Failed)
        {
            throw new InvalidOperationException($"Invalid {section} configuration: {string.Join("; ", result.Failures)}");
        }
    }
}

internal sealed class UserSecretsMarker;
