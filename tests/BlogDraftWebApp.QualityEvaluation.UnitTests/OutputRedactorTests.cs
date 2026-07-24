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

    [Test]
    public void Redact_RemovesPartialUnicodeExcerpt()
    {
        const string excerpt = "一二三四五六七八九十ABCDEFGHIJ";
        var chunks = new[]
        {
            new RAGChunk { Text = $"公開禁止の先頭情報{excerpt}公開禁止の末尾情報" },
        };
        var output = $"記事へコピーされた断片: {excerpt}";

        var redacted = OutputRedactor.Redact(output, chunks, new StyleCard());

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain(excerpt));
            Assert.That(redacted, Does.Contain("[REDACTED_SENSITIVE_CONTENT]"));
        });
    }

    [Test]
    public void Redact_MergesOverlappingSensitiveRanges()
    {
        const string output = "abcdefghijklmnopQRSTuvwxyzABCDEFGHI";
        var chunks = new[]
        {
            new RAGChunk { Text = "abcdefghijklmnopQRST" },
            new RAGChunk { Text = "QRSTuvwxyzABCDEFGHI" },
        };

        var redacted = OutputRedactor.Redact(output, chunks, new StyleCard());

        Assert.That(redacted, Is.EqualTo("[REDACTED_SENSITIVE_CONTENT]"));
    }

    [Test]
    public void Redact_IgnoresEmptyValuesAndRedactsEveryOccurrenceOfDuplicateValue()
    {
        var chunks = new[]
        {
            new RAGChunk { Text = "same-secret", SourceTitle = " ", SourceUrl = "same-secret" },
            new RAGChunk { Text = "same-secret" },
        };

        var redacted = OutputRedactor.Redact("same-secret / same-secret", chunks, new StyleCard { Content = "same-secret" });

        Assert.That(
            redacted,
            Is.EqualTo("[REDACTED_SENSITIVE_CONTENT] / [REDACTED_SENSITIVE_CONTENT]"));
    }

    [Test]
    public void Redact_DoesNotRemovePartialMatchShorterThanThreshold()
    {
        const string shortExcerpt = "123456789012345";
        var chunks = new[]
        {
            new RAGChunk { Text = $"prefix-{shortExcerpt}-suffix" },
        };

        var redacted = OutputRedactor.Redact(shortExcerpt, chunks, new StyleCard());

        Assert.That(redacted, Is.EqualTo(shortExcerpt));
    }
}
