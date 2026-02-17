using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;
using BlogDraftWebApp.Api.Endpoints;
using BlogDraftWebApp.Api.Middleware;
using BlogDraftWebApp.Components;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using BlogDraftWebApp.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((context, config) =>
{
    config.Sources.Clear();
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);

    if (context.HostingEnvironment.IsDevelopment())
    {
        config.AddUserSecrets<Program>(optional: true);
    }

    config.AddEnvironmentVariables();

    var interim = config.Build();
    var vaultUri = interim["KeyVault:VaultUri"];
    if (!string.IsNullOrWhiteSpace(vaultUri) && Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri))
    {
        var secretClient = new SecretClient(uri, new DefaultAzureCredential());
        config.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());
    }

    config.AddCommandLine(args);
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddSingleton<LlmErrorClassifier>();
builder.Services.AddTransient<GlobalExceptionHandler>();

builder.Services.AddSingleton<ConfigurationStatus>();
builder.Services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

builder.Services.AddSingleton<IAzureSearchClientFactory, DefaultAzureSearchClientFactory>();
builder.Services.AddScoped<IRetrievalService, AzureAISearchService>();
builder.Services.AddScoped<IPromptComposer, PromptComposer>();
// E2E では実LLMを呼ばずにスタブを使うため、設定で切り替える。
var useStubLlm = builder.Configuration.GetValue<bool>("E2E:StubLlm");
    if (useStubLlm)
    {
        builder.Services.AddScoped<ILlmClient, StubLlmClient>();
    }
    else
    {
        builder.Services.AddHttpClient<OpenAiLlmClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(600);
        });
        builder.Services.AddScoped<ILlmClient>(sp => sp.GetRequiredService<OpenAiLlmClient>());
    }

builder.Services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<RagOptions>, RagOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<StyleCardOptions>, StyleCardOptionsValidator>();
builder.Services.AddSingleton<IPostConfigureOptions<LlmOptions>, LlmOptionsPostConfigure>();
builder.Services.AddSingleton<IPostConfigureOptions<StyleCardOptions>, StyleCardPostConfigure>();

builder.Services.AddOptions<LlmOptions>().Bind(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddOptions<RagOptions>().Bind(builder.Configuration.GetSection("AzureAISearch"));
builder.Services.AddOptions<StyleCardOptions>().Bind(builder.Configuration.GetSection("StyleCard"));
builder.Services.AddOptions<WorkflowOptions>().Bind(builder.Configuration.GetSection("Workflow"));

builder.Services.AddHttpClient(string.Empty, httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(600);
});

// StyleCard is treated as sensitive data; keep it server-side and cache it as a singleton.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<StyleCardOptions>>().Value;
    return new StyleCard
    {
        Title = options.Title,
        Content = options.Content,
        SystemPrompt = options.SystemPrompt,
    };
});

builder.Services.AddSingleton<WorkflowSessionLock>();
builder.Services.AddSingleton<IWorkflowRepository, LiteDbWorkflowRepository>();
builder.Services.AddScoped<IWorkflowOrchestrator, WorkflowOrchestrator>();
builder.Services.AddScoped<ClipboardService>();
builder.Services.AddHostedService<SessionCleanupService>();

var app = builder.Build();

await ValidateConfigurationOnStartupAsync(app);

app.UseMiddleware<GlobalExceptionHandler>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var status = context.RequestServices.GetRequiredService<ConfigurationStatus>();
    if (status.IsValid)
    {
        await next();
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/configuration-error", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    if (path.StartsWith("/draft", StringComparison.OrdinalIgnoreCase))
    {
        var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new BlogDraftWebApp.Api.Models.ErrorResponse
        {
            ErrorCode = "CONFIG_ERROR",
            Message = "システムが正しく構成されていません。管理者に連絡してください",
            Details = env.IsDevelopment() ? string.Join("\n", status.Errors) : null,
            RequestId = context.TraceIdentifier,
            IsRetryable = false,
        });
        return;
    }

    context.Response.Redirect("/configuration-error");
});


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthEndpoints();
app.MapDraftEndpoints();
app.MapWorkflowEndpoints();

app.Run();

static async Task ValidateConfigurationOnStartupAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var validator = scope.ServiceProvider.GetRequiredService<IConfigurationValidator>();
    await validator.ValidateAsync(CancellationToken.None);
}
