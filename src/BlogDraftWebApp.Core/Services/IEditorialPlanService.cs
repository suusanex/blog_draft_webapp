using System.Text.Json.Serialization;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public interface IEditorialPlanService
{
    Task<EditorialPlanResult> ProposeAsync(
        string overview,
        PlanGenerationMode mode,
        EditorialPlan? currentPlan,
        CancellationToken cancellationToken);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanGenerationMode
{
    Full,
    SectionsOnly,
}

public sealed record EditorialPlanResult(
    EditorialPlan Plan,
    string Model,
    DateTimeOffset GeneratedAt);
