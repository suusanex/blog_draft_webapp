using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BlogDraftWebApp.QualityEvaluation;

public static class ReportWriter
{
    public static async Task WriteRunAsync(RunReport report, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        await WriteJsonAsync(Path.Combine(outputDirectory, "run-report.json"), report, EvaluationJsonContext.Default.RunReport, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "run-report.md"),
            BuildRunMarkdown(report),
            new UTF8Encoding(false),
            cancellationToken);
    }

    public static async Task<RunReport> ReadRunAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.GetFullPath(path));
        return await JsonSerializer.DeserializeAsync(stream, EvaluationJsonContext.Default.RunReport, cancellationToken)
            ?? throw new InvalidDataException($"Run report is empty: {path}");
    }

    public static async Task WriteComparisonAsync(ComparisonReport report, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        await WriteJsonAsync(
            Path.Combine(outputDirectory, "comparison-report.json"),
            report,
            EvaluationJsonContext.Default.ComparisonReport,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "comparison-report.md"),
            BuildComparisonMarkdown(report),
            new UTF8Encoding(false),
            cancellationToken);
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
    }

    private static string BuildRunMarkdown(RunReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Blog draft quality evaluation");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{Escape(report.Status)}`");
        builder.AppendLine($"- Case set: `{Escape(report.CaseSetVersion)}`");
        builder.AppendLine($"- Prompt version: `{Escape(report.PromptVersion)}`");
        builder.AppendLine($"- Mode: `{Escape(report.ExecutionMode)}`");
        builder.AppendLine($"- RAG selection: `{Escape(report.RagSelection)}`");
        builder.AppendLine($"- Model: `{Escape(report.Configuration.Model)}`");
        if (!string.IsNullOrWhiteSpace(report.Message))
        {
            builder.AppendLine($"- Message: {Escape(report.Message)}");
        }

        builder.AppendLine();
        builder.AppendLine("## Results");
        builder.AppendLine();
        builder.AppendLine("| Case | RAG | Status | Chars | Headings | Lists | Focal coverage | Forbidden candidates |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var result in report.Results)
        {
            var metrics = result.Metrics;
            builder.AppendLine(
                $"| {Escape(result.CaseId)} | {Escape(result.RagMode)} | {Escape(result.Status)} | " +
                $"{metrics?.CharacterCount.ToString(CultureInfo.InvariantCulture) ?? "-"} | " +
                $"{metrics?.HeadingCount.ToString(CultureInfo.InvariantCulture) ?? "-"} | " +
                $"{metrics?.ListItemCount.ToString(CultureInfo.InvariantCulture) ?? "-"} | " +
                $"{metrics?.FocalPointCoverageRate.ToString("P0", CultureInfo.InvariantCulture) ?? "-"} | " +
                $"{Escape(metrics is null ? result.Message ?? string.Empty : string.Join(", ", metrics.ForbiddenScopeCandidates))} |");
        }

        if (report.RagDeltas.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## RAG deltas");
            builder.AppendLine();
            builder.AppendLine("| Case | Characters | Headings | Lists | Focal coverage | New forbidden candidates |");
            builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
            foreach (var delta in report.RagDeltas)
            {
                builder.AppendLine(
                    $"| {Escape(delta.CaseId)} | {delta.CharacterCountDelta:+#;-#;0} | {delta.HeadingCountDelta:+#;-#;0} | " +
                    $"{delta.ListItemCountDelta:+#;-#;0} | {delta.FocalPointCoverageRateDelta:+0.00%;-0.00%;0.00%} | " +
                    $"{Escape(string.Join(", ", delta.NewForbiddenScopeCandidates))} |");
            }
        }

        foreach (var result in report.Results.Where(x => x.Status == "completed" && x.GeneratedOutput is not null))
        {
            builder.AppendLine();
            builder.AppendLine($"## {Escape(result.CaseId)} / RAG {Escape(result.RagMode)}");
            builder.AppendLine();
            builder.AppendLine("````markdown");
            builder.AppendLine(result.GeneratedOutput);
            builder.AppendLine("````");
        }

        return builder.ToString();
    }

    private static string BuildComparisonMarkdown(ComparisonReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Blog draft quality comparison");
        builder.AppendLine();
        builder.AppendLine($"- Case set: `{Escape(report.CaseSetVersion)}`");
        builder.AppendLine($"- Baseline: `{Escape(report.BaselinePromptVersion)}`");
        builder.AppendLine($"- Candidate: `{Escape(report.CandidatePromptVersion)}`");
        builder.AppendLine();
        builder.AppendLine("| Case | RAG | Status | Characters | Headings | Lists | Focal coverage | New forbidden candidates |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var comparison in report.Comparisons)
        {
            var delta = comparison.Delta;
            builder.AppendLine(
                $"| {Escape(comparison.CaseId)} | {Escape(comparison.RagMode)} | " +
                $"{Escape(comparison.BaselineStatus)} → {Escape(comparison.CandidateStatus)} | " +
                $"{Format(delta?.CharacterCount)} | {Format(delta?.HeadingCount)} | {Format(delta?.ListItemCount)} | " +
                $"{(delta is null ? "-" : delta.FocalPointCoverageRate.ToString("+0.00%;-0.00%;0.00%", CultureInfo.InvariantCulture))} | " +
                $"{Escape(string.Join(", ", comparison.NewForbiddenScopeCandidates))} |");
        }

        return builder.ToString();
    }

    private static string Format(int? value) => value is null ? "-" : value.Value.ToString("+#;-#;0", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
