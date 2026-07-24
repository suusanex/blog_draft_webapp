using Microsoft.Extensions.Configuration;

namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class LiveConfigurationTests
{
    [Test]
    public void Create_SkipsLiveEvaluationWhenApiKeyIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Model"] = "test-model",
            })
            .Build();

        var dependencies = LiveConfiguration.Create(configuration, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(dependencies, Is.Null);
            Assert.That(reason, Does.Contain("OpenAI:ApiKey"));
        });
    }

    [Test]
    public void Create_SkipsOnlyRagEnabledVariantWhenRagIsDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key",
                ["OpenAI:Model"] = "test-model",
                ["StyleCard:SystemPrompt"] = "test system prompt",
                ["StyleCard:Content"] = "test style card",
                ["AzureAISearch:Enabled"] = "false",
            })
            .Build();

        using var dependencies = LiveConfiguration.Create(configuration, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(reason, Is.Null);
            Assert.That(dependencies, Is.Not.Null);
            Assert.That(dependencies!.RetrievalService, Is.Null);
            Assert.That(dependencies.RagSkipReason, Does.Contain("Enabled is false"));
        });
    }

    [Test]
    public void Create_AcceptsStyleCardFilePathWithoutSystemPrompt()
    {
        var styleCardPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(styleCardPath, "style card loaded from a local file");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "test-key",
                    ["OpenAI:Model"] = "test-model",
                    ["StyleCard:FilePath"] = styleCardPath,
                    ["AzureAISearch:Enabled"] = "false",
                })
                .Build();

            using var dependencies = LiveConfiguration.Create(configuration, out var reason);

            Assert.Multiple(() =>
            {
                Assert.That(reason, Is.Null);
                Assert.That(dependencies, Is.Not.Null);
                Assert.That(dependencies!.StyleCard.Content, Is.EqualTo("style card loaded from a local file"));
                Assert.That(dependencies.StyleCard.SystemPrompt, Is.Empty);
            });
        }
        finally
        {
            File.Delete(styleCardPath);
        }
    }
}
