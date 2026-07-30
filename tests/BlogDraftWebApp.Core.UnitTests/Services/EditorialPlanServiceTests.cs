using System.Text.Json;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class EditorialPlanServiceTests
{
    [Test]
    public async Task ProposeAsync_ParsesJsonPlan()
    {
        var plan = new EditorialPlan
        {
            Thesis = new BriefItem
            {
                Id = "thesis-1",
                Text = "中心",
                Origin = BriefItemOrigin.Input,
                SourceExcerpt = "0123456789",
            },
        };
        var service = new EditorialPlanService(new FakeLlmClient(JsonSerializer.Serialize(plan)));

        var result = await service.ProposeAsync("0123456789", PlanGenerationMode.Full, null, CancellationToken.None);

        Assert.That(result.Plan.Thesis!.Text, Is.EqualTo("中心"));
    }

    [Test]
    public void ProposeAsync_InvalidJson_ThrowsRetryableLlmException()
    {
        var service = new EditorialPlanService(new FakeLlmClient("not json"));

        var ex = Assert.ThrowsAsync<BlogDraftWebApp.Core.Exceptions.LlmException>(async () =>
            await service.ProposeAsync("0123456789", PlanGenerationMode.Full, null, CancellationToken.None));

        Assert.That(ex!.ErrorCode, Is.EqualTo("LLM_INVALID_RESPONSE"));
        Assert.That(ex.IsRetryable, Is.True);
    }

    [Test]
    public async Task ProposeAsync_InvalidFirstResponse_RetriesWithRepairPrompt()
    {
        var validPlan = new EditorialPlan
        {
            Thesis = new BriefItem
            {
                Id = "thesis-1",
                Text = "中心",
                Origin = BriefItemOrigin.Input,
                SourceExcerpt = "0123456789",
            },
        };
        var client = new SequenceLlmClient("not json", JsonSerializer.Serialize(validPlan));

        var result = await new EditorialPlanService(client)
            .ProposeAsync("0123456789", PlanGenerationMode.Full, null, CancellationToken.None);

        Assert.That(result.Plan.Thesis!.Id, Is.EqualTo("thesis-1"));
        Assert.That(client.Prompts, Has.Count.EqualTo(2));
        Assert.That(client.Prompts[1].UserOverview, Does.Contain("前回応答の検証結果"));
    }

    [Test]
    public async Task ProposeAsync_SectionsOnly_MergesAndRevalidatesCurrentBrief()
    {
        var current = new EditorialPlan
        {
            Thesis = new BriefItem { Id = "thesis-1", Text = "中心", Origin = BriefItemOrigin.Input, SourceExcerpt = "0123456789" },
            FocalPoints = [new BriefItem { Id = "focus-1", Text = "重要", Origin = BriefItemOrigin.Input, SourceExcerpt = "0123456789" }],
        };
        var response = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new PlannedSection { Id = "section-1", Heading = "見出し", Purpose = "目的", SourceItemIds = new List<string> { "focus-1" } },
            },
        });

        var result = await new EditorialPlanService(new SequenceLlmClient(response))
            .ProposeAsync("0123456789", PlanGenerationMode.SectionsOnly, current, CancellationToken.None);

        Assert.That(result.Plan.FocalPoints[0].Id, Is.EqualTo("focus-1"));
        Assert.That(result.Plan.Sections[0].SourceItemIds, Is.EqualTo(new[] { "focus-1" }));
    }

    [Test]
    public void ProposeAsync_SectionsOnly_RejectsUnknownReferenceAfterMerge()
    {
        var current = new EditorialPlan
        {
            Thesis = new BriefItem { Id = "thesis-1", Text = "中心", Origin = BriefItemOrigin.Input, SourceExcerpt = "0123456789" },
        };
        var response = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new PlannedSection { Id = "section-1", Heading = "見出し", Purpose = "目的", SourceItemIds = new List<string> { "unknown" } },
            },
        });

        var ex = Assert.ThrowsAsync<BlogDraftWebApp.Core.Exceptions.LlmException>(async () =>
            await new EditorialPlanService(new SequenceLlmClient(response, response))
                .ProposeAsync("0123456789", PlanGenerationMode.SectionsOnly, current, CancellationToken.None));

        Assert.That(ex!.ErrorCode, Is.EqualTo("LLM_INVALID_RESPONSE"));
    }

    [Test]
    public void JsonContract_ContainsNestedPlannerShape()
    {
        var full = EditorialPlanJsonContract.For(PlanGenerationMode.Full);
        var sections = EditorialPlanJsonContract.For(PlanGenerationMode.SectionsOnly);

        Assert.That(full.SchemaJson, Does.Contain("sourceExcerpt"));
        Assert.That(full.SchemaJson, Does.Contain("InferredEditorialConstraint"));
        Assert.That(sections.SchemaJson, Does.Contain("sourceItemIds"));
        Assert.That(sections.SchemaJson, Does.Not.Contain("focalPoints"));
    }

    [Test]
    public void JsonContract_ExcludesAzureUnsupportedKeywordsFromAllNestedSchemas()
    {
        foreach (var mode in Enum.GetValues<PlanGenerationMode>())
        {
            using var document = JsonDocument.Parse(EditorialPlanJsonContract.For(mode).SchemaJson);

            Assert.That(ContainsProperty(document.RootElement, "minLength"), Is.False, mode.ToString());
            Assert.That(ContainsProperty(document.RootElement, "minItems"), Is.False, mode.ToString());
            Assert.That(ContainsProperty(document.RootElement, "uniqueItems"), Is.False, mode.ToString());
        }
    }

    private static bool ContainsProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName) || ContainsProperty(property.Value, propertyName))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (ContainsProperty(child, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly string _content;
        public FakeLlmClient(string content) => _content = content;

        public Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null) =>
            Task.FromResult(new Draft { Content = _content, Model = "test-model" });
    }

    private sealed class SequenceLlmClient : ILlmClient
    {
        private readonly Queue<string> _responses;
        public List<Prompt> Prompts { get; } = [];

        public SequenceLlmClient(params string[] responses) => _responses = new Queue<string>(responses);

        public Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken, int? maxOutputTokens = null)
        {
            Prompts.Add(prompt);
            var content = _responses.Count == 0 ? "not json" : _responses.Dequeue();
            return Task.FromResult(new Draft { Content = content, Model = "test-model" });
        }
    }
}
