namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class OutputAnalyzerTests
{
    [Test]
    public void Analyze_MeasuresMarkdownOutsideCodeFencesAndNormalizesCoverage()
    {
        var item = CreateCase();
        var output = """
            # 結果

            ＡＰＩ のタイムアウトを90秒に変更した。

            - 月次集計だけが対象
            1. 通常APIは変更なし

            ```markdown
            ## コード内の見出し
            - コード内の箇条書き
            ```
            """;

        var metrics = OutputAnalyzer.Analyze(item, output);

        Assert.Multiple(() =>
        {
            Assert.That(metrics.HeadingCount, Is.EqualTo(1));
            Assert.That(metrics.ListItemCount, Is.EqualTo(2));
            Assert.That(metrics.CodeBlockCount, Is.EqualTo(1));
            Assert.That(metrics.FocalPointCoverageRate, Is.EqualTo(1));
            Assert.That(metrics.SupportingTopicCoverageRate, Is.EqualTo(1));
            Assert.That(metrics.IsWithinHeadingLimit, Is.True);
        });
    }

    [Test]
    public void Analyze_FlagsForbiddenScopesAndGenericStructureOveruse()
    {
        var item = CreateCase();
        var output = """
            # 概要
            APIの全機能を紹介する。
            ## まとめ
            通常APIも変更した。
            """;

        var metrics = OutputAnalyzer.Analyze(item, output);

        Assert.Multiple(() =>
        {
            Assert.That(metrics.ForbiddenScopeCandidates, Is.EqualTo(new[] { "全機能紹介" }));
            Assert.That(metrics.GenericHeadings, Is.EqualTo(new[] { "概要", "まとめ" }));
            Assert.That(metrics.GenericStructureOveruse, Is.True);
        });
    }

    private static EvaluationCase CreateCase() => new()
    {
        Id = "case",
        Category = "experiment-results",
        Input = "APIのタイムアウトを90秒に変更した。",
        ExpectedFocalPoints = [new ExpectedTopic { Name = "90秒", AnyOf = ["APIのタイムアウトを90秒"] }],
        RequiredSupportingTopics = [new ExpectedTopic { Name = "対象", AnyOf = ["月次集計だけ"] }],
        ForbiddenScopes = [new ForbiddenScope { Name = "全機能紹介", Patterns = ["APIの全機能"] }],
        MaxHeadingCount = 3,
        TargetLengthRange = new LengthRange { Min = 1, Max = 1000 },
    };
}
