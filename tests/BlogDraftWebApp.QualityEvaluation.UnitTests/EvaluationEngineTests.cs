using System.Text.Json;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class EvaluationEngineTests
{
    [Test]
    public async Task StubMode_EvaluatesAllRagVariantsWithoutSensitiveRagDataInReport()
    {
        var root = RepositoryPaths.FindRoot();
        var caseSet = await CaseSetLoader.LoadAsync(Path.Combine(root, "evaluation", "cases.json"), CancellationToken.None);
        var engine = new EvaluationEngine(root);

        var report = await engine.RunAsync(
            caseSet,
            new RunOptions { Mode = "stub", PromptVersion = "test", Rag = "both" },
            CancellationToken.None);

        var json = JsonSerializer.Serialize(report, EvaluationJsonContext.Default.RunReport);
        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo("completed"));
            Assert.That(report.Results, Has.Count.EqualTo(24));
            Assert.That(report.Results, Has.All.Property(nameof(EvaluationResult.Status)).EqualTo("completed"));
            Assert.That(report.RagDeltas, Has.Count.EqualTo(12));
            Assert.That(json, Does.Not.Contain("example.invalid"));
            Assert.That(json, Does.Not.Contain("架空の過去記事断片"));
            Assert.That(json, Does.Not.Contain("fixture-experiment"));
        });
    }

    [Test]
    public async Task StubMode_CapturesGenerationFailuresAndMarksReportPartial()
    {
        var root = RepositoryPaths.FindRoot();
        var caseSet = await CaseSetLoader.LoadAsync(Path.Combine(root, "evaluation", "cases.json"), CancellationToken.None);
        var engine = new EvaluationEngine(root, new FailingLlmClient());

        var report = await engine.RunAsync(
            caseSet,
            new RunOptions { Mode = "stub", PromptVersion = "failure-test", Rag = "disabled" },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo("partial"));
            Assert.That(report.Results, Has.All.Property(nameof(EvaluationResult.Status)).EqualTo("failed"));
            Assert.That(report.Results, Has.All.Property(nameof(EvaluationResult.Message)).EqualTo("Generation or retrieval failed (InvalidOperationException)."));
        });
    }

    private sealed class FailingLlmClient : ILlmClient
    {
        public Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null) =>
            throw new InvalidOperationException("sensitive upstream details");
    }
}
