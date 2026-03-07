using System.Text;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public sealed class PromptComposer : IPromptComposer
{
    private const int Step1RagTopK = 3;
    private const int Step1MaxChunkLength = 500;

    public Task<Prompt> ComposeAsync(BlogOverview overview, IReadOnlyList<RAGChunk> ragChunks, StyleCard styleCard, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(styleCard);

        overview.Validate();

        var system = BuildSystemMessage(styleCard);
        var rag = BuildRagSection(ragChunks, step: null);

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
        var rag = BuildRagSection(ragChunks, step);
        var userContent = BuildStepUserContent(step, overview, outline, draft);

        var prompt = new Prompt
        {
            SystemMessage = system,
            RagContext = rag,
            UserOverview = userContent,
        };

        return Task.FromResult(prompt);
    }

    public Task<Prompt> ComposeTitleHookAsync(string articleBody, StyleCard styleCard, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(articleBody);
        ArgumentNullException.ThrowIfNull(styleCard);

        var prompt = new Prompt
        {
            SystemMessage = BuildSystemMessage(styleCard),
            RagContext = string.Empty,
            UserOverview = BuildStandaloneTitleHookUserContent(articleBody),
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

    private static string BuildRagSection(IReadOnlyList<RAGChunk> ragChunks, WorkflowStep? step)
    {
        if (ragChunks is null || ragChunks.Count == 0)
        {
            return string.Empty;
        }

        var targetChunks = step == WorkflowStep.Step1_Outline
            ? ragChunks
                .OrderByDescending(chunk => chunk.Score)
                .Take(Step1RagTopK)
                .Select(chunk => new RAGChunk
                {
                    Text = TrimChunkText(chunk.Text),
                    Score = chunk.Score,
                    SourceTitle = chunk.SourceTitle,
                    SourceUrl = chunk.SourceUrl,
                })
                .ToList()
            : ragChunks;

        var sb = new StringBuilder();
        sb.AppendLine("## 過去記事からの関連情報（参考）");
        sb.AppendLine();

        var index = 1;
        foreach (var chunk in targetChunks)
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
            WorkflowStep.Step1_Outline => BuildOutlineUserContent(overview),
            WorkflowStep.Step2_Draft => BuildDraftUserContent(overview, outline),
            WorkflowStep.Step3_TitleHook => BuildTitleHookUserContent(overview, draft),
            _ => $"## 記事の概要\n\n{overview.Content}",
        };
    }

    private static string BuildOutlineUserContent(BlogOverview overview)
    {
        return string.Join("\n", new[]
        {
            "## アウトライン生成（厳格フォーマット）",
            string.Empty,
            "以下の概要から、短く編集しやすいアウトラインのみを生成してください。",
            "記事本文、説明文、注釈ではなく、見出し構造だけを返してください。",
            string.Empty,
            "[出力ルール]",
            "- 出力は `- ` で始まる箇条書きのみ（前置き・後書き・ラベル禁止）",
            "- 最初の文字は `-`、最後の行も箇条書きで終える",
            "- 行数は 5〜15 行。迷ったら 7〜10 行に収める",
            "- 項目が多すぎる場合は、近い内容を統合して行数内に収める",
            "- 階層は最大 2（2スペースインデントで表現）",
            "- 1行は簡潔に（長文説明は禁止）",
            "- 禁止: `#`見出し、段落文、番号付きリスト、コードブロック、引用、補足説明",
            "- `アウトライン:`, `以下の通り`, ``` などの囲みや説明を出力しない",
            string.Empty,
            "[出力例]",
            "- 章タイトル",
            "  - 小項目",
            string.Empty,
            "## 記事の概要",
            string.Empty,
            overview.Content,
        });
    }

    private static string BuildDraftUserContent(BlogOverview overview, string? outline)
    {
        return string.Join("\n", new[]
        {
            "## 下書き生成（アウトライン準拠）",
            string.Empty,
            "以下の情報を使って、記事の下書きをMarkdownで作成してください。",
            "- 利用してよい入力: 記事概要、確定アウトライン、RAGコンテキスト",
            "- 確定アウトラインの章順を維持し、各章に対応する本文を作成すること",
            "- 未確定のアウトラインを想定した補完はしないこと",
            string.Empty,
            "## 記事の概要",
            string.Empty,
            overview.Content,
            string.Empty,
            "## 確定アウトライン",
            string.Empty,
            string.IsNullOrWhiteSpace(outline) ? "(未設定)" : outline,
        });
    }

    private static string BuildTitleHookUserContent(BlogOverview overview, string? draft)
    {
        var draftSection = string.IsNullOrWhiteSpace(draft)
            ? "(未設定)"
            : draft;

        return string.Join("\n", new[]
        {
            "## タイトルと導入部生成",
            string.Empty,
            "以下の概要と下書きから、タイトル案と導入部を生成してください。",
            string.Empty,
            "## 記事の概要",
            string.Empty,
            overview.Content,
            string.Empty,
            "## 確定下書き",
            string.Empty,
            draftSection,
        });
    }

    private static string BuildStandaloneTitleHookUserContent(string articleBody)
    {
        return string.Join("\n", new[]
        {
            "## タイトル案と冒頭段落案の生成",
            string.Empty,
            "以下の完成済み本文だけを入力として、3案を作成してください。",
            string.Empty,
            "[出力ルール]",
            "- 3案を必ず出力する",
            "- 各案は次の形式: 1行目がタイトル、2行目以降が冒頭段落",
            "- 案と案の区切りは `---` のみを使う",
            "- 前置き・解説・注釈は出力しない",
            string.Empty,
            "## 入力本文",
            string.Empty,
            articleBody,
        });
    }

    private static string TrimChunkText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim();
        if (normalized.Length <= Step1MaxChunkLength)
        {
            return normalized;
        }

        return normalized[..Step1MaxChunkLength] + "...";
    }
}

