namespace BlogDraftWebApp.Core.Models;

public sealed class Prompt
{
    public string SystemMessage { get; init; } = string.Empty;
    public string RagContext { get; init; } = string.Empty;
    public string UserOverview { get; init; } = string.Empty;
    public StructuredOutputDefinition? StructuredOutput { get; init; }

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

public sealed class StructuredOutputDefinition
{
    public string Name { get; init; } = string.Empty;
    public string SchemaJson { get; init; } = "{}";
    public bool Strict { get; init; } = true;
}
