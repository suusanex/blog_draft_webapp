using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using BlogDraftWebApp.Api.IntegrationTests.TestFixtures;

namespace BlogDraftWebApp.Api.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _includeValidConfiguration;
    private readonly bool _invalidRagConfiguration;

    public TestWebApplicationFactory(bool includeValidConfiguration = true, bool invalidRagConfiguration = false)
    {
        _includeValidConfiguration = includeValidConfiguration;
        _invalidRagConfiguration = invalidRagConfiguration;
    }

    public Mock<IRetrievalService> RetrievalServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<ILlmClient> LlmClientMock { get; } = new(MockBehavior.Strict);

    public StyleCard StyleCard { get; } = new()
    {
        Title = "Test Style",
        SystemPrompt = "You are a test assistant.",
        Content = "## Rules\n- Output markdown",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            var values = new Dictionary<string, string?>
            {
                ["AzureAISearch:Enabled"] = _invalidRagConfiguration ? "true" : "false",
                ["Workflow:DatabasePath"] = "./data/test-workflow.db",
                ["Workflow:SessionRetentionDays"] = "30",
                ["Workflow:CleanupSchedule"] = "0 2 * * *",
            };

            if (_includeValidConfiguration)
            {
                values["OpenAI:ApiKey"] = "test-key";
                values["OpenAI:Model"] = "test-model";
                values["StyleCard:SystemPrompt"] = StyleCard.SystemPrompt;
                values["StyleCard:Content"] = StyleCard.Content;
                values["StyleCard:Title"] = StyleCard.Title;
            }

            config.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            // Replace external dependencies with mocks.
            services.AddSingleton(LlmClientMock.Object);
            services.AddSingleton(RetrievalServiceMock.Object);

            services.AddSingleton<IWorkflowRepository, InMemoryWorkflowRepository>();

            // Also register StyleCard as singleton for tests if production code uses it.
            services.AddSingleton(StyleCard);
        });
    }
}
