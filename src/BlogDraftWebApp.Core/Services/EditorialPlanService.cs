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
        if (mode == PlanGenerationMode.SectionsOnly)
        {
            EditorialPlanValidator.Validate(currentPlan, overview);
        }

        var prompt = EditorialPlanPromptComposer.Compose(overview, mode, currentPlan);
        var draft = await _llmClient.GenerateAsync(prompt, cancellationToken);
        var json = StripCodeFence(draft.Content);

        EditorialPlan plan;
        try
        {
            plan = JsonSerializer.Deserialize<EditorialPlan>(json, JsonOptions)
                ?? throw new JsonException("編集計画が空です");
            EditorialPlanValidator.Validate(plan, overview);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or EditorialPlanValidationException)
        {
            throw new LlmException("LLM_INVALID_RESPONSE", "LLMから有効な編集計画を取得できませんでした", true, ex);
        }

        if (mode == PlanGenerationMode.SectionsOnly && currentPlan is not null)
        {
            PreserveBriefItems(currentPlan, plan);
        }

        return new EditorialPlanResult(plan, draft.Model, draft.GeneratedAt);
    }

    private static void PreserveBriefItems(EditorialPlan current, EditorialPlan proposed)
    {
        proposed.Thesis = current.Thesis;
        proposed.FocalPoints = current.FocalPoints;
        proposed.TriedOrObserved = current.TriedOrObserved;
        proposed.Judgements = current.Judgements;
        proposed.ReaderAssumptions = current.ReaderAssumptions;
        proposed.ExcludedScope = current.ExcludedScope;
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
}
