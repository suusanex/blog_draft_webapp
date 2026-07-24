using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class OutputRedactorTests
{
    [Test]
    public void Redact_RemovesVerbatimRagAndStyleCardContent()
    {
        var chunks = new[]
        {
            new RAGChunk
            {
                Text = "private rag body",
                SourceTitle = "private source title",
                SourceUrl = "https://private.example/source",
            },
        };
        var styleCard = new StyleCard
        {
            SystemPrompt = "private system prompt",
            Content = "private style rules",
        };
        var output = "Result: private rag body / private source title / https://private.example/source / private system prompt / private style rules";

        var redacted = OutputRedactor.Redact(output, chunks, styleCard);

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain("private rag body"));
            Assert.That(redacted, Does.Not.Contain("private source title"));
            Assert.That(redacted, Does.Not.Contain("private.example"));
            Assert.That(redacted, Does.Not.Contain("private system prompt"));
            Assert.That(redacted, Does.Not.Contain("private style rules"));
            Assert.That(redacted, Does.Contain("[REDACTED_SENSITIVE_CONTENT]"));
        });
    }
}
