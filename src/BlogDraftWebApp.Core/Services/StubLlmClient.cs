using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

/// <summary>
/// E2E 向けの LLM スタブ実装。
/// 実外部呼び出しを行わず、安定した応答を返す。
/// </summary>
public sealed class StubLlmClient : ILlmClient
{
    public Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = BuildContent(prompt);

        return Task.FromResult(new Draft
        {
            Content = content,
            Model = "stub-llm",
            GeneratedAt = DateTimeOffset.UtcNow,
            TokensUsed = 0,
        });
    }

    private static string BuildContent(Prompt prompt)
    {
        if (prompt.UserOverview.Contains("アウトライン再整形", StringComparison.Ordinal)
            || prompt.UserOverview.Contains("アウトライン生成（厳格フォーマット）", StringComparison.Ordinal))
        {
            return string.Join("\n", new[]
            {
                "- 背景",
                "  - 課題",
                "- 目的",
                "  - 対象読者",
                "- 結論",
            });
        }

        if (prompt.UserOverview.Contains("タイトル案と冒頭段落案の生成", StringComparison.Ordinal))
        {
            return string.Join("\n---\n", new[]
            {
                "タイトル案A\n冒頭段落A",
                "タイトル案B\n冒頭段落B",
                "タイトル案C\n冒頭段落C",
            });
        }

        return "# テスト下書き\n\nこれは E2E テスト用のスタブ出力です。\n\n" + new string('あ', 150);
    }
}
