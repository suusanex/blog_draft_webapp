using System.Text.Json;
using System.Text.Json.Nodes;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public static class EditorialPlanJsonContract
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public static StructuredOutputDefinition For(PlanGenerationMode mode)
    {
        return new StructuredOutputDefinition
        {
            Name = mode == PlanGenerationMode.Full ? "editorial_plan" : "editorial_sections",
            SchemaJson = BuildSchema(mode).ToJsonString(PrettyJson),
            Strict = true,
        };
    }

    public static string Describe(PlanGenerationMode mode)
    {
        var schema = BuildSchema(mode).ToJsonString(PrettyJson);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("次のJSON Schemaに厳密に従ってください。プロパティを省略せず、余分なプロパティを追加しないでください。");
        builder.AppendLine("```json");
        builder.AppendLine(schema);
        builder.AppendLine("```");
        builder.AppendLine("InputまたはReorganizedの項目には、自由入力中の完全一致sourceExcerptを付けてください。");
        builder.AppendLine("InferredEditorialConstraintはreaderAssumptionsとexcludedScopeだけで使用し、技術的事実・観測・判断には使用しないでください。");
        if (mode == PlanGenerationMode.Full)
        {
            builder.AppendLine("各sectionのsourceItemIdsには、thesis、focalPoints、triedOrObserved、judgementsのいずれかを最低1件指定してください。");
            builder.AppendLine("readerAssumptionsは本文材料ではなく、excludedScopeはexcludedScopeItemIdsからのみ参照する編集制約です。");
        }
        else
        {
            builder.AppendLine("SectionsOnlyではsections以外のプロパティを返さず、現在のブリーフ項目のIDだけを参照してください。");
        }

        return builder.ToString().Trim();
    }

    private static JsonObject BuildSchema(PlanGenerationMode mode)
    {
        return mode == PlanGenerationMode.Full ? BuildPlanSchema() : BuildSectionsSchema();
    }

    private static JsonObject BuildPlanSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Required("thesis", "focalPoints", "triedOrObserved", "judgements", "readerAssumptions", "excludedScope", "sections"),
            ["properties"] = new JsonObject
            {
                ["thesis"] = new JsonObject
                {
                    ["anyOf"] = new JsonArray(BriefItemSchema(), new JsonObject { ["type"] = "null" }),
                },
                ["focalPoints"] = ItemsArray(),
                ["triedOrObserved"] = ItemsArray(),
                ["judgements"] = ItemsArray(),
                ["readerAssumptions"] = ItemsArray(),
                ["excludedScope"] = ItemsArray(),
                ["sections"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = SectionSchema(),
                },
            },
        };
    }

    private static JsonObject BuildSectionsSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Required("sections"),
            ["properties"] = new JsonObject
            {
                ["sections"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = SectionSchema(),
                },
            },
        };
    }

    private static JsonObject BriefItemSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Required("id", "text", "origin", "sourceExcerpt"),
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject { ["type"] = "string" },
                ["text"] = new JsonObject { ["type"] = "string" },
                ["origin"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray(JsonValue.Create("Input"), JsonValue.Create("Reorganized"), JsonValue.Create("InferredEditorialConstraint")),
                },
                ["sourceExcerpt"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            },
        };
    }

    private static JsonObject ItemsArray()
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = BriefItemSchema(),
        };
    }

    private static JsonObject SectionSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Required("id", "heading", "purpose", "sourceItemIds", "excludedScopeItemIds"),
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject { ["type"] = "string" },
                ["heading"] = new JsonObject { ["type"] = "string" },
                ["purpose"] = new JsonObject { ["type"] = "string" },
                ["sourceItemIds"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                },
                ["excludedScopeItemIds"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                },
            },
        };
    }

    private static JsonArray Required(params string[] names) => new(names.Select(name => JsonValue.Create(name)).ToArray());
}
