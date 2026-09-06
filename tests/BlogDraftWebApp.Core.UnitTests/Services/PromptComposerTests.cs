using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using NUnit.Framework;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class PromptComposerTests
{
    private static StyleCard TestStyleCard => new()
    {
        SystemPrompt = "あなたはテックブログの執筆アシスタントです。",
        Content = "## 執筆方針\n- 具体例を出す",
    };

    [Test]
    public async Task ComposeAsync_一括生成_編集方針と著者入力を含みRAGを含まない()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789 https://example.com/doc");

        var prompt = await composer.ComposeAsync(overview, TestStyleCard, CancellationToken.None);

        Assert.That(prompt.SystemMessage, Does.Contain("[システムプロンプト]"));
        Assert.That(prompt.SystemMessage, Does.Contain(TestStyleCard.SystemPrompt));
        Assert.That(prompt.SystemMessage, Does.Contain("[編集方針: 入力保持・限定補足]"));
        Assert.That(prompt.SystemMessage, Does.Contain("独自の重要度判断で削除しない"));
        Assert.That(prompt.SystemMessage, Does.Contain("[文体カード]"));
        Assert.That(prompt.SystemMessage, Does.Contain(TestStyleCard.Content));

        Assert.That(prompt.RagContext, Is.EqualTo(string.Empty));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.OneShotDraft));
        Assert.That(prompt.UserOverview, Does.Contain(PromptComposer.OneShotHeading));
        Assert.That(prompt.UserOverview, Does.Contain(PromptComposer.AuthorInputHeading));
        Assert.That(prompt.UserOverview, Does.Contain(overview.Content));
        Assert.That(prompt.UserOverview, Does.Contain("https://example.com/doc"));
        Assert.That(prompt.FullPrompt, Does.Not.Contain("過去記事からの関連情報"));
        Assert.That(prompt.FullPrompt, Does.Not.Contain("RAGコンテキスト"));
    }

    [Test]
    public async Task ComposeAsync_Step2Draft_元入力全文と確定アウトラインと編集メモを含む()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789 コマンド例 apm install");

        var prompt = await composer.ComposeAsync(
            WorkflowStep.Step2_Draft,
            overview,
            TestStyleCard,
            "- 使い方\n- 留保",
            null,
            "{\"meaningElements\":[]}",
            CancellationToken.None);

        Assert.That(prompt.UserOverview, Does.Contain(PromptComposer.DraftHeading));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.WorkflowDraft));
        Assert.That(prompt.UserOverview, Does.Contain("確定アウトライン"));
        Assert.That(prompt.UserOverview, Does.Contain("- 使い方"));
        Assert.That(prompt.UserOverview, Does.Contain("編集メモ"));
        Assert.That(prompt.UserOverview, Does.Contain("meaningElements"));
        Assert.That(prompt.UserOverview, Does.Contain(overview.Content));
        Assert.That(prompt.UserOverview, Does.Contain("apm install"));
        Assert.That(prompt.UserOverview, Does.Not.Contain("RAGコンテキスト"));
        Assert.That(prompt.RagContext, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task ComposeAsync_Step2Draft_メモ未設定でも元入力を渡す()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789");

        var prompt = await composer.ComposeAsync(
            WorkflowStep.Step2_Draft,
            overview,
            TestStyleCard,
            "- 章",
            null,
            null,
            CancellationToken.None);

        Assert.That(prompt.UserOverview, Does.Contain("未設定。元入力全文と確定アウトラインを根拠にする"));
        Assert.That(prompt.UserOverview, Does.Contain(overview.Content));
    }

    [Test]
    public async Task ComposeAsync_Step3TitleHook_確定下書きを含み本文にない主張を足さない指示がある()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789");

        var prompt = await composer.ComposeAsync(
            WorkflowStep.Step3_TitleHook,
            overview,
            TestStyleCard,
            null,
            "# 確定下書き\n本文",
            null,
            CancellationToken.None);

        Assert.That(prompt.UserOverview, Does.Contain("タイトルと導入部生成"));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.WorkflowTitleHook));
        Assert.That(prompt.UserOverview, Does.Contain("確定下書き"));
        Assert.That(prompt.UserOverview, Does.Contain("本文"));
        Assert.That(prompt.UserOverview, Does.Contain("新たな主張を足さない"));
    }

    [Test]
    public async Task ComposeAsync_Step1Outline_行数目安を要求せず編集メモJSONを求める()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789");

        var prompt = await composer.ComposeAsync(
            WorkflowStep.Step1_Outline,
            overview,
            TestStyleCard,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.That(prompt.UserOverview, Does.Contain(PromptComposer.OutlineHeading));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.WorkflowOutline));
        Assert.That(prompt.UserOverview, Does.Contain("editorialMemo"));
        Assert.That(prompt.UserOverview, Does.Contain("階層は最大 2"));
        Assert.That(prompt.UserOverview, Does.Contain("禁止: `#`見出し"));
        Assert.That(prompt.UserOverview, Does.Not.Contain("行数は 5〜15 行"));
        Assert.That(prompt.UserOverview, Does.Not.Contain("迷ったら 7〜10 行"));
        Assert.That(prompt.UserOverview, Does.Not.Contain("項目が多すぎる場合は、近い内容を統合"));
        Assert.That(prompt.SystemMessage, Does.Contain("[編集方針: 入力保持・限定補足]"));
    }

    [Test]
    public async Task ComposeTitleHookAsync_本文のみを入力としてプロンプトを構築する()
    {
        var composer = new PromptComposer();

        var prompt = await composer.ComposeTitleHookAsync(
            "これは完成した本文です。" + new string('あ', 120),
            TestStyleCard,
            CancellationToken.None);

        Assert.That(prompt.RagContext, Is.EqualTo(string.Empty));
        Assert.That(prompt.UserOverview, Does.Contain("タイトル案と冒頭段落案の生成"));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.WorkflowTitleHook));
        Assert.That(prompt.UserOverview, Does.Contain("入力本文"));
        Assert.That(prompt.UserOverview, Does.Contain("完成した本文"));
        Assert.That(prompt.UserOverview, Does.Contain("本文に書かれていない効能"));
        Assert.That(prompt.SystemMessage, Does.Contain("[編集方針: 入力保持・限定補足]"));
    }

    [Test]
    public void ComposeOutlineRepair_元入力と編集方針と形式エラーを含む()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789 残すべき具体例");
        var options = new WorkflowOptions();

        var prompt = composer.ComposeOutlineRepair(
            overview,
            TestStyleCard,
            "壊れた出力",
            "アウトラインを入力してください",
            options);

        Assert.That(prompt.UserOverview, Does.Contain(PromptComposer.OutlineRepairHeading));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.OutlineRepair));
        Assert.That(prompt.UserOverview, Does.Contain("壊れた出力"));
        Assert.That(prompt.UserOverview, Does.Contain("アウトラインを入力してください"));
        Assert.That(prompt.UserOverview, Does.Contain("残すべき具体例"));
        Assert.That(prompt.UserOverview, Does.Contain("最低項目数を満たすための増補をしない"));
        Assert.That(prompt.UserOverview, Does.Not.Contain("項目が多すぎる場合は近い内容を統合して収める"));
        Assert.That(prompt.SystemMessage, Does.Contain("[編集方針: 入力保持・限定補足]"));
    }

    [Test]
    public void ComposeDraftRepair_本文と元入力と形式エラーを含む()
    {
        var composer = new PromptComposer();
        var overview = new BlogOverview("0123456789 残すべき具体例");

        var prompt = composer.ComposeDraftRepair(
            overview,
            TestStyleCard,
            "壊れた本文JSON",
            "LLM出力がJSON形式ではありません。",
            "- 具体例",
            "{\"meaningElements\":[]}");

        Assert.That(prompt.UserOverview, Does.Contain("JSON再整形"));
        Assert.That(prompt.Kind, Is.EqualTo(PromptKind.OneShotDraft));
        Assert.That(prompt.UserOverview, Does.Contain("壊れた本文JSON"));
        Assert.That(prompt.UserOverview, Does.Contain("LLM出力がJSON形式ではありません。"));
        Assert.That(prompt.UserOverview, Does.Contain(overview.Content));
        Assert.That(prompt.UserOverview, Does.Contain("- 具体例"));
        Assert.That(prompt.UserOverview, Does.Contain("meaningElements"));
        Assert.That(prompt.UserOverview, Does.Contain("形式だけを修復"));
        Assert.That(prompt.RagContext, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task ComposeAsync_文体カードと共通方針が衝突する場合は共通方針を優先する()
    {
        var composer = new PromptComposer();
        var styleCard = new StyleCard
        {
            SystemPrompt = "文体カードのシステム指示",
            Content = "入力の細部は削除し、要点を1〜3個に絞る。",
        };

        var prompt = await composer.ComposeAsync(new BlogOverview("短い入力"), styleCard, CancellationToken.None);

        Assert.That(prompt.SystemMessage, Does.Contain("入力の細部は削除し、要点を1〜3個に絞る。"));
        Assert.That(prompt.SystemMessage, Does.Contain("文体カードに矛盾する規則がある場合は、共通編集方針を優先する"));
        Assert.That(
            prompt.SystemMessage.IndexOf("[共通編集方針の優先順位]", StringComparison.Ordinal),
            Is.GreaterThan(prompt.SystemMessage.IndexOf(styleCard.Content, StringComparison.Ordinal)));
    }
}
