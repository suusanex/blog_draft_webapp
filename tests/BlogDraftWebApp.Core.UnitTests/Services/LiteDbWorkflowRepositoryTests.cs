using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class LiteDbWorkflowRepositoryTests
{
    [Test]
    public async Task CRUD_Session_WorksWithTempDatabase()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var repository = CreateRepository(dbPath);

            var session = new WorkflowSession
            {
                SessionId = Guid.NewGuid().ToString(),
                InitialInput = new BlogOverview("0123456789"),
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow,
                DeleteAt = DateTimeOffset.UtcNow.AddDays(30),
            };

            await repository.CreateSessionAsync(session, CancellationToken.None);

            var loaded = await repository.GetSessionAsync(session.SessionId, CancellationToken.None);
            Assert.That(loaded.SessionId, Is.EqualTo(session.SessionId));
            Assert.That(loaded.InitialInput.Content, Is.EqualTo("0123456789"));

            loaded.OutlineEdited = "updated-outline-content-updated-outline-content-updated-outline";
            await repository.UpdateSessionAsync(loaded, CancellationToken.None);

            var updated = await repository.GetSessionAsync(session.SessionId, CancellationToken.None);
            Assert.That(updated.OutlineEdited, Is.EqualTo(loaded.OutlineEdited));

            await repository.DeleteSessionAsync(session.SessionId, CancellationToken.None);
            Assert.That(async () => await repository.GetSessionAsync(session.SessionId, CancellationToken.None), Throws.InstanceOf<WorkflowStorageException>());
        }
        finally
        {
            CleanupTempDatabaseFiles(dbPath);
        }
    }

    [Test]
    public async Task UpsertAndGetSnapshot_WorksWithTempDatabase()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var repository = CreateRepository(dbPath);

            var snapshotId = Guid.NewGuid().ToString();
            var snapshot = new RagSnapshot
            {
                SnapshotId = snapshotId,
                SearchQuery = "query",
                SearchExecutedAt = DateTimeOffset.UtcNow,
                ChunkCount = 1,
                Chunks = new List<RAGChunk>
                {
                    new()
                    {
                        Text = "chunk",
                        Score = 0.95,
                        SourceTitle = "title",
                        SourceUrl = "https://example.com",
                    },
                },
            };

            await repository.UpsertSnapshotAsync(snapshot, CancellationToken.None);

            var loaded = await repository.GetSnapshotAsync(snapshotId, CancellationToken.None);
            Assert.That(loaded.SnapshotId, Is.EqualTo(snapshotId));
            Assert.That(loaded.Chunks.Count, Is.EqualTo(1));
            Assert.That(loaded.Chunks[0].Text, Is.EqualTo("chunk"));
        }
        finally
        {
            CleanupTempDatabaseFiles(dbPath);
        }
    }

    private static LiteDbWorkflowRepository CreateRepository(string databasePath)
    {
        var options = Options.Create(new WorkflowOptions
        {
            DatabasePath = databasePath,
            SessionRetentionDays = 30,
            CleanupSchedule = "0 2 * * *",
        });

        return new LiteDbWorkflowRepository(options, NullLogger<LiteDbWorkflowRepository>.Instance);
    }

    private static string CreateTempDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "BlogDraftWebAppTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "workflow-test.db");
    }

    private static void CleanupTempDatabaseFiles(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
        }
    }
}
