using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class AzureAISearchServiceTests
{
    [Test]
    public async Task RetrieveAsync_MinimumScore未満は除外し_同一URLは重複除外する()
    {
        var options = Options.Create(new RagOptions
        {
            Enabled = true,
            Endpoint = "https://example.search.windows.net",
            ApiKey = "x",
            IndexName = "idx",
            TextFieldName = "content",
            TopK = 10,
            MinimumScore = 0.7,
        });

        var mockClient = new Mock<IAzureSearchClient>(MockBehavior.Strict);
        mockClient
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AzureSearchHit>
            {
                new(0.9, new Dictionary<string, object?> { ["content"] = "text1", ["metadata_storage_path"] = "u1", ["title_Data_Column"] = "t1" }),
                new(0.6, new Dictionary<string, object?> { ["content"] = "low", ["metadata_storage_path"] = "u2", ["title_Data_Column"] = "t2" }),
                new(0.95, new Dictionary<string, object?> { ["content"] = "dup", ["metadata_storage_path"] = "u1", ["title_Data_Column"] = "t1" }),
            });

        var mockFactory = new Mock<IAzureSearchClientFactory>(MockBehavior.Strict);
        mockFactory.Setup(x => x.CreateClient(It.IsAny<RagOptions>())).Returns(mockClient.Object);

        var service = new AzureAISearchService(NullLogger<AzureAISearchService>.Instance, options, mockFactory.Object);

        var result = await service.RetrieveAsync("query", CancellationToken.None);

        Assert.That(result.Warning, Is.Null);
        Assert.That(result.Chunks.Count, Is.EqualTo(1));
        Assert.That(result.Chunks[0].Text, Is.EqualTo("text1"));
        Assert.That(result.Chunks[0].SourceTitle, Is.EqualTo("t1"));
        Assert.That(result.Chunks[0].SourceUrl, Is.EqualTo("u1"));
    }

    [Test]
    public async Task RetrieveAsync_例外時は空配列と警告を返す()
    {
        var options = Options.Create(new RagOptions
        {
            Enabled = true,
            Endpoint = "https://example.search.windows.net",
            ApiKey = "x",
            IndexName = "idx",
            TextFieldName = "content",
        });

        var mockClient = new Mock<IAzureSearchClient>(MockBehavior.Strict);
        mockClient
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var mockFactory = new Mock<IAzureSearchClientFactory>(MockBehavior.Strict);
        mockFactory.Setup(x => x.CreateClient(It.IsAny<RagOptions>())).Returns(mockClient.Object);

        var service = new AzureAISearchService(NullLogger<AzureAISearchService>.Instance, options, mockFactory.Object);

        Assert.That(async () => await service.RetrieveAsync("query", CancellationToken.None), Throws.InstanceOf<RagException>());
    }
}
