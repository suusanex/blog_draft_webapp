using System.Text;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public sealed class PromptComposer : IPromptComposer
{
    public Task<Prompt> ComposeAsync(BlogOverview overview, IReadOnlyList<RAGChunk> ragChunks, StyleCard styleCard, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(styleCard);

        overview.Validate();

        var system = BuildSystemMessage(styleCard);
        var rag = BuildRagSection(ragChunks);

        var prompt = new Prompt
        {
            SystemMessage = system,
            RagContext = rag,
            UserOverview = $"## 新規記事の概要\n\n{overview.Content}",
        };

        return Task.FromResult(prompt);
    }

    private static string BuildSystemMessage(StyleCard styleCard)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[システムプロンプト]");
        sb.AppendLine(styleCard.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("[文体カード]");
        sb.AppendLine(styleCard.Content);
        return sb.ToString().Trim();
    }

    private static string BuildRagSection(IReadOnlyList<RAGChunk> ragChunks)
    {
        if (ragChunks is null || ragChunks.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("## 過去記事からの関連情報（参考）");
        sb.AppendLine();

        var index = 1;
        foreach (var chunk in ragChunks)
        {
            var title = string.IsNullOrWhiteSpace(chunk.SourceTitle) ? "(no title)" : chunk.SourceTitle;
            var link = !string.IsNullOrWhiteSpace(chunk.SourceUrl)
                ? $"[{title}]({chunk.SourceUrl})"
                : title;

            sb.AppendLine($"{index}. {link}");
            sb.AppendLine($"> {chunk.Text}");
            sb.AppendLine();
            index++;
        }

        return sb.ToString().Trim();
    }
}
