using System.Text.Json.Serialization;

namespace BlogDraftWebApp.QualityEvaluation;

public sealed class EvaluationCaseSet
{
    public int SchemaVersion { get; init; }
    public string Version { get; init; } = string.Empty;
    public List<EvaluationCase> Cases { get; init; } = [];
}

public sealed class EvaluationCase
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public List<ExpectedTopic> ExpectedFocalPoints { get; init; } = [];
    public List<ExpectedTopic> RequiredSupportingTopics { get; init; } = [];
    public List<ForbiddenScope> ForbiddenScopes { get; init; } = [];
    public int MaxHeadingCount { get; init; }
    public LengthRange TargetLengthRange { get; init; } = new();
}

public sealed class ExpectedTopic
{
    public string Name { get; init; } = string.Empty;
    public List<string> AnyOf { get; init; } = [];
}

public sealed class ForbiddenScope
{
    public string Name { get; init; } = string.Empty;
    public List<string> Patterns { get; init; } = [];
}

public sealed class LengthRange
{
    public int Min { get; init; }
    public int Max { get; init; }
}

public sealed class TopicCoverage
{
    public string Name { get; init; } = string.Empty;
    public bool Covered { get; init; }
    public string? MatchedPattern { get; init; }
}

public sealed class OutputMetrics
{
    public int CharacterCount { get; init; }
    public int HeadingCount { get; init; }
    public int ListItemCount { get; init; }
    public int CodeBlockCount { get; init; }
    public List<TopicCoverage> FocalPointCoverage { get; init; } = [];
    public List<TopicCoverage> SupportingTopicCoverage { get; init; } = [];
    public double FocalPointCoverageRate { get; init; }
    public double SupportingTopicCoverageRate { get; init; }
    public List<string> ForbiddenScopeCandidates { get; init; } = [];
    public List<string> GenericHeadings { get; init; } = [];
    public bool GenericStructureOveruse { get; init; }
    public bool IsWithinTargetLength { get; init; }
    public bool IsWithinHeadingLimit { get; init; }
}

public sealed class SafeRunConfiguration
{
    public string Model { get; init; } = string.Empty;
    public int MaxTokens { get; init; }
    public Dictionary<string, string> ModelParameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int RagTopK { get; init; }
    public double RagMinimumScore { get; init; }
}

public sealed class EvaluationResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string RagMode { get; init; } = string.Empty;
    public string Status { get; init; } = "completed";
    public string? Message { get; init; }
    public string Model { get; init; } = string.Empty;
    public int RagHitCount { get; init; }
    public List<string> SourceIds { get; init; } = [];
    public string? GeneratedOutput { get; init; }
    public OutputMetrics? Metrics { get; init; }
}

public sealed class RagDelta
{
    public string CaseId { get; init; } = string.Empty;
    public int CharacterCountDelta { get; init; }
    public int HeadingCountDelta { get; init; }
    public int ListItemCountDelta { get; init; }
    public double FocalPointCoverageRateDelta { get; init; }
    public double SupportingTopicCoverageRateDelta { get; init; }
    public List<string> NewForbiddenScopeCandidates { get; init; } = [];
}

public sealed class RunReport
{
    public int SchemaVersion { get; init; } = 1;
    public string CaseSetVersion { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public string ExecutionMode { get; init; } = string.Empty;
    public string RagSelection { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public string Status { get; set; } = "completed";
    public string? Message { get; set; }
    public SafeRunConfiguration Configuration { get; set; } = new();
    public List<EvaluationResult> Results { get; init; } = [];
    public List<RagDelta> RagDeltas { get; init; } = [];
}

public sealed class MetricDelta
{
    public int CharacterCount { get; init; }
    public int HeadingCount { get; init; }
    public int ListItemCount { get; init; }
    public double FocalPointCoverageRate { get; init; }
    public double SupportingTopicCoverageRate { get; init; }
}

public sealed class CaseComparison
{
    public string CaseId { get; init; } = string.Empty;
    public string RagMode { get; init; } = string.Empty;
    public string BaselineStatus { get; init; } = string.Empty;
    public string CandidateStatus { get; init; } = string.Empty;
    public MetricDelta? Delta { get; init; }
    public List<string> NewForbiddenScopeCandidates { get; init; } = [];
}

public sealed class ComparisonReport
{
    public int SchemaVersion { get; init; } = 1;
    public string CaseSetVersion { get; init; } = string.Empty;
    public string BaselinePromptVersion { get; init; } = string.Empty;
    public string CandidatePromptVersion { get; init; } = string.Empty;
    public DateTimeOffset ComparedAt { get; init; }
    public List<CaseComparison> Comparisons { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(EvaluationCaseSet))]
[JsonSerializable(typeof(RunReport))]
[JsonSerializable(typeof(ComparisonReport))]
public partial class EvaluationJsonContext : JsonSerializerContext;
