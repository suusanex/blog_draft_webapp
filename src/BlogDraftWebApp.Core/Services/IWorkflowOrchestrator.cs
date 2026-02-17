using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public interface IWorkflowOrchestrator
{
    Task<WorkflowSession> CreateSessionAsync(string overview, CancellationToken cancellationToken);
    Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowSession>> ListSessionsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<WorkflowRefreshResult> RefreshRagSnapshotAsync(string sessionId, CancellationToken cancellationToken);
    Task<WorkflowRefreshResult> ConfirmRagSnapshotRefreshAsync(string sessionId, CancellationToken cancellationToken);
    Task<WorkflowGenerateResult> GenerateStepAsync(string sessionId, WorkflowStep step, bool regenerate, CancellationToken cancellationToken);
    Task<WorkflowPreviewResult> PreviewStepAsync(string sessionId, WorkflowStep step, CancellationToken cancellationToken);
    Task SaveStepAsync(string sessionId, WorkflowStep step, string editedContent, CancellationToken cancellationToken);
    Task<WorkflowConfirmResult> ConfirmStepAsync(string sessionId, WorkflowStep step, string confirmedContent, CancellationToken cancellationToken);
}

public sealed record WorkflowGenerateResult(
    string Content,
    int RagHitCount,
    string Model,
    DateTimeOffset GeneratedAt,
    string? Warning);

public sealed record WorkflowPreviewResult(
    string Prompt,
    int RagHitCount,
    string? Warning,
    string? RagSnapshotId);

public sealed record WorkflowRefreshResult(
    string PreviousSnapshotId,
    string NewSnapshotId,
    RagSnapshotDiff Diff,
    bool Confirmed);

public sealed record WorkflowConfirmResult(
    WorkflowStep Step,
    WorkflowStep? NextStep);
