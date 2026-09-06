namespace BlogDraftWebApp.Core.Models;

public enum PromptKind
{
    Unknown = 0,
    OneShotDraft,
    WorkflowOutline,
    WorkflowDraft,
    WorkflowTitleHook,
    OutlineRepair,
}

public sealed class Prompt
{
    public PromptKind Kind { get; init; }
    public string SystemMessage { get; init; } = string.Empty;
    public string RagContext { get; init; } = string.Empty;
    public string UserOverview { get; init; } = string.Empty;

    public string FullPrompt
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(SystemMessage))
            {
                parts.Add(SystemMessage.Trim());
            }

            if (!string.IsNullOrWhiteSpace(RagContext))
            {
                parts.Add(RagContext.Trim());
            }

            if (!string.IsNullOrWhiteSpace(UserOverview))
            {
                parts.Add(UserOverview.Trim());
            }

            return string.Join("\n\n", parts);
        }
    }
}
