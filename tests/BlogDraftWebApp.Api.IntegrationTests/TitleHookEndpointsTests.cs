using System.Net;
using System.Net.Http.Json;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Models;
using Moq;

namespace BlogDraftWebApp.Api.IntegrationTests;

public sealed class TitleHookEndpointsTests
{
    [Test]
    public async Task PreviewTitleHook_ReturnsPrompt()
    {
        await using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/titlehook/preview", new GenerateTitleHookRequest
        {
            ArticleBody = new string('A', 120),
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<PreviewPromptResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Step, Is.EqualTo("titlehook"));
        Assert.That(payload.Prompt, Does.Contain("タイトル案と冒頭段落案の生成"));

        factory.LlmClientMock.Verify(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public async Task GenerateTitleHook_ReturnsThreeOptions()
    {
        await using var factory = new TestWebApplicationFactory();

        factory.LlmClientMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "# タイトル1\n導入1\n---\n# タイトル2\n導入2\n---\n# タイトル3\n導入3",
                Model = "test",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        using var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("/titlehook", new GenerateTitleHookRequest
        {
            ArticleBody = new string('B', 150),
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<GenerateTitleHookResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Options.Count, Is.EqualTo(3));
        Assert.That(payload.Options[0].Title, Is.EqualTo("タイトル1"));
    }
}
