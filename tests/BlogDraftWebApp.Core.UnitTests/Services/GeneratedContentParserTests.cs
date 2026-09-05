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
        var raw = "# 見出し\n\n本文";

        var parsed = GeneratedContentParser.ParseDraft(raw);

        Assert.That(parsed.FromJson, Is.False);
        Assert.That(parsed.IsValid, Is.False);
        Assert.That(parsed.Draft, Is.Empty);
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void ParseDraft_壊れたJSONは無効として要確認事項も返さない()
    {
        var parsed = GeneratedContentParser.ParseDraft("{\"draft\":\"本文\",\"openQuestions\":[}");

        Assert.That(parsed.IsValid, Is.False);
        Assert.That(parsed.Draft, Is.Empty);
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void TryRecoverMarkdownDraft_JSON再整形後もMarkdownなら本文として復旧する()
    {
        var recovered = GeneratedContentParser.TryRecoverMarkdownDraft(
            "# 見出し\n\n本文",
            out var parsed);

        Assert.That(recovered, Is.True);
        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.FromJson, Is.False);
        Assert.That(parsed.Draft, Is.EqualTo("# 見出し\n\n本文"));
        Assert.That(parsed.OpenQuestions, Is.Empty);
    }

    [Test]
    public void TryRecoverMarkdownDraft_説明文だけは復旧しない()
    {
        var recovered = GeneratedContentParser.TryRecoverMarkdownDraft("JSONではありません", out _);

        Assert.That(recovered, Is.False);
    }

    [Test]
    public void ParseDraft_二重化されたJSON本文を1段だけアンラップする()
    {
        var parsed = GeneratedContentParser.ParseDraft(
            "{\"draft\":\"{\\\"draft\\\":\\\"本文\\\",\\\"openQuestions\\\":[\\\"確認事項\\\"]}\",\"openQuestions\":[]}");

        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.Draft, Is.EqualTo("本文"));
        Assert.That(parsed.OpenQuestions, Is.EqualTo(new[] { "確認事項" }));
    }

    [Test]
    public void ParseDraft_多重化されたJSON本文も有限回でアンラップする()
    {
        var inner = "{\"draft\":\"本文\",\"openQuestions\":[\"確認事項\"]}";
        var middle = $"{{\"draft\":{System.Text.Json.JsonSerializer.Serialize(inner)},\"openQuestions\":[]}}";
        var raw = $"{{\"draft\":{System.Text.Json.JsonSerializer.Serialize(middle)},\"openQuestions\":[]}}";

        var parsed = GeneratedContentParser.ParseDraft(raw);

        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.Draft, Is.EqualTo("本文"));
        Assert.That(parsed.OpenQuestions, Is.EqualTo(new[] { "確認事項" }));
    }

    [Test]
    public void ParseDraft_壊れたJSONから十分な長さのdraft文字列を救済する()
    {
        var body = "# 見出し\n\nこれは壊れたJSONでも保持して返す十分な長さの本文です。";
        var parsed = GeneratedContentParser.ParseDraft($"{{\"draft\":{System.Text.Json.JsonSerializer.Serialize(body)},\"openQuestions\":[}}");

        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.FromJson, Is.False);
        Assert.That(parsed.Draft, Is.EqualTo(body));
    }

    [Test]
    public void ParseOutline_壊れたJSONを原文成功として扱わない()
    {
        var parsed = GeneratedContentParser.ParseOutline("{\"outline\":\"- 背景\"");

        Assert.That(parsed.IsMalformedJson, Is.True);
    }
}
