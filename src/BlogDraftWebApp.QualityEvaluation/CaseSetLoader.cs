using System.Text.Json;

namespace BlogDraftWebApp.QualityEvaluation;

public static class CaseSetLoader
{
    private static readonly HashSet<string> RequiredCategories =
    [
        "experiment-results",
        "troubleshooting",
        "comparison-decision",
        "very-short-input",
        "supporting-context-needed",
        "rag-broad-match-risk",
    ];

    public static async Task<EvaluationCaseSet> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var caseSet = await JsonSerializer.DeserializeAsync(
            stream,
            EvaluationJsonContext.Default.EvaluationCaseSet,
            cancellationToken) ?? throw new InvalidDataException("The evaluation case set is empty.");
        Validate(caseSet);
        return caseSet;
    }

    public static void Validate(EvaluationCaseSet caseSet)
    {
        if (caseSet.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported case-set schema version: {caseSet.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(caseSet.Version))
        {
            throw new InvalidDataException("The case-set version is required.");
        }

        if (caseSet.Cases.Count < 10)
        {
            throw new InvalidDataException("At least 10 evaluation cases are required.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in caseSet.Cases)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
            {
                throw new InvalidDataException($"Case IDs must be non-empty and unique: {item.Id}");
            }

            if (string.IsNullOrWhiteSpace(item.Category) || string.IsNullOrWhiteSpace(item.Input))
            {
                throw new InvalidDataException($"Case {item.Id} requires category and input.");
            }

            ValidateTopics(item.Id, "expectedFocalPoints", item.ExpectedFocalPoints);
            ValidateTopics(item.Id, "requiredSupportingTopics", item.RequiredSupportingTopics);

            if (item.ForbiddenScopes.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Patterns.Count == 0 || x.Patterns.Any(string.IsNullOrWhiteSpace)))
            {
                throw new InvalidDataException($"Case {item.Id} has an invalid forbidden scope.");
            }

            if (item.MaxHeadingCount <= 0
                || item.TargetLengthRange.Min < 0
                || item.TargetLengthRange.Max < item.TargetLengthRange.Min)
            {
                throw new InvalidDataException($"Case {item.Id} has invalid length or heading limits.");
            }
        }

        var missing = RequiredCategories.Except(caseSet.Cases.Select(x => x.Category), StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"Required categories are missing: {string.Join(", ", missing)}");
        }
    }

    private static void ValidateTopics(string caseId, string field, IReadOnlyList<ExpectedTopic> topics)
    {
        if (topics.Count == 0 || topics.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.AnyOf.Count == 0 || x.AnyOf.Any(string.IsNullOrWhiteSpace)))
        {
            throw new InvalidDataException($"Case {caseId} has invalid {field}.");
        }
    }
}
