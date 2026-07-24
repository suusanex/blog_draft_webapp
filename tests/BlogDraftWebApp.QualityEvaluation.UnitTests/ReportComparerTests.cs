namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class ReportComparerTests
{
    [Test]
    public void Compare_ComputesCandidateMinusBaseline()
    {
        var baseline = Report("before", "case-1", Metrics(100, 2, 1, 0.5, 0.75, "existing"));
        var candidate = Report("after", "case-1", Metrics(80, 1, 3, 1, 0.5, "existing", "new"));

        var comparison = ReportComparer.Compare(baseline, candidate);
        var result = comparison.Comparisons.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Delta!.CharacterCount, Is.EqualTo(-20));
            Assert.That(result.Delta.HeadingCount, Is.EqualTo(-1));
            Assert.That(result.Delta.ListItemCount, Is.EqualTo(2));
            Assert.That(result.Delta.FocalPointCoverageRate, Is.EqualTo(0.5));
            Assert.That(result.Delta.SupportingTopicCoverageRate, Is.EqualTo(-0.25));
            Assert.That(result.NewForbiddenScopeCandidates, Is.EqualTo(new[] { "new" }));
        });
    }

    [Test]
    public void Compare_RejectsMismatchedCaseVariants()
    {
        var baseline = Report("before", "case-1", Metrics(1, 1, 1, 1, 1));
        var candidate = Report("after", "case-2", Metrics(1, 1, 1, 1, 1));

        Assert.That(
            () => ReportComparer.Compare(baseline, candidate),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("do not match"));
    }

    [Test]
    public void Compare_RejectsDuplicateBaselineCaseVariants()
    {
        var baseline = Report("before", "case-1", Metrics(1, 1, 1, 1, 1));
        baseline.Results.Add(baseline.Results.Single());
        var candidate = Report("after", "case-1", Metrics(1, 1, 1, 1, 1));

        Assert.That(
            () => ReportComparer.Compare(baseline, candidate),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("baseline")
                .And.Message.Contains("(case-1, disabled)"));
    }

    [Test]
    public void Compare_RejectsDuplicateCandidateCaseVariants()
    {
        var baseline = Report("before", "case-1", Metrics(1, 1, 1, 1, 1));
        var candidate = Report("after", "case-1", Metrics(1, 1, 1, 1, 1));
        candidate.Results.Add(candidate.Results.Single());

        Assert.That(
            () => ReportComparer.Compare(baseline, candidate),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("candidate")
                .And.Message.Contains("(case-1, disabled)"));
    }

    private static RunReport Report(string promptVersion, string caseId, OutputMetrics metrics) => new()
    {
        CaseSetVersion = "v1",
        PromptVersion = promptVersion,
        Results =
        [
            new EvaluationResult
            {
                CaseId = caseId,
                RagMode = "disabled",
                Status = "completed",
                Metrics = metrics,
            },
        ],
    };

    private static OutputMetrics Metrics(
        int characters,
        int headings,
        int lists,
        double focalCoverage,
        double supportingCoverage,
        params string[] forbidden) => new()
        {
            CharacterCount = characters,
            HeadingCount = headings,
            ListItemCount = lists,
            FocalPointCoverageRate = focalCoverage,
            SupportingTopicCoverageRate = supportingCoverage,
            ForbiddenScopeCandidates = forbidden.ToList(),
        };
}
