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

    public Task<Prompt> ComposeAsync(
        WorkflowStep step,
        BlogOverview overview,
        IReadOnlyList<RAGChunk> ragChunks,
        StyleCard styleCard,
        string? outline,
        string? draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(styleCard);

        overview.Validate();

        var system = BuildSystemMessage(styleCard);
        var rag = BuildRagSection(ragChunks);
        var userContent = BuildStepUserContent(step, overview, outline, draft);

        var prompt = new Prompt
        {
            SystemMessage = system,
            RagContext = rag,
            UserOverview = userContent,
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

    private static string BuildStepUserContent(WorkflowStep step, BlogOverview overview, string? outline, string? draft)
    {
        return step switch
        {
            WorkflowStep.Step1_Outline => $"## アウトライン生成\n\n以下の概要から、章立てと箇条書きのアウトラインを生成してください。\n\n## 記事の概要\n\n{overview.Content}",
            WorkflowStep.Step2_Draft => BuildDraftUserContent(overview, outline),
            WorkflowStep.Step3_TitleHook => BuildTitleHookUserContent(overview, draft),
            _ => $"## 記事の概要\n\n{overview.Content}",
        };
    }

    private static string BuildDraftUserContent(BlogOverview overview, string? outline)
    {
        var outlineSection = string.IsNullOrWhiteSpace(outline)
            ? ""
            : $"\n\n## 確定アウトライン\n\n{outline}";

        return $"## 下書き生成\n\n以下の概要とアウトラインに沿って下書きを生成してください。\n\n## 記事の概要\n\n{overview.Content}{outlineSection}";
    }

    private static string BuildTitleHookUserContent(BlogOverview overview, string? draft)
    {
        var draftSection = string.IsNullOrWhiteSpace(draft)
            ? ""
            : $"\n\n## 確定下書き\n\n{draft}";

        return $"## タイトルと導入部生成\n\n以下の概要と下書きから、タイトル案と導入部を生成してください。\n\n## 記事の概要\n\n{overview.Content}{draftSection}";
    }
}
