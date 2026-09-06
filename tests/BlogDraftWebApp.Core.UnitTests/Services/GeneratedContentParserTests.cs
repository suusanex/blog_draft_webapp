using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class GeneratedContentParserTests
{
    [Test]
    public void ParseOutline_JSONを分離する()
    {
        var raw = """
            ```json
            {
              "outline": "- 背景\n- 結論",
              "editorialMemo": {
                "articleQuestion": "なぜか",
                "openQuestions": ["要確認"]
              }
            }
            ```
            """;

        var parsed = GeneratedContentParser.ParseOutline(raw);

        Assert.That(parsed.FromJson, Is.True);
        Assert.That(parsed.IsMalformedJson, Is.False);
        Assert.That(parsed.Outline, Is.EqualTo("- 背景\n- 結論"));
        Assert.That(parsed.EditorialMemo, Is.Not.Null);
        Assert.That(parsed.EditorialMemo!.ArticleQuestion, Is.EqualTo("なぜか"));
        Assert.That(parsed.EditorialMemo.OpenQuestions, Does.Contain("要確認"));
    }

    [Test]
    public void ParseOutline_JSONでない場合は原文をアウトラインにする()
    {
        var raw = "- 背景\n- 結論";

        var parsed = GeneratedContentParser.ParseOutline(raw);

        Assert.That(parsed.FromJson, Is.False);
        Assert.That(parsed.IsMalformedJson, Is.False);
        Assert.That(parsed.Outline, Is.EqualTo(raw));
        Assert.That(parsed.EditorialMemo, Is.Null);
    }

    [Test]
    public void ParseDraft_JSONを分離する()
    {
        var raw = """
            {
              "draft": "# 見出し\n\n本文",
              "openQuestions": ["確認A", ""]
            }
            """;

        var parsed = GeneratedContentParser.ParseDraft(raw);

        Assert.That(parsed.FromJson, Is.True);
        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.Draft, Is.EqualTo("# 見出し\n\n本文"));
        Assert.That(parsed.OpenQuestions, Is.EqualTo(new[] { "確認A" }));
    }

    [Test]
    public void ParseDraft_JSONでない場合は無効として原文を本文にしない()
    {
        var parsed = GeneratedContentParser.ParseDraft("# 見出し\n\n本文");

        Assert.That(parsed.FromJson, Is.False);
        Assert.That(parsed.IsValid, Is.False);
        Assert.That(parsed.Draft, Is.Empty);
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void ParseDraft_壊れたJSONから本文だけを救済せず無効にする()
    {
        var parsed = GeneratedContentParser.ParseDraft("{\"draft\":\"本文\",\"openQuestions\":[}");

        Assert.That(parsed.IsValid, Is.False);
        Assert.That(parsed.Draft, Is.Empty);
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void ParseDraft_openQuestionsが欠落している場合は無効にする()
    {
        var parsed = GeneratedContentParser.ParseDraft("{\"draft\":\"本文\"}");

        Assert.That(parsed.IsValid, Is.False);
        Assert.That(parsed.Draft, Is.Empty);
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void ParseDraft_openQuestionsが文字列配列でない場合は無効にする()
    {
        var parsed = GeneratedContentParser.ParseDraft("{\"draft\":\"本文\",\"openQuestions\":\"確認事項\"}");

        Assert.That(parsed.IsValid, Is.False);
        Assert.That(parsed.Draft, Is.Empty);
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void ParseDraft_入れ子JSONをアンラップせず本文として扱う()
    {
        var raw = "{\"draft\":\"{\\\"draft\\\":\\\"本文\\\",\\\"openQuestions\\\":[\\\"確認事項\\\"]}\",\"openQuestions\":[]}";

        var parsed = GeneratedContentParser.ParseDraft(raw);

        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.Draft, Does.StartWith("{\"draft\":"));
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void ParseOutline_壊れたJSONを原文成功として扱わない()
    {
        var parsed = GeneratedContentParser.ParseOutline("{\"outline\":\"- 背景\"");

        Assert.That(parsed.IsMalformedJson, Is.True);
    }
}
