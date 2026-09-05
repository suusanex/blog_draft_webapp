using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class OutlineValidatorTests
{
    private static readonly WorkflowOptions DefaultOptions = new()
    {
        OutlineMinLines = 1,
        OutlineMaxLines = 80,
        OutlineMaxDepth = 2,
        OutlineMaxLineLength = 120,
        OutlineMaxTotalChars = 8000,
    };

    [Test]
    public void ValidateOrThrow_有効な箇条書き_例外を投げない()
    {
        var validator = new OutlineValidator();
        var content = string.Join("\n", new[]
        {
            "- 背景",
            "  - 課題",
            "- 目的",
            "  - 対象読者",
            "- 結論",
        });

        Assert.DoesNotThrow(() => validator.ValidateOrThrow(content, DefaultOptions));
    }

    [Test]
    public void ValidateOrThrow_1行_例外を投げない()
    {
        var validator = new OutlineValidator();

        Assert.DoesNotThrow(() => validator.ValidateOrThrow("- 一点だけ", DefaultOptions));
    }

    [Test]
    public void ValidateOrThrow_20行_例外を投げない()
    {
        var validator = new OutlineValidator();
        var lines = Enumerable.Range(1, 20).Select(i => $"- item {i}");

        Assert.DoesNotThrow(() => validator.ValidateOrThrow(string.Join("\n", lines), DefaultOptions));
    }

    [TestCase("# 見出し\n- a\n- b\n- c\n- d", "箇条書き")]
    [TestCase("1. 番号\n- a\n- b\n- c\n- d", "箇条書き")]
    [TestCase("文章だけ\n- a\n- b\n- c\n- d", "箇条書き")]
    public void ValidateOrThrow_禁止フォーマット_例外を投げる(string content, string expected)
    {
        var validator = new OutlineValidator();

        var ex = Assert.Throws<OutlineConstraintViolationException>(() =>
            validator.ValidateOrThrow(content, DefaultOptions));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain(expected));
    }

    [Test]
    public void ValidateOrThrow_階層が深すぎる_例外を投げる()
    {
        var validator = new OutlineValidator();
        var content = string.Join("\n", new[]
        {
            "- a",
            "  - b",
            "    - c",
            "      - d",
            "- e",
        });

        var ex = Assert.Throws<OutlineConstraintViolationException>(() =>
            validator.ValidateOrThrow(content, DefaultOptions));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("階層"));
    }

    [Test]
    public void ValidateOrThrow_行数超過_例外を投げる()
    {
        var validator = new OutlineValidator();
        var lines = Enumerable.Range(1, 81).Select(i => $"- item {i}");

        var ex = Assert.Throws<OutlineConstraintViolationException>(() =>
            validator.ValidateOrThrow(string.Join("\n", lines), DefaultOptions));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("行数"));
    }

    [Test]
    public void ValidateOrThrow_空_例外を投げる()
    {
        var validator = new OutlineValidator();

        var ex = Assert.Throws<OutlineConstraintViolationException>(() =>
            validator.ValidateOrThrow("   ", DefaultOptions));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("アウトラインを入力してください"));
    }

    [Test]
    public void ValidateOrThrow_生成結果検証時_発生源を保持する()
    {
        var validator = new OutlineValidator();

        var ex = Assert.Throws<OutlineConstraintViolationException>(() =>
            validator.ValidateOrThrow("# invalid\n- a\n- b\n- c\n- d", DefaultOptions, OutlineViolationSource.LlmGenerated));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.SourceKind, Is.EqualTo(OutlineViolationSource.LlmGenerated));
    }
}
