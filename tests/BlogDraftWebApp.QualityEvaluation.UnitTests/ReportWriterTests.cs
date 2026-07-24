namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class ReportWriterTests
{
    [Test]
    public async Task WriteRunAsync_IncludesCodeBlocksAndSupportingCoverage()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var report = new RunReport
            {
                CaseSetVersion = "v1",
                PromptVersion = "test",
                Results =
                [
                    new EvaluationResult
                    {
                        CaseId = "case-1",
                        RagMode = "disabled",
                        Metrics = new OutputMetrics
                        {
                            CharacterCount = 100,
                            HeadingCount = 2,
                            ListItemCount = 3,
                            CodeBlockCount = 4,
                            FocalPointCoverageRate = 0.5,
                            SupportingTopicCoverageRate = 0.25,
                        },
                    },
                ],
                RagDeltas =
                [
                    new RagDelta
                    {
                        CaseId = "case-1",
                        SupportingTopicCoverageRateDelta = 0.25,
                    },
                ],
            };

            await ReportWriter.WriteRunAsync(report, outputDirectory, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "run-report.md"));

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("| Code blocks | Focal coverage | Supporting coverage |"));
                Assert.That(markdown, Does.Contain("| 100 | 2 | 3 | 4 | 50 % | 25 % |"));
                Assert.That(markdown, Does.Contain("| Focal coverage | Supporting coverage | New forbidden candidates |"));
                Assert.That(markdown, Does.Contain("| 0.00% | +25.00% |"));
            });
        }
        finally
        {
            Directory.Delete(outputDirectory, true);
        }
    }

    [Test]
    public async Task WriteComparisonAsync_IncludesSupportingCoverageDelta()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var report = new ComparisonReport
            {
                CaseSetVersion = "v1",
                BaselinePromptVersion = "before",
                CandidatePromptVersion = "after",
                Comparisons =
                [
                    new CaseComparison
                    {
                        CaseId = "case-1",
                        RagMode = "disabled",
                        BaselineStatus = "completed",
                        CandidateStatus = "completed",
                        Delta = new MetricDelta { SupportingTopicCoverageRate = -0.25 },
                    },
                ],
            };

            await ReportWriter.WriteComparisonAsync(report, outputDirectory, CancellationToken.None);
            var markdown = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "comparison-report.md"));

            Assert.Multiple(() =>
            {
                Assert.That(markdown, Does.Contain("| Focal coverage | Supporting coverage | New forbidden candidates |"));
                Assert.That(markdown, Does.Contain("| 0.00% | -25.00% |"));
            });
        }
        finally
        {
            Directory.Delete(outputDirectory, true);
        }
    }

    private static string CreateOutputDirectory() =>
        Path.Combine(Path.GetTempPath(), "BlogDraftWebApp.QualityEvaluation.UnitTests", Guid.NewGuid().ToString("N"));
}
