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

        if (prompt.UserOverview.Contains("[EDITORIAL_PLAN_JSON]", StringComparison.Ordinal))
        {
            if (prompt.StructuredOutput?.Name == "editorial_sections")
            {
                var sections = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sections = new[]
                    {
                        new PlannedSection
                        {
                            Id = "section-1",
                            Heading = "入力の中心ポイント",
                            Purpose = "入力内容を伝える",
                            SourceItemIds = ["focus-1"],
                        },
                    },
                });

                return Task.FromResult(new Draft
                {
                    Content = sections,
                    Model = "stub-llm",
                    GeneratedAt = DateTimeOffset.UtcNow,
                    TokensUsed = 0,
                });
            }

            var input = ExtractInput(prompt.UserOverview);
            var excerpt = input.Length > 80 ? input[..80] : input;
            var plan = System.Text.Json.JsonSerializer.Serialize(new EditorialPlan
            {
                Thesis = new BriefItem
                {
                    Id = "thesis-1",
                    Text = "入力の中心ポイント",
                    Origin = BriefItemOrigin.Input,
                    SourceExcerpt = excerpt,
                },
                FocalPoints =
                [
                    new BriefItem
                    {
                        Id = "focus-1",
                        Text = "入力された中心ポイント",
                        Origin = BriefItemOrigin.Input,
                        SourceExcerpt = excerpt,
                    },
                ],
                Sections =
                [
                    new PlannedSection
                    {
                        Id = "section-1",
                        Heading = "入力の中心ポイント",
                        Purpose = "入力内容を伝える",
                        SourceItemIds = ["focus-1"],
                    },
                ],
            });

            return Task.FromResult(new Draft
            {
                Content = plan,
                Model = "stub-llm",
                GeneratedAt = DateTimeOffset.UtcNow,
                TokensUsed = 0,
            });
        }

        var content = BuildContent(prompt);

        return Task.FromResult(new Draft
        {
            Content = content,
            Model = "stub-llm",
            GeneratedAt = DateTimeOffset.UtcNow,
            TokensUsed = 0,
        });
    }

    private static string ExtractInput(string prompt)
    {
        const string start = "<free_input>";
        const string end = "</free_input>";
        var startIndex = prompt.IndexOf(start, StringComparison.Ordinal);
        var endIndex = prompt.IndexOf(end, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
        {
            return "0123456789";
        }

        return prompt[(startIndex + start.Length)..endIndex].Trim();
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

        return "# テスト下書き\n\n" +
               "これは E2E テスト用のスタブ出力です。\n\n" +
               "- 目的: 画面フローの検証\n" +
               "- 入力: " + prompt.UserOverview + "\n\n" +
               new string('あ', 150);
    }
}
