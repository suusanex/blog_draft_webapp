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

    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly string _content;
        public FakeLlmClient(string content) => _content = content;

        public Task<Draft> GenerateAsync(Prompt prompt, CancellationToken cancellationToken) =>
            Task.FromResult(new Draft { Content = _content, Model = "test-model" });
    }
}
