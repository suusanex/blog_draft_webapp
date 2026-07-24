namespace BlogDraftWebApp.QualityEvaluation;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var verbose = CommandLine.HasVerbose(args);
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine(CommandLine.Usage);
                return 2;
            }

            var repositoryRoot = RepositoryPaths.FindRoot();
            return args[0].ToLowerInvariant() switch
            {
                "run" => await RunAsync(repositoryRoot, CommandLine.ParseRun(args[1..])),
                "compare" => await CompareAsync(repositoryRoot, CommandLine.ParseCompare(args[1..])),
                "--help" or "-h" or "help" => ShowHelp(),
                _ => throw new ArgumentException($"Unknown command: {args[0]}"),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(verbose ? ex.ToString() : ex.Message);
            return 2;
        }
    }

    private static async Task<int> RunAsync(string repositoryRoot, RunOptions options)
    {
        var casesPath = Path.GetFullPath(options.CasesPath ?? Path.Combine(repositoryRoot, "evaluation", "cases.json"));
        var caseSet = await CaseSetLoader.LoadAsync(casesPath, CancellationToken.None);
        var outputDirectory = RepositoryPaths.ResolveOutputDirectory(repositoryRoot, options.OutputDirectory);

        var engine = new EvaluationEngine(repositoryRoot);
        var report = await engine.RunAsync(caseSet, options, CancellationToken.None);
        await ReportWriter.WriteRunAsync(report, outputDirectory, CancellationToken.None);

        Console.WriteLine($"Evaluation status: {report.Status}");
        if (!string.IsNullOrWhiteSpace(report.Message))
        {
            Console.WriteLine(report.Message);
        }

        Console.WriteLine($"Reports: {outputDirectory}");
        return report.Status is "failed" or "partial" ? 1 : 0;
    }

    private static async Task<int> CompareAsync(string repositoryRoot, CompareOptions options)
    {
        var baseline = await ReportWriter.ReadRunAsync(options.BaselinePath, CancellationToken.None);
        var candidate = await ReportWriter.ReadRunAsync(options.CandidatePath, CancellationToken.None);
        var comparison = ReportComparer.Compare(baseline, candidate);
        var outputDirectory = RepositoryPaths.ResolveOutputDirectory(repositoryRoot, options.OutputDirectory, "comparison");
        await ReportWriter.WriteComparisonAsync(comparison, outputDirectory, CancellationToken.None);
        Console.WriteLine($"Comparison reports: {outputDirectory}");
        return 0;
    }

    private static int ShowHelp()
    {
        Console.WriteLine(CommandLine.Usage);
        return 0;
    }
}

public static class RepositoryPaths
{
    public static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BlogDraftWebApp.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing BlogDraftWebApp.sln.");
    }

    public static string ResolveOutputDirectory(string repositoryRoot, string? requested, string suffix = "run")
    {
        var path = requested ?? Path.Combine(
            repositoryRoot,
            "artifacts",
            "quality-evaluation",
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{suffix}");
        return Path.GetFullPath(path);
    }
}
