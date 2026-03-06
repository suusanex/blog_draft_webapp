using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Services;

public sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private const int MinDraftLength = 100;
    private const int MaxDraftLength = 50000;

    private readonly IWorkflowRepository _repository;
    private readonly IRetrievalService _retrievalService;
    private readonly IPromptComposer _promptComposer;
    private readonly ILlmClient _llmClient;
    private readonly OutlineValidator _outlineValidator;
    private readonly StyleCard _styleCard;
    private readonly WorkflowSessionLock _sessionLock;
    private readonly WorkflowOptions _options;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(
        IWorkflowRepository repository,
        IRetrievalService retrievalService,
        IPromptComposer promptComposer,
        ILlmClient llmClient,
        OutlineValidator outlineValidator,
        StyleCard styleCard,
        WorkflowSessionLock sessionLock,
        IOptions<WorkflowOptions> options,
        ILogger<WorkflowOrchestrator> logger)
    {
        _repository = repository;
        _retrievalService = retrievalService;
        _promptComposer = promptComposer;
        _llmClient = llmClient;
        _outlineValidator = outlineValidator;
        _styleCard = styleCard;
        _sessionLock = sessionLock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WorkflowSession> CreateSessionAsync(string overview, CancellationToken cancellationToken)
    {
        var blogOverview = new BlogOverview(overview);
        blogOverview.Validate();

        var now = DateTimeOffset.UtcNow;
        var session = new WorkflowSession
        {
            SessionId = Guid.NewGuid().ToString(),
            CurrentStep = WorkflowStep.Step1_Outline,
            CreatedAt = now,
            LastAccessedAt = now,
            DeleteAt = now.AddDays(_options.SessionRetentionDays),
            InitialInput = blogOverview,
        };

        await _repository.CreateSessionAsync(session, cancellationToken);
        _logger.LogInformation("Workflow session created. SessionId={SessionId}", session.SessionId);
        return session;
    }

    public async Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        session.Touch(_options.SessionRetentionDays);
        await _repository.UpdateSessionAsync(session, cancellationToken);
        return session;
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        await _repository.DeleteSessionAsync(sessionId, cancellationToken);
        _logger.LogInformation("Workflow session deleted. SessionId={SessionId}", sessionId);
    }

    public async Task<IReadOnlyList<WorkflowSession>> ListSessionsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = page <= 0 ? 1 : page;
        var safeSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var skip = (safePage - 1) * safeSize;
        var sessions = await _repository.ListSessionsAsync(skip, safeSize, cancellationToken);
        return sessions;
    }

    public async Task<WorkflowRefreshResult> RefreshRagSnapshotAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var handle = await TryAcquireAsync(sessionId, cancellationToken);

        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        var previousSnapshotId = session.RagSnapshotId ?? string.Empty;
        var previousSnapshot = string.IsNullOrWhiteSpace(session.RagSnapshotId)
            ? null
            : await _repository.GetSnapshotAsync(session.RagSnapshotId, cancellationToken);

        var retrieval = await _retrievalService.RetrieveAsync(session.InitialInput.Content, cancellationToken);
        var newSnapshot = new RagSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString(),
            SearchQuery = session.InitialInput.Content,
            SearchExecutedAt = DateTimeOffset.UtcNow,
            Chunks = retrieval.Chunks.ToList(),
            ChunkCount = retrieval.Chunks.Count,
        };

        await _repository.UpsertSnapshotAsync(newSnapshot, cancellationToken);

        var diff = ComputeRagSnapshotDiff(previousSnapshot?.Chunks ?? new List<RAGChunk>(), newSnapshot.Chunks);
        session.PendingRagSnapshotId = newSnapshot.SnapshotId;
        session.PendingRagSnapshotDiff = diff;
        session.Touch(_options.SessionRetentionDays);
        await _repository.UpdateSessionAsync(session, cancellationToken);

        _logger.LogInformation("RAG snapshot refreshed. SessionId={SessionId} PreviousSnapshotId={PreviousSnapshotId} NewSnapshotId={NewSnapshotId}", sessionId, previousSnapshotId, newSnapshot.SnapshotId);

        return new WorkflowRefreshResult(previousSnapshotId, newSnapshot.SnapshotId, diff, Confirmed: false);
    }

    public async Task<WorkflowRefreshResult> ConfirmRagSnapshotRefreshAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var handle = await TryAcquireAsync(sessionId, cancellationToken);

        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(session.PendingRagSnapshotId) || session.PendingRagSnapshotDiff is null)
        {
            throw new InvalidStateTransitionException("RAG再検索の確定対象がありません。");
        }

        var previousSnapshotId = session.RagSnapshotId ?? string.Empty;
        var newSnapshotId = session.PendingRagSnapshotId;
        var diff = session.PendingRagSnapshotDiff;

        session.RagSnapshotId = newSnapshotId;
        session.PendingRagSnapshotId = null;
        session.PendingRagSnapshotDiff = null;
        session.Touch(_options.SessionRetentionDays);
        await _repository.UpdateSessionAsync(session, cancellationToken);

        _logger.LogInformation("RAG snapshot refresh confirmed. SessionId={SessionId} PreviousSnapshotId={PreviousSnapshotId} NewSnapshotId={NewSnapshotId}", sessionId, previousSnapshotId, newSnapshotId);

        return new WorkflowRefreshResult(previousSnapshotId, newSnapshotId, diff, Confirmed: true);
    }

    public async Task<WorkflowGenerateResult> GenerateStepAsync(string sessionId, WorkflowStep step, bool regenerate, CancellationToken cancellationToken)
    {
        using var handle = await TryAcquireAsync(sessionId, cancellationToken);

        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        if (!session.CanGenerateStep(step))
        {
            throw new InvalidStateTransitionException($"Cannot generate step {step} from {session.CurrentStep}.");
        }

        var (snapshot, warning) = await ResolveSnapshotAsync(session, step, regenerate, cancellationToken);

        var prompt = await _promptComposer.ComposeAsync(
            step,
            session.InitialInput,
            snapshot.Chunks,
            _styleCard,
            GetOutlineForPrompt(session, step),
            GetDraftForPrompt(session, step),
            cancellationToken);

        var draft = await _llmClient.GenerateAsync(
            prompt,
            cancellationToken,
            step == WorkflowStep.Step1_Outline ? _options.OutlineMaxOutputTokens : null);

        if (step == WorkflowStep.Step1_Outline)
        {
            _outlineValidator.ValidateOrThrow(draft.Content, _options);
        }

        var content = ApplyGeneratedContent(session, step, draft.Content);
        session.Touch(_options.SessionRetentionDays);
        await _repository.UpdateSessionAsync(session, cancellationToken);

        warning = content.Trim().Length < 50
            ? "生成結果が短すぎます。入力内容を詳しくするか、設定を確認してください"
            : warning;

        _logger.LogInformation("Workflow step generated. SessionId={SessionId} Step={Step}", sessionId, step);

        return new WorkflowGenerateResult(
            content,
            snapshot.ChunkCount,
            draft.Model,
            draft.GeneratedAt,
            warning);
    }

    public async Task<WorkflowPreviewResult> PreviewStepAsync(string sessionId, WorkflowStep step, CancellationToken cancellationToken)
    {
        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        if (!session.CanGenerateStep(step))
        {
            throw new InvalidStateTransitionException($"Cannot preview step {step} from {session.CurrentStep}.");
        }

        var (snapshot, warning) = await ResolveSnapshotAsync(session, step, regenerate: false, cancellationToken);

        var prompt = await _promptComposer.ComposeAsync(
            step,
            session.InitialInput,
            snapshot.Chunks,
            _styleCard,
            GetOutlineForPrompt(session, step),
            GetDraftForPrompt(session, step),
            cancellationToken);

        _logger.LogInformation("Workflow prompt preview generated. SessionId={SessionId} Step={Step}", sessionId, step);

        return new WorkflowPreviewResult(prompt.FullPrompt, snapshot.ChunkCount, warning, snapshot.SnapshotId);
    }

    public async Task SaveStepAsync(string sessionId, WorkflowStep step, string editedContent, CancellationToken cancellationToken)
    {
        using var handle = await TryAcquireAsync(sessionId, cancellationToken);

        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        ApplyEditedContent(session, step, editedContent);
        session.Touch(_options.SessionRetentionDays);
        await _repository.UpdateSessionAsync(session, cancellationToken);

        _logger.LogInformation("Workflow step saved. SessionId={SessionId} Step={Step}", sessionId, step);
    }

    public async Task<WorkflowConfirmResult> ConfirmStepAsync(string sessionId, WorkflowStep step, string confirmedContent, CancellationToken cancellationToken)
    {
        using var handle = await TryAcquireAsync(sessionId, cancellationToken);

        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        var nextStep = ApplyConfirmedContent(session, step, confirmedContent);
        await _repository.UpdateSessionAsync(session, cancellationToken);

        _logger.LogInformation("Workflow step confirmed. SessionId={SessionId} Step={Step}", sessionId, step);

        return new WorkflowConfirmResult(step, nextStep);
    }

    private async Task<(RagSnapshot Snapshot, string? Warning)> ResolveSnapshotAsync(
        WorkflowSession session,
        WorkflowStep step,
        bool regenerate,
        CancellationToken cancellationToken)
    {
        if (step == WorkflowStep.Step1_Outline && (regenerate || string.IsNullOrWhiteSpace(session.RagSnapshotId)))
        {
            var retrieval = await _retrievalService.RetrieveAsync(session.InitialInput.Content, cancellationToken);
            var snapshot = new RagSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString(),
                SearchQuery = session.InitialInput.Content,
                SearchExecutedAt = DateTimeOffset.UtcNow,
                Chunks = retrieval.Chunks.ToList(),
                ChunkCount = retrieval.Chunks.Count,
            };

            await _repository.UpsertSnapshotAsync(snapshot, cancellationToken);
            session.RagSnapshotId = snapshot.SnapshotId;
            return (snapshot, retrieval.Warning);
        }

        if (!string.IsNullOrWhiteSpace(session.RagSnapshotId))
        {
            var snapshot = await _repository.GetSnapshotAsync(session.RagSnapshotId, cancellationToken);
            return (snapshot, null);
        }

        throw new InvalidStateTransitionException("RAG snapshot is not available for this step.");
    }

    private string ApplyGeneratedContent(WorkflowSession session, WorkflowStep step, string content)
    {
        switch (step)
        {
            case WorkflowStep.Step1_Outline:
                session.OutlineGenerated = content;
                return content;
            case WorkflowStep.Step2_Draft:
                session.DraftGenerated = content;
                return content;
            case WorkflowStep.Step3_TitleHook:
                var options = ParseTitleHookOptions(content);
                session.TitleHookOptions = options;
                session.TitleHookSelected = options.FirstOrDefault();
                return FormatTitleHookOptions(options);
            default:
                throw new InvalidStateTransitionException($"Step {step} is not available yet.");
        }
    }

    private void ApplyEditedContent(WorkflowSession session, WorkflowStep step, string editedContent)
    {
        switch (step)
        {
            case WorkflowStep.Step1_Outline:
                session.OutlineEdited = editedContent;
                return;
            case WorkflowStep.Step2_Draft:
                session.DraftEdited = editedContent;
                return;
            case WorkflowStep.Step3_TitleHook:
                var selected = ParseTitleHook(editedContent, optionIndex: null);
                selected.Validate();
                session.TitleHookSelected = selected;
                return;
            default:
                throw new InvalidStateTransitionException($"Step {step} is not available yet.");
        }
    }

    private WorkflowStep? ApplyConfirmedContent(WorkflowSession session, WorkflowStep step, string confirmedContent)
    {
        if (!session.CanConfirmStep(step))
        {
            throw new InvalidStateTransitionException($"Cannot confirm step {step} from {session.CurrentStep}.");
        }

        switch (step)
        {
            case WorkflowStep.Step1_Outline:
                _outlineValidator.ValidateOrThrow(confirmedContent, _options);
                session.OutlineConfirmed = confirmedContent;
                session.TransitionToStep(WorkflowStep.Step2_Draft, _options.SessionRetentionDays);
                return WorkflowStep.Step2_Draft;
            case WorkflowStep.Step2_Draft:
                ValidateDraft(confirmedContent);
                session.DraftConfirmed = confirmedContent;
                session.CurrentStep = WorkflowStep.Completed;
                session.Touch(_options.SessionRetentionDays);
                return WorkflowStep.Completed;
            case WorkflowStep.Step3_TitleHook:
                var confirmed = string.IsNullOrWhiteSpace(confirmedContent)
                    ? session.TitleHookSelected ?? session.TitleHookOptions?.FirstOrDefault() ?? throw new InvalidStateTransitionException("TitleHook option is not selected.")
                    : ParseTitleHook(confirmedContent, optionIndex: null);

                confirmed.Validate();
                session.TitleHookConfirmed = confirmed;
                session.CurrentStep = WorkflowStep.Completed;
                session.Touch(_options.SessionRetentionDays);
                return WorkflowStep.Completed;
            default:
                throw new InvalidStateTransitionException($"Step {step} is not available yet.");
        }
    }
    private static string? GetOutlineForPrompt(WorkflowSession session, WorkflowStep step)
    {
        return step == WorkflowStep.Step2_Draft
            ? session.OutlineConfirmed
            : null;
    }

    private static string? GetDraftForPrompt(WorkflowSession session, WorkflowStep step)
    {
        return step == WorkflowStep.Step3_TitleHook
            ? session.DraftConfirmed
            : null;
    }

    private static void ValidateDraft(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("下書きを入力してください", nameof(content));
        }

        if (content.Length < MinDraftLength)
        {
            throw new ArgumentException($"下書きは {MinDraftLength} 文字以上入力してください", nameof(content));
        }

        if (content.Length > MaxDraftLength)
        {
            throw new ArgumentException($"下書きは {MaxDraftLength} 文字以内で入力してください", nameof(content));
        }
    }

    private static List<TitleHook> ParseTitleHookOptions(string content)
    {
        var sections = content.Split("\n---\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new List<TitleHook>();

        for (var i = 0; i < sections.Length && i < 3; i++)
        {
            var parsed = ParseTitleHook(sections[i], i + 1);
            options.Add(parsed);
        }

        if (options.Count == 0)
        {
            options.Add(ParseTitleHook(content, 1));
        }

        while (options.Count < 3)
        {
            var template = options[0];
            options.Add(new TitleHook
            {
                Title = $"{template.Title} ({options.Count + 1})",
                HookText = template.HookText,
                OptionIndex = options.Count + 1,
            });
        }

        return options;
    }

    private static TitleHook ParseTitleHook(string content, int? optionIndex)
    {
        var normalized = content.Replace("\r\n", "\n").Trim();
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            throw new ArgumentException("タイトル案が空です", nameof(content));
        }

        var title = lines[0].TrimStart('#', '-', '*', ' ').Trim();
        var hook = lines.Length > 1
            ? string.Join(Environment.NewLine, lines.Skip(1)).Trim()
            : "導入文を追加してください。";

        return new TitleHook
        {
            Title = title,
            HookText = hook,
            OptionIndex = optionIndex,
        };
    }

    private static string FormatTitleHookOptions(IEnumerable<TitleHook> options)
    {
        return string.Join(
            Environment.NewLine + "---" + Environment.NewLine,
            options.Select(x => $"# {x.Title}{Environment.NewLine}{x.HookText}"));
    }

    private static RagSnapshotDiff ComputeRagSnapshotDiff(IReadOnlyList<RAGChunk> previous, IReadOnlyList<RAGChunk> current)
    {
        var previousKeys = BuildChunkKeys(previous);
        var currentKeys = BuildChunkKeys(current);

        var added = currentKeys.Except(previousKeys, StringComparer.OrdinalIgnoreCase).Count();
        var removed = previousKeys.Except(currentKeys, StringComparer.OrdinalIgnoreCase).Count();
        var denominator = Math.Max(previousKeys.Count, 1);
        var rate = (added + removed) / (double)denominator;

        return new RagSnapshotDiff
        {
            AddedChunkCount = added,
            RemovedChunkCount = removed,
            ChangeRate = Math.Round(rate, 4),
        };
    }

    private static HashSet<string> BuildChunkKeys(IReadOnlyList<RAGChunk> chunks)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in chunks)
        {
            var key = !string.IsNullOrWhiteSpace(chunk.SourceUrl)
                ? chunk.SourceUrl
                : $"{chunk.SourceTitle}:{chunk.Text}";

            keys.Add(key);
        }

        return keys;
    }

    private async Task<WorkflowSession> GetSessionOrThrowAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(sessionId, cancellationToken);

        if (session.DeleteAt <= DateTimeOffset.UtcNow)
        {
            await _repository.DeleteSessionAsync(sessionId, cancellationToken);
            throw new SessionNotFoundException(sessionId, isExpired: true);
        }

        return session;
    }

    private async Task<IDisposable> TryAcquireAsync(string sessionId, CancellationToken cancellationToken)
    {
        var handle = await _sessionLock.TryAcquireAsync(sessionId, cancellationToken);
        if (handle is null)
        {
            throw new SessionBusyException(sessionId);
        }

        return handle;
    }
}


