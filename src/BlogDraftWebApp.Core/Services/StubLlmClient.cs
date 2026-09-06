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
        if (prompt.Kind is PromptKind.WorkflowOutline or PromptKind.OutlineRepair)
        {
            return """
                {
                  "outline": "- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論",
                  "editorialMemo": {
                    "meaningElements": [{ "source": "入力の要点", "role": "主張" }],
                    "logicalRelations": [],
                    "articleQuestion": "テスト",
                    "readerAssumption": "基本操作を知る実務者",
                    "scopeBySection": [],
                    "openQuestions": []
                  }
                }
                """;
        }

        if (prompt.Kind is PromptKind.WorkflowTitleHook)
        {
            return string.Join("\n---\n", new[]
            {
                "タイトル案A\n冒頭段落A",
                "タイトル案B\n冒頭段落B",
                "タイトル案C\n冒頭段落C",
            });
        }

        var draftBody = "# テスト下書き\n\nこれは E2E テスト用のスタブ出力です。\n\n" + new string('あ', 150);
        return "{\n  \"draft\": " + System.Text.Json.JsonSerializer.Serialize(draftBody) + ",\n  \"openQuestions\": []\n}";
    }
}
