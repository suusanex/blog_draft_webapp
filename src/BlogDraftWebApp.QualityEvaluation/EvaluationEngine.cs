using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.QualityEvaluation;

public sealed class EvaluationEngine
{
    private readonly string repositoryRoot;
    private readonly ILlmClient stubClient;

    public EvaluationEngine(string repositoryRoot, ILlmClient? stubClient = null)
    {
        this.repositoryRoot = repositoryRoot;
        this.stubClient = stubClient ?? DefaultStubClient;
    }

    public async Task<RunReport> RunAsync(EvaluationCaseSet caseSet, RunOptions options, CancellationToken cancellationToken)
    {
        var report = new RunReport
        {
            CaseSetVersion = caseSet.Version,
            PromptVersion = options.PromptVersion,
            ExecutionMode = options.Mode,
            RagSelection = options.Rag,
            StartedAt = DateTimeOffset.UtcNow,
        };

        LiveDependencies? live = null;
        try
        {
            if (options.Mode == "live")
            {
                live = LiveConfiguration.Load(repositoryRoot, options.ConfigurationArguments, out var skipReason);
                if (live is null)
                {
                    report.Status = "skipped";
                    report.Message = skipReason;
                    return report;
                }

                report.Configuration = CreateSafeConfiguration(live.LlmOptions, live.RagOptions);
            }
            else
            {
                report.Configuration = new SafeRunConfiguration
                {
                    Model = "quality-evaluation-stub",
                    MaxTokens = 0,
                    RagTopK = 1,
                    RagMinimumScore = 0,
                };
            }

            var modes = options.Rag switch
            {
                "enabled" => new[] { "enabled" },
                "disabled" => new[] { "disabled" },
                _ => new[] { "disabled", "enabled" },
            };

            var promptComposer = new PromptComposer();
            foreach (var item in caseSet.Cases)
            {
                foreach (var ragMode in modes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await EvaluateCaseAsync(item, ragMode, options.Mode, promptComposer, stubClient, live, cancellationToken);
                    report.Results.Add(result);
                }

                AddRagDelta(report, item.Id);
            }

            if (report.Results.Any(x => x.Status == "failed"))
            {
                report.Status = "partial";
                report.Message = "One or more case variants failed. Completed variants remain available for review.";
            }

            return report;
        }
        finally
        {
            live?.Dispose();
        }
    }

    private static async Task<EvaluationResult> EvaluateCaseAsync(
        EvaluationCase item,
        string ragMode,
        string executionMode,
        IPromptComposer promptComposer,
        ILlmClient stubClient,
        LiveDependencies? live,
        CancellationToken cancellationToken)
    {
        if (executionMode == "live" && ragMode == "enabled" && live?.RetrievalService is null)
        {
            return new EvaluationResult
            {
                CaseId = item.Id,
                Category = item.Category,
                RagMode = ragMode,
                Status = "skipped",
                Message = live?.RagSkipReason ?? "RAG configuration is unavailable.",
                Model = live?.LlmOptions.Model ?? string.Empty,
            };
        }

        try
        {
            var chunks = await GetChunksAsync(item, ragMode, executionMode, live, cancellationToken);
            var styleCard = live?.StyleCard ?? StubStyleCard;
            var prompt = await promptComposer.ComposeAsync(new BlogOverview(item.Input), chunks, styleCard, cancellationToken);
            var llm = live?.LlmClient ?? stubClient;
            var draft = await llm.GenerateAsync(prompt, cancellationToken);
            var safeOutput = OutputRedactor.Redact(draft.Content, chunks, styleCard);

            return new EvaluationResult
            {
                CaseId = item.Id,
                Category = item.Category,
                RagMode = ragMode,
                Model = draft.Model,
                RagHitCount = chunks.Count,
                SourceIds = chunks.Select(SourceIdentity.Create).Distinct(StringComparer.Ordinal).ToList(),
                GeneratedOutput = safeOutput,
                Metrics = OutputAnalyzer.Analyze(item, draft.Content),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new EvaluationResult
            {
                CaseId = item.Id,
                Category = item.Category,
                RagMode = ragMode,
                Status = "failed",
                Message = $"Generation or retrieval failed ({ex.GetType().Name}).",
                Model = live?.LlmOptions.Model ?? "quality-evaluation-stub",
            };
        }
    }

    private static async Task<IReadOnlyList<RAGChunk>> GetChunksAsync(
        EvaluationCase item,
        string ragMode,
        string executionMode,
        LiveDependencies? live,
        CancellationToken cancellationToken)
    {
        if (ragMode == "disabled")
        {
            return Array.Empty<RAGChunk>();
        }

        if (executionMode == "stub")
        {
            return
            [
                new RAGChunk
                {
                    Text = "これは公開可能な評価用の架空の過去記事断片です。",
                    Score = 1,
                    SourceTitle = $"fixture-{item.Id}",
                    SourceUrl = $"https://example.invalid/evaluation/{item.Id}",
                },
            ];
        }

        var retrieval = await live!.RetrievalService!.RetrieveAsync(item.Input, cancellationToken);
        return retrieval.Chunks;
    }

    private static SafeRunConfiguration CreateSafeConfiguration(LlmOptions llm, RagOptions rag)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in llm.Parameters)
        {
            parameters[key] = IsSensitiveParameter(key) ? "[REDACTED]" : value;
        }

