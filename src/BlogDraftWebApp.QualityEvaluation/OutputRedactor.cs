using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.QualityEvaluation;

public static class OutputRedactor
{
    public static string Redact(string output, IReadOnlyList<RAGChunk> chunks, StyleCard styleCard)
    {
        var redacted = output;
        var sensitiveValues = chunks
            .SelectMany(x => new[] { x.Text, x.SourceTitle, x.SourceUrl })
            .Append(styleCard.SystemPrompt)
            .Append(styleCard.Content);
        foreach (var sensitiveValue in sensitiveValues)
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue))
            {
                redacted = redacted.Replace(sensitiveValue, "[REDACTED_SENSITIVE_CONTENT]", StringComparison.Ordinal);
            }
        }

        return redacted;
    }
}
