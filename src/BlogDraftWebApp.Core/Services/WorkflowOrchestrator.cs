using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Services;

public sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private const int MaxDraftLength = 50000;
    private const string BrokenOutputWarning = "生成結果が空、または壊れている可能性があります";

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

        var prompt = await _promptComposer.ComposeAsync(
            step,
            session.InitialInput,
            _styleCard,
            GetOutlineForPrompt(session, step),
            GetDraftForPrompt(session, step),
            GetEditorialMemoForPrompt(session, step),
            cancellationToken);

        string content;
        string model;
        DateTimeOffset generatedAt;
        IReadOnlyList<string> openQuestions = Array.Empty<string>();
        string? editorialMemoJson = null;

        if (step == WorkflowStep.Step1_Outline)
        {
            var outline = await GenerateOutlineWithRecoveryAsync(session.InitialInput, prompt, cancellationToken);
            content = ApplyGeneratedContent(session, step, outline.Content);
            session.EditorialMemoJson = outline.EditorialMemoJson;
            editorialMemoJson = session.EditorialMemoJson;
            openQuestions = outline.OpenQuestions;
            model = outline.Model;
            generatedAt = outline.GeneratedAt;
        }
        else
        {
            var draft = await _llmClient.GenerateAsync(prompt, cancellationToken, null);
            var parsed = GeneratedContentParser.ParseDraft(draft.Content);
            if (!parsed.IsValid)
            {
                var initialDraft = draft;
                draft = await _llmClient.GenerateAsync(
                    _promptComposer.ComposeDraftRepair(
                        session.InitialInput,
                        _styleCard,
                        draft.Content,
                        parsed.ErrorMessage ?? "下書き生成結果を解釈できませんでした。",
                        session.OutlineConfirmed,
                        session.EditorialMemoJson),
                    cancellationToken,
                    null);
                parsed = GeneratedContentParser.ParseDraft(draft.Content);
                if (!parsed.IsValid
                    && GeneratedContentParser.TryRecoverMarkdownDraft(draft.Content, out var recovered))
                {
                    parsed = recovered;
                }
                else if (!parsed.IsValid
                    && GeneratedContentParser.TryRecoverMarkdownDraft(initialDraft.Content, out recovered))
                {
                    draft = initialDraft;
                    parsed = recovered;
                }
            }

            EnsureValidDraftGeneration(parsed);
            content = ApplyGeneratedContent(session, step, parsed.Draft);
            session.OpenQuestions = parsed.OpenQuestions.ToList();
            openQuestions = session.OpenQuestions;
            model = draft.Model;
            generatedAt = draft.GeneratedAt;
        }

        session.Touch(_options.SessionRetentionDays);
        await _repository.UpdateSessionAsync(session, cancellationToken);

        var warning = string.IsNullOrWhiteSpace(content) ? BrokenOutputWarning : null;

        _logger.LogInformation("Workflow step generated. SessionId={SessionId} Step={Step}", sessionId, step);

        return new WorkflowGenerateResult(
            content,
            RagHitCount: 0,
            model,
            generatedAt,
            warning,
            openQuestions,
            editorialMemoJson);
    }

    public async Task<WorkflowPreviewResult> PreviewStepAsync(string sessionId, WorkflowStep step, CancellationToken cancellationToken)
    {
        var session = await GetSessionOrThrowAsync(sessionId, cancellationToken);
        if (!session.CanGenerateStep(step))
        {
            throw new InvalidStateTransitionException($"Cannot preview step {step} from {session.CurrentStep}.");
        }

        var prompt = await _promptComposer.ComposeAsync(
            step,
            session.InitialInput,
            _styleCard,
            GetOutlineForPrompt(session, step),
            GetDraftForPrompt(session, step),
            GetEditorialMemoForPrompt(session, step),
            cancellationToken);

        _logger.LogInformation("Workflow prompt preview generated. SessionId={SessionId} Step={Step}", sessionId, step);

        return new WorkflowPreviewResult(prompt.FullPrompt, RagHitCount: 0, Warning: null, RagSnapshotId: null);
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
                _outlineValidator.ValidateOrThrow(confirmedContent, _options, OutlineViolationSource.UserInput);
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

    private static string? GetEditorialMemoForPrompt(WorkflowSession session, WorkflowStep step)
    {
        return step == WorkflowStep.Step2_Draft
            ? session.EditorialMemoJson
            : null;
    }

    private async Task<OutlineGenerationOutcome> GenerateOutlineWithRecoveryAsync(
        BlogOverview overview,
        Prompt prompt,
        CancellationToken cancellationToken)
    {
        var maxTokens = _options.OutlineMaxOutputTokens is > 0
            ? _options.OutlineMaxOutputTokens
            : null;

        var generated = await _llmClient.GenerateAsync(prompt, cancellationToken, maxTokens);
        var parsed = GeneratedContentParser.ParseOutline(generated.Content);
        var normalized = NormalizeGeneratedOutline(parsed.Outline);
        string? validationMessage = null;

        if (!parsed.IsMalformedJson && TryValidateGeneratedOutline(normalized, out validationMessage))
        {
            return ToOutlineOutcome(generated, normalized, parsed);
        }

        var initialValidationMessage = parsed.IsMalformedJson
            ? "アウトライン生成結果のJSONが壊れています。"
            : validationMessage ?? "アウトラインの形式が不正です。";

        _logger.LogWarning("Generated outline failed validation. Attempting repair. Reason={Reason}", initialValidationMessage);

        var repairSource = generated;
        var repairMessage = initialValidationMessage;
        const int maxFormatRepairAttempts = 2;
        for (var attempt = 0; attempt < maxFormatRepairAttempts; attempt++)
        {
            var repaired = await _llmClient.GenerateAsync(
                _promptComposer.ComposeOutlineRepair(
                    overview,
                    _styleCard,
                    repairSource.Content,
                    repairMessage,
                    _options),
                cancellationToken,
                maxTokens);

            var repairedParsed = GeneratedContentParser.ParseOutline(repaired.Content);
            var normalizedRepaired = NormalizeGeneratedOutline(repairedParsed.Outline);
            if (!repairedParsed.IsMalformedJson)
            {
                _outlineValidator.ValidateOrThrow(normalizedRepaired, _options, OutlineViolationSource.LlmGenerated);
                return ToOutlineOutcome(repaired, normalizedRepaired, repairedParsed);
            }

            repairSource = repaired;
            repairMessage = "アウトライン再整形結果のJSONも壊れています。JSON形式だけを修復してください。";
        }

        throw new LlmException(
            "LLM_OUTPUT_INVALID",
            "アウトライン生成結果のJSONを解釈できませんでした。",
            isRetryable: true);
    }

    private static OutlineGenerationOutcome ToOutlineOutcome(Draft source, string outline, OutlineParseResult parsed)
    {
        var memoJson = GeneratedContentParser.SerializeEditorialMemo(parsed.EditorialMemo);
        var openQuestions = parsed.EditorialMemo?.OpenQuestions
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList()
            ?? new List<string>();

        return new OutlineGenerationOutcome(
            outline,
            source.Model,
            source.GeneratedAt,
            memoJson,
            openQuestions);
    }

    private sealed record OutlineGenerationOutcome(
        string Content,
        string Model,
        DateTimeOffset GeneratedAt,
        string? EditorialMemoJson,
        IReadOnlyList<string> OpenQuestions);

    private bool TryValidateGeneratedOutline(string content, out string? validationMessage)
    {
        try
        {
            _outlineValidator.ValidateOrThrow(content, _options, OutlineViolationSource.LlmGenerated);
            validationMessage = null;
            return true;
        }
        catch (OutlineConstraintViolationException ex)
        {
            validationMessage = ex.Message;
            return false;
        }
    }

    private string NormalizeGeneratedOutline(string content)
    {
        var normalized = (content ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var normalizedLines = new List<string>();
        var hasUnrecognizedContent = false;
        foreach (var rawLine in normalized.Split('\n'))
        {
            if (IsKnownOutlineFormattingLine(rawLine))
            {
                continue;
            }

            if (TryNormalizeOutlineLine(rawLine, out var normalizedLine))
            {
                normalizedLines.Add(normalizedLine!);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(rawLine))
            {
                hasUnrecognizedContent = true;
            }
        }

        return !hasUnrecognizedContent && normalizedLines.Count >= _options.OutlineMinLines
            ? string.Join("\n", normalizedLines)
            : normalized;
    }

    private static bool IsKnownOutlineFormattingLine(string rawLine)
    {
        var trimmed = rawLine.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal)
            || string.Equals(trimmed, "---", StringComparison.Ordinal))
        {
            return true;
        }

        return trimmed.StartsWith("以下のアウトライン", StringComparison.Ordinal)
            || trimmed.StartsWith("アウトライン:", StringComparison.Ordinal)
            || trimmed.StartsWith("アウトライン：", StringComparison.Ordinal);
    }

    private bool TryNormalizeOutlineLine(string rawLine, out string? normalizedLine)
    {
        normalizedLine = null;

        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        var unquoted = rawLine.Replace("\t", "  ", StringComparison.Ordinal);
        var trimmed = unquoted.TrimStart();
        while (trimmed.StartsWith(">", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].TrimStart();
        }

        if (string.IsNullOrWhiteSpace(trimmed)
            || trimmed.StartsWith("```", StringComparison.Ordinal)
            || string.Equals(trimmed, "---", StringComparison.Ordinal))
        {
            return false;
        }

        var leadingSpaces = CountLeadingSpaces(unquoted);
        var depth = Math.Min(leadingSpaces / 2, _options.OutlineMaxDepth);
        var indent = new string(' ', depth * 2);

        string? text = null;
        if (trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            text = trimmed[2..].Trim();
        }
        else if (trimmed.Length > 1 && trimmed[0] == '-' && !char.IsWhiteSpace(trimmed[1]))
        {
            text = trimmed[1..].Trim();
        }
        else if (trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            text = trimmed[2..].Trim();
        }
        else if (LooksLikeNumberedList(trimmed, out var numberedText))
        {
            text = numberedText;
        }
        else if (LooksLikeHeading(trimmed, out var headingText))
        {
            text = headingText;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith("[ ] ", StringComparison.Ordinal) || text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].Trim();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        normalizedLine = indent + "- " + text;
        return true;
    }

    private static bool LooksLikeNumberedList(string text, out string? content)
    {
        content = null;
        var index = 0;
        while (index < text.Length && char.IsDigit(text[index]))
        {
            index++;
        }

        if (index == 0 || index + 1 >= text.Length)
        {
            return false;
        }

        if ((text[index] != '.' && text[index] != ')') || !char.IsWhiteSpace(text[index + 1]))
        {
            return false;
        }

        content = text[(index + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(content);
    }

    private static bool LooksLikeHeading(string text, out string? content)
    {
        content = null;
        if (!text.StartsWith('#'))
        {
            return false;
        }

        var trimmed = text.TrimStart('#', ' ').Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        content = trimmed;
        return true;
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                count++;
                continue;
            }

            break;
        }

        return count;
    }
    private static void ValidateDraft(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("下書きを入力してください", nameof(content));
        }

        if (content.Length > MaxDraftLength)
        {
            throw new ArgumentException($"下書きは {MaxDraftLength} 文字以内で入力してください", nameof(content));
        }
    }

    private static void EnsureValidDraftGeneration(DraftParseResult parsed)
    {
        if (parsed.IsValid)
        {
            return;
        }

        throw new LlmException(
            "LLM_OUTPUT_INVALID",
            parsed.ErrorMessage ?? "下書き生成結果を解釈できませんでした。",
            isRetryable: true);
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