        return new SafeRunConfiguration
        {
            Model = llm.Model,
            MaxTokens = llm.MaxTokens,
            ModelParameters = parameters,
            RagTopK = rag.TopK,
            RagMinimumScore = rag.MinimumScore,
        };
    }

    private static bool IsSensitiveParameter(string key) =>
        key.Contains("api", StringComparison.OrdinalIgnoreCase)
        || key.Contains("key", StringComparison.OrdinalIgnoreCase)
        || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("authorization", StringComparison.OrdinalIgnoreCase)
        || key.Contains("prompt", StringComparison.OrdinalIgnoreCase)
        || key.Contains("stylecard", StringComparison.OrdinalIgnoreCase);

    private static void AddRagDelta(RunReport report, string caseId)
    {
        var disabled = report.Results.SingleOrDefault(x => x.CaseId == caseId && x.RagMode == "disabled" && x.Status == "completed");
        var enabled = report.Results.SingleOrDefault(x => x.CaseId == caseId && x.RagMode == "enabled" && x.Status == "completed");
        if (disabled?.Metrics is null || enabled?.Metrics is null)
        {
            return;
        }

        report.RagDeltas.Add(new RagDelta
        {
            CaseId = caseId,
            CharacterCountDelta = enabled.Metrics.CharacterCount - disabled.Metrics.CharacterCount,
            HeadingCountDelta = enabled.Metrics.HeadingCount - disabled.Metrics.HeadingCount,
            ListItemCountDelta = enabled.Metrics.ListItemCount - disabled.Metrics.ListItemCount,
            FocalPointCoverageRateDelta = Math.Round(enabled.Metrics.FocalPointCoverageRate - disabled.Metrics.FocalPointCoverageRate, 4),
            SupportingTopicCoverageRateDelta = Math.Round(
                enabled.Metrics.SupportingTopicCoverageRate - disabled.Metrics.SupportingTopicCoverageRate,
                4),
            NewForbiddenScopeCandidates = enabled.Metrics.ForbiddenScopeCandidates
                .Except(disabled.Metrics.ForbiddenScopeCandidates, StringComparer.Ordinal)
                .ToList(),
        });
    }

    private static readonly StyleCard StubStyleCard = new()
    {
        Title = "Evaluation fixture",
        SystemPrompt = "固定評価用の架空のシステム指示です。Markdownで簡潔に出力してください。",
        Content = "固定評価用の架空の文体カードです。",
    };

    private static readonly ILlmClient DefaultStubClient = new DeterministicEvaluationStubLlmClient();

    private sealed class DeterministicEvaluationStubLlmClient : ILlmClient
    {
        public Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ragNote = string.IsNullOrWhiteSpace(prompt.RagContext)
                ? string.Empty
                : "\n\n## 参考情報\n\n架空の作例を参照しました。";
            return Task.FromResult(new Draft
            {
                Content = $"# 固定評価用下書き\n\n{prompt.UserOverview}{ragNote}",
                Model = "quality-evaluation-stub",
                GeneratedAt = DateTimeOffset.UnixEpoch,
                TokensUsed = 0,
            });
        }
    }
}
