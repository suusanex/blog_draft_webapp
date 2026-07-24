namespace BlogDraftWebApp.QualityEvaluation;

public static class ReportComparer
{
    public static ComparisonReport Compare(RunReport baseline, RunReport candidate)
    {
        if (baseline.SchemaVersion != candidate.SchemaVersion)
        {
            throw new InvalidDataException("The reports use different schema versions.");
        }

        if (!string.Equals(baseline.CaseSetVersion, candidate.CaseSetVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The reports use different case-set versions.");
        }

        ValidateUniqueKeys(baseline, "baseline");
        ValidateUniqueKeys(candidate, "candidate");

        var baselineKeys = baseline.Results.Select(Key).ToHashSet(StringComparer.Ordinal);
        var candidateKeys = candidate.Results.Select(Key).ToHashSet(StringComparer.Ordinal);
        if (!baselineKeys.SetEquals(candidateKeys))
        {
            var missing = baselineKeys.Except(candidateKeys, StringComparer.Ordinal);
            var added = candidateKeys.Except(baselineKeys, StringComparer.Ordinal);
            throw new InvalidDataException(
                $"The report case variants do not match. Missing: {string.Join(", ", missing)}; Added: {string.Join(", ", added)}");
        }

        var candidateByKey = candidate.Results.ToDictionary(Key, StringComparer.Ordinal);
        var comparisons = new List<CaseComparison>();
        foreach (var baselineResult in baseline.Results.OrderBy(x => x.CaseId, StringComparer.Ordinal).ThenBy(x => x.RagMode, StringComparer.Ordinal))
        {
            var candidateResult = candidateByKey[Key(baselineResult)];
            MetricDelta? delta = null;
            var newForbidden = new List<string>();
            if (baselineResult.Metrics is not null && candidateResult.Metrics is not null)
            {
                delta = new MetricDelta
                {
                    CharacterCount = candidateResult.Metrics.CharacterCount - baselineResult.Metrics.CharacterCount,
                    HeadingCount = candidateResult.Metrics.HeadingCount - baselineResult.Metrics.HeadingCount,
                    ListItemCount = candidateResult.Metrics.ListItemCount - baselineResult.Metrics.ListItemCount,
                    FocalPointCoverageRate = Math.Round(
                        candidateResult.Metrics.FocalPointCoverageRate - baselineResult.Metrics.FocalPointCoverageRate,
                        4),
                    SupportingTopicCoverageRate = Math.Round(
                        candidateResult.Metrics.SupportingTopicCoverageRate - baselineResult.Metrics.SupportingTopicCoverageRate,
                        4),
                };
                newForbidden = candidateResult.Metrics.ForbiddenScopeCandidates
                    .Except(baselineResult.Metrics.ForbiddenScopeCandidates, StringComparer.Ordinal)
                    .ToList();
            }

            comparisons.Add(new CaseComparison
            {
                CaseId = baselineResult.CaseId,
                RagMode = baselineResult.RagMode,
                BaselineStatus = baselineResult.Status,
                CandidateStatus = candidateResult.Status,
                Delta = delta,
                NewForbiddenScopeCandidates = newForbidden,
            });
        }

        return new ComparisonReport
        {
            CaseSetVersion = baseline.CaseSetVersion,
            BaselinePromptVersion = baseline.PromptVersion,
            CandidatePromptVersion = candidate.PromptVersion,
            ComparedAt = DateTimeOffset.UtcNow,
            Comparisons = comparisons,
        };
    }

    private static void ValidateUniqueKeys(RunReport report, string reportName)
    {
        var duplicates = report.Results
            .GroupBy(Key, StringComparer.Ordinal)
            .Where(x => x.Skip(1).Any())
            .Select(x =>
            {
                var result = x.First();
                return $"({result.CaseId}, {result.RagMode})";
            })
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidDataException(
                $"The {reportName} report contains duplicate case variants: {string.Join(", ", duplicates)}");
        }
    }

    private static string Key(EvaluationResult result) => $"{result.CaseId}\u001f{result.RagMode}";
}
