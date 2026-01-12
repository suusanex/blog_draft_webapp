using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using NUnit.Framework;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class PromptComposerTests
{
    [Test]
    public async Task ComposeAsync_RagChunksあり_SystemとRagと概要を結合する()
    {
        var composer = new PromptComposer();

        var overview = new BlogOverview("0123456789");
        var styleCard = new StyleCard
        {
            SystemPrompt = "あなたはテックブログの執筆アシスタントです。",
            Content = "## 執筆方針\n- 具体例を出す",
        };

        var rag = new List<RAGChunk>
        {
            new()
            {
                Text = "chunk1",
                Score = 0.9,
                SourceTitle = "t1",
                SourceUrl = "https://example.com/1",
            },
        };

        var prompt = await composer.ComposeAsync(overview, rag, styleCard, CancellationToken.None);

        Assert.That(prompt.SystemMessage, Does.Contain("[システムプロンプト]"));
        Assert.That(prompt.SystemMessage, Does.Contain(styleCard.SystemPrompt));
        Assert.That(prompt.SystemMessage, Does.Contain("[文体カード]"));
        Assert.That(prompt.SystemMessage, Does.Contain(styleCard.Content));

        Assert.That(prompt.RagContext, Does.Contain("## 過去記事からの関連情報"));
        Assert.That(prompt.RagContext, Does.Contain("chunk1"));
        Assert.That(prompt.UserOverview, Does.Contain("## 新規記事の概要"));
        Assert.That(prompt.UserOverview, Does.Contain(overview.Content));

        Assert.That(prompt.FullPrompt, Does.Contain(styleCard.SystemPrompt));
        Assert.That(prompt.FullPrompt, Does.Contain("chunk1"));
        Assert.That(prompt.FullPrompt, Does.Contain(overview.Content));
    }

    [Test]
    public async Task ComposeAsync_RagChunksなし_RagContextは空になる()
    {
        var composer = new PromptComposer();

        var overview = new BlogOverview("0123456789");
        var styleCard = new StyleCard
        {
            SystemPrompt = "sys",
            Content = "content",
        };

        var prompt = await composer.ComposeAsync(overview, Array.Empty<RAGChunk>(), styleCard, CancellationToken.None);

        Assert.That(prompt.RagContext, Is.EqualTo(string.Empty));
        Assert.That(prompt.FullPrompt, Does.Not.Contain("過去記事からの関連情報"));
    }
}
