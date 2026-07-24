namespace BlogDraftWebApp.QualityEvaluation;

public sealed class RunOptions
{
    public string Mode { get; init; } = "stub";
    public string PromptVersion { get; init; } = string.Empty;
    public string Rag { get; init; } = "both";
    public string? CasesPath { get; init; }
    public string? OutputDirectory { get; init; }
    public List<string> ConfigurationArguments { get; init; } = [];
}

public sealed class CompareOptions
{
    public string BaselinePath { get; init; } = string.Empty;
    public string CandidatePath { get; init; } = string.Empty;
    public string? OutputDirectory { get; init; }
}

public static class CommandLine
{
    public static RunOptions ParseRun(string[] args)
    {
        var values = ParseKnown(args, ["--mode", "--prompt-version", "--rag", "--cases", "--output"], out var remaining);
        var mode = Get(values, "--mode", "stub").ToLowerInvariant();
        var rag = Get(values, "--rag", "both").ToLowerInvariant();
        var promptVersion = Get(values, "--prompt-version", string.Empty);

        if (mode is not ("stub" or "live"))
        {
            throw new ArgumentException("--mode must be stub or live.");
        }

        if (rag is not ("both" or "enabled" or "disabled"))
        {
            throw new ArgumentException("--rag must be both, enabled, or disabled.");
        }

        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            throw new ArgumentException("--prompt-version is required.");
        }

        return new RunOptions
        {
            Mode = mode,
            Rag = rag,
            PromptVersion = promptVersion,
            CasesPath = GetOptional(values, "--cases"),
            OutputDirectory = GetOptional(values, "--output"),
            ConfigurationArguments = remaining,
        };
    }

    public static CompareOptions ParseCompare(string[] args)
    {
        var values = ParseKnown(args, ["--baseline", "--candidate", "--output"], out var remaining);
        if (remaining.Count > 0)
        {
            throw new ArgumentException($"Unknown argument: {remaining[0]}");
        }

        var baseline = Get(values, "--baseline", string.Empty);
        var candidate = Get(values, "--candidate", string.Empty);
        if (string.IsNullOrWhiteSpace(baseline) || string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("--baseline and --candidate are required.");
        }

        return new CompareOptions
        {
            BaselinePath = baseline,
            CandidatePath = candidate,
            OutputDirectory = GetOptional(values, "--output"),
        };
    }

    public static string Usage => """
        Usage:
          BlogDraftWebApp.QualityEvaluation run --mode stub|live --prompt-version <label> [--rag both|enabled|disabled] [--cases <path>] [--output <directory>] [configuration arguments]
          BlogDraftWebApp.QualityEvaluation compare --baseline <report.json> --candidate <report.json> [--output <directory>]
        """;

    private static Dictionary<string, string> ParseKnown(
        string[] args,
        HashSet<string> knownOptions,
        out List<string> remaining)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        remaining = [];

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!knownOptions.Contains(arg))
            {
                remaining.Add(arg);
                continue;
            }

            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"A value is required for {arg}.");
            }

            values[arg] = args[i];
        }

        return values;
    }

    private static string Get(Dictionary<string, string> values, string key, string defaultValue) =>
        values.TryGetValue(key, out var value) ? value : defaultValue;

    private static string? GetOptional(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;
}
