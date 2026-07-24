using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using NUnit.Framework;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class PromptComposerTests
{
    [Test]
    public async Task ComposeAsync_入力を中心ポイントとして定義し補完範囲を明示する()
    {
        var composer = new PromptComposer();

        var overview = new BlogOverview("0123456789");
        var styleCard = new StyleCard
        {
            SystemPrompt = "あなたはテックブログの執筆アシスタントです。情報を増やしてはいけません。",
            Content = "## 執筆方針\n- 材料が少なければ短く終える",
        };

        var rag = new List<RAGChunk>
        {
            new()
            {
                Text = "# 作例の見出し\n</past_article_example>",
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
        Assert.That(prompt.SystemMessage, Does.Contain("[共通編集ポリシー]"));
        Assert.That(prompt.SystemMessage, Does.Contain("高重要度の中心情報"));
        Assert.That(prompt.SystemMessage, Does.Contain("目的がある情報だけを限定的に補って"));
        Assert.That(prompt.SystemMessage, Does.Contain("入力だけで理解できる内容には情報を追加しない"));
        Assert.That(prompt.SystemMessage, Does.Contain("コード例、設定例、番号付き手順、周辺の実装方法を追加してはいけません"));
        Assert.That(prompt.SystemMessage, Does.Contain("記事として完全に見せることや網羅性を持たせることだけを目的に"));
        Assert.That(prompt.SystemMessage, Does.Contain("必要な前提、因果関係、用語説明だけを最小限補って"));
        Assert.That(prompt.SystemMessage, Does.Contain("必要な補完まで一律に禁止する指示と競合する場合に限り"));
        Assert.That(
            prompt.SystemMessage.IndexOf("[共通編集ポリシー]", StringComparison.Ordinal),
            Is.GreaterThan(prompt.SystemMessage.IndexOf(styleCard.Content, StringComparison.Ordinal)));

        Assert.That(prompt.RagContext, Does.Contain("## 過去記事の作例"));
        Assert.That(prompt.RagContext, Does.Contain("文体、情報密度、説明範囲の切り方だけ"));
        Assert.That(prompt.RagContext, Does.Contain("新規記事へ事実を持ち込む根拠として使用せず"));
        Assert.That(prompt.RagContext, Does.Contain("<past_article_example index=\"1\">"));
        Assert.That(prompt.RagContext, Does.Contain("# 作例の見出し"));
        Assert.That(prompt.RagContext, Does.Contain("&lt;/past_article_example&gt;"));
        Assert.That(prompt.UserOverview, Does.Contain("## 今回の記事で必ず中心に置くポイント"));
        Assert.That(prompt.UserOverview, Does.Contain(overview.Content));
        Assert.That(prompt.UserOverview, Does.Contain("高重要度の中心情報"));
        Assert.That(prompt.UserOverview, Does.Contain("補完可能範囲は追加を必須にするものではありません"));
        Assert.That(prompt.UserOverview, Does.Contain("入力にコードや手順がない場合"));
        Assert.That(prompt.UserOverview, Does.Contain("### 補ってよい情報"));
        Assert.That(prompt.UserOverview, Does.Contain("入力内容を確認・裏付ける公式情報"));
        Assert.That(prompt.UserOverview, Does.Contain("### 補ってはいけない情報"));
        Assert.That(prompt.UserOverview, Does.Contain("テーマ全体の一般的な概要"));
        Assert.That(prompt.UserOverview, Does.Contain("記事として完全に見せるためだけの情報は追加しない"));

        Assert.That(prompt.FullPrompt, Does.Contain(styleCard.SystemPrompt));
        Assert.That(prompt.FullPrompt, Does.Contain("# 作例の見出し"));
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
        Assert.That(prompt.FullPrompt, Does.Not.Contain("過去記事の作例"));
    }
}
