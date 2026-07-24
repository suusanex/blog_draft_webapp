using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BlogDraftWebApp.QualityEvaluation;

public static partial class OutputAnalyzer
{
    private static readonly HashSet<string> GenericHeadingNames =
    [
        Normalize("概要"),
        Normalize("前提"),
        Normalize("導入手順"),
        Normalize("まとめ"),
        Normalize("はじめに"),
    ];

    public static OutputMetrics Analyze(EvaluationCase item, string output)
    {
        var normalizedOutput = Normalize(output);
        var headings = new List<string>();
        var listItemCount = 0;
        var codeBlockCount = 0;
        var inFence = false;
        char fenceCharacter = default;

        foreach (var line in output.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var fence = FenceRegex().Match(line);
            if (fence.Success)
            {
                var currentFence = fence.Groups[1].Value[0];
                if (!inFence)
                {
                    inFence = true;
                    fenceCharacter = currentFence;
                    codeBlockCount++;
                }
                else if (currentFence == fenceCharacter)
                {
                    inFence = false;
                }

                continue;
            }

            if (inFence)
            {
                continue;
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                headings.Add(heading.Groups[1].Value.Trim().TrimEnd('#').Trim());
            }

            if (ListItemRegex().IsMatch(line))
            {
                listItemCount++;
            }
        }

        var focalCoverage = MeasureCoverage(item.ExpectedFocalPoints, normalizedOutput);
        var supportingCoverage = MeasureCoverage(item.RequiredSupportingTopics, normalizedOutput);
        var forbidden = item.ForbiddenScopes
            .Where(scope => scope.Patterns.Any(pattern => normalizedOutput.Contains(Normalize(pattern), StringComparison.Ordinal)))
            .Select(scope => scope.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var genericHeadings = headings
            .Where(heading => GenericHeadingNames.Contains(Normalize(heading)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var characterCount = output.EnumerateRunes().Count();

        return new OutputMetrics
        {
            CharacterCount = characterCount,
            HeadingCount = headings.Count,
            ListItemCount = listItemCount,
            CodeBlockCount = codeBlockCount,
            FocalPointCoverage = focalCoverage,
            SupportingTopicCoverage = supportingCoverage,
            FocalPointCoverageRate = Rate(focalCoverage),
            SupportingTopicCoverageRate = Rate(supportingCoverage),
            ForbiddenScopeCandidates = forbidden,
            GenericHeadings = genericHeadings,
            GenericStructureOveruse = genericHeadings.Count >= 2,
            IsWithinTargetLength = characterCount >= item.TargetLengthRange.Min && characterCount <= item.TargetLengthRange.Max,
            IsWithinHeadingLimit = headings.Count <= item.MaxHeadingCount,
        };
    }

    private static List<TopicCoverage> MeasureCoverage(IEnumerable<ExpectedTopic> topics, string normalizedOutput) =>
        topics.Select(topic =>
        {
            var match = topic.AnyOf.FirstOrDefault(pattern => normalizedOutput.Contains(Normalize(pattern), StringComparison.Ordinal));
            return new TopicCoverage
            {
                Name = topic.Name,
                Covered = match is not null,
                MatchedPattern = match,
            };
        }).ToList();

    private static double Rate(IReadOnlyCollection<TopicCoverage> coverage) =>
        coverage.Count == 0 ? 1d : Math.Round((double)coverage.Count(x => x.Covered) / coverage.Count, 4);

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^\s*(`{3,}|~{3,})")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.+?)\s*$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]\s+|\d+[.)]\s+)")]
    private static partial Regex ListItemRegex();
}
