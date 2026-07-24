using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class SourceIdentityTests
{
    [Test]
    public void Create_IsStableAndDoesNotExposeSourceMetadata()
    {
        var chunk = new RAGChunk
        {
            SourceUrl = "https://private.example/article/42",
            SourceTitle = "Private title",
            Text = "Private body",
        };

        var first = SourceIdentity.Create(chunk);
        var second = SourceIdentity.Create(chunk);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("sha256:"));
            Assert.That(first, Does.Not.Contain("private").IgnoreCase);
            Assert.That(first, Has.Length.EqualTo(71));
        });
    }
}
