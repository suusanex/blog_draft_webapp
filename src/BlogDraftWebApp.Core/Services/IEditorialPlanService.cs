using System.Text.Json;
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

[JsonConverter(typeof(PlanGenerationModeJsonConverter))]
public enum PlanGenerationMode
{
    Full,
    SectionsOnly,
}

public sealed class PlanGenerationModeJsonConverter : JsonConverter<PlanGenerationMode>
{
    public override PlanGenerationMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "full" => PlanGenerationMode.Full,
            "sectionsonly" => PlanGenerationMode.SectionsOnly,
            _ => throw new JsonException($"Unknown plan generation mode: {value}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, PlanGenerationMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value == PlanGenerationMode.Full ? "full" : "sectionsOnly");
    }
}

public sealed record EditorialPlanResult(
    EditorialPlan Plan,
    string Model,
    DateTimeOffset GeneratedAt);
