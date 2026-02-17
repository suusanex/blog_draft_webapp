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

        var content = "# テスト下書き\n\n" +
                      "これは E2E テスト用のスタブ出力です。\n\n" +
                      "- 目的: 画面フローの検証\n" +
                      "- 入力: " + prompt.UserOverview + "\n\n" +
                      new string('あ', 150);

        return Task.FromResult(new Draft
        {
            Content = content,
            Model = "stub-llm",
            GeneratedAt = DateTimeOffset.UtcNow,
            TokensUsed = 0,
        });
    }
}
