using System.Text.Json;
using System.Text.Json.Serialization;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public sealed class EditorialPlanService : IEditorialPlanService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILlmClient _llmClient;

    public EditorialPlanService(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<EditorialPlanResult> ProposeAsync(
        string overview,
        PlanGenerationMode mode,
        EditorialPlan? currentPlan,
        CancellationToken cancellationToken)
    {
        EditorialPlan? normalizedBrief = null;
        if (mode == PlanGenerationMode.SectionsOnly)
        {
            normalizedBrief = EditorialPlanValidator.NormalizeAndValidateBrief(currentPlan, overview);
        }

        var prompt = EditorialPlanPromptComposer.Compose(overview, mode, normalizedBrief);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var draft = await _llmClient.GenerateAsync(prompt, cancellationToken);
            try
            {
                var plan = ParseAndValidate(draft.Content, overview, mode, normalizedBrief);
                return new EditorialPlanResult(plan, draft.Model, draft.GeneratedAt);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or EditorialPlanValidationException)
            {
                if (attempt == 0)
                {
                    prompt = EditorialPlanPromptComposer.Compose(
                        overview,
                        mode,
                        normalizedBrief,
                        BuildRepairReason(ex));
                    continue;
                }

                throw new LlmException(
                    "LLM_INVALID_RESPONSE",
                    "LLMから有効な編集計画を取得できませんでした",
                    true,
                    ex);
            }
        }

        throw new LlmException("LLM_INVALID_RESPONSE", "LLMから有効な編集計画を取得できませんでした", true);
    }

    private static EditorialPlan ParseAndValidate(
        string content,
        string overview,
        PlanGenerationMode mode,
        EditorialPlan? currentPlan)
    {
        var json = StripCodeFence(content);
        using var document = JsonDocument.Parse(json);
        EnsureRequiredShape(document.RootElement, mode);
        if (mode == PlanGenerationMode.SectionsOnly)
        {
            var envelope = JsonSerializer.Deserialize<EditorialSectionsResponse>(json, JsonOptions)
                ?? throw new JsonException("sections応答が空です");

            if (currentPlan is null)
            {
                throw new EditorialPlanValidationException("SectionsOnlyには現在のブリーフが必要です");
            }

            var merged = new EditorialPlan
            {
                Thesis = currentPlan.Thesis,
                FocalPoints = currentPlan.FocalPoints,
                TriedOrObserved = currentPlan.TriedOrObserved,
                Judgements = currentPlan.Judgements,
                ReaderAssumptions = currentPlan.ReaderAssumptions,
                ExcludedScope = currentPlan.ExcludedScope,
                Sections = envelope.Sections,
            };

            return EditorialPlanValidator.NormalizeAndValidate(merged, overview);
        }

        var plan = JsonSerializer.Deserialize<EditorialPlan>(json, JsonOptions)
            ?? throw new JsonException("編集計画が空です");
        return EditorialPlanValidator.NormalizeAndValidate(plan, overview, allowUserEdited: false);
    }

    private static void EnsureRequiredShape(JsonElement root, PlanGenerationMode mode)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("編集計画のルートはJSONオブジェクトである必要があります");
        }

        var topLevel = mode == PlanGenerationMode.Full
            ? new[] { "thesis", "focalPoints", "triedOrObserved", "judgements", "readerAssumptions", "excludedScope", "sections" }
            : new[] { "sections" };
        foreach (var property in topLevel)
        {
            if (!TryGetProperty(root, property, out _))
            {
                throw new JsonException($"必須プロパティがありません: {property}");
            }
        }

        if (!TryGetProperty(root, "sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var section in sections.EnumerateArray())
        {
            RequireObjectProperties(section, "id", "heading", "purpose", "sourceItemIds", "excludedScopeItemIds");
            if (TryGetProperty(section, "sourceItemIds", out var sourceIds) && sourceIds.ValueKind == JsonValueKind.Array)
            {
                foreach (var sourceId in sourceIds.EnumerateArray())
                {
                    if (sourceId.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException("sourceItemIdsには文字列だけを指定してください");
                    }
                }
            }
        }

        if (mode == PlanGenerationMode.SectionsOnly)
        {
            return;
        }

        foreach (var property in new[] { "focalPoints", "triedOrObserved", "judgements", "readerAssumptions", "excludedScope" })
        {
            if (TryGetProperty(root, property, out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    RequireObjectProperties(item, "id", "text", "origin", "sourceExcerpt");
                }
            }
        }

        if (TryGetProperty(root, "thesis", out var thesis) && thesis.ValueKind == JsonValueKind.Object)
        {
            RequireObjectProperties(thesis, "id", "text", "origin", "sourceExcerpt");
        }
    }

    private static void RequireObjectProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("配列要素はJSONオブジェクトである必要があります");
        }

        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out _))
            {
                throw new JsonException($"必須プロパティがありません: {name}");
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string BuildRepairReason(Exception exception)
    {
        return exception switch
        {
            JsonException => "JSONの構文またはプロパティ契約に違反しています。",
            NotSupportedException => "JSONの型契約に違反しています。",
            EditorialPlanValidationException validation => $"編集計画の検証に失敗しました: {validation.Message}",
            _ => "編集計画の検証に失敗しました。",
        };
    }

    private static string StripCodeFence(string content)
    {
        var value = content.Trim();
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var firstNewLine = value.IndexOf('\n');
        var lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewLine < 0 || lastFence <= firstNewLine)
        {
            return value;
        }

        return value[(firstNewLine + 1)..lastFence].Trim();
    }

    private sealed class EditorialSectionsResponse
    {
        public List<PlannedSection> Sections { get; set; } = [];
    }
}
