using BlogDraftWebApp.Core.Exceptions;
using LiteDB;

namespace BlogDraftWebApp.Core.Models;

public sealed class WorkflowSession
{
    [BsonId]
    public string SessionId { get; init; } = string.Empty;
    public WorkflowStep CurrentStep { get; set; } = WorkflowStep.Step1_Outline;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DeleteAt { get; set; } = DateTimeOffset.UtcNow;
    public BlogOverview InitialInput { get; init; } = new();
    public string? RagSnapshotId { get; set; }
    public string? PendingRagSnapshotId { get; set; }
    public RagSnapshotDiff? PendingRagSnapshotDiff { get; set; }
    public string? OutlineGenerated { get; set; }
    public string? OutlineEdited { get; set; }
    public string? OutlineConfirmed { get; set; }
    public string? DraftGenerated { get; set; }
    public string? DraftEdited { get; set; }
    public string? DraftConfirmed { get; set; }
    public List<TitleHook>? TitleHookOptions { get; set; }
    public TitleHook? TitleHookSelected { get; set; }
    public TitleHook? TitleHookConfirmed { get; set; }

    public bool CanGenerateStep(WorkflowStep step)
    {
        return step switch
        {
            WorkflowStep.Step1_Outline => true,
            WorkflowStep.Step2_Draft => !string.IsNullOrWhiteSpace(OutlineConfirmed),
            WorkflowStep.Step3_TitleHook => !string.IsNullOrWhiteSpace(DraftConfirmed),
            _ => false,
        };
    }

    public bool CanConfirmStep(WorkflowStep step)
    {
        return step switch
        {
            WorkflowStep.Step1_Outline => !string.IsNullOrWhiteSpace(OutlineEdited) || !string.IsNullOrWhiteSpace(OutlineGenerated),
            WorkflowStep.Step2_Draft => !string.IsNullOrWhiteSpace(DraftEdited) || !string.IsNullOrWhiteSpace(DraftGenerated),
            WorkflowStep.Step3_TitleHook => TitleHookSelected is not null || (TitleHookOptions?.Count > 0),
            _ => false,
        };
    }

    public void TransitionToStep(WorkflowStep newStep, int retentionDays)
    {
        if (!CanGenerateStep(newStep))
        {
            throw new InvalidStateTransitionException($"Cannot transition to {newStep} from {CurrentStep}.");
        }

        CurrentStep = newStep;
        Touch(retentionDays);
    }

    public void Touch(int retentionDays)
    {
        LastAccessedAt = DateTimeOffset.UtcNow;
        DeleteAt = LastAccessedAt.AddDays(retentionDays);
    }
}
