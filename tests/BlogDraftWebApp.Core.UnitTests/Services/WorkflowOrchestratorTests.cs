using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class WorkflowOrchestratorTests
{
    [Test]
    public async Task GenerateStepAsync_ThrowsSessionBusy_WhenLocked()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var orchestrator = CreateOrchestrator(repository, lockService);

        var session = new WorkflowSession
        {
            SessionId = "s1",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);
        using var handle = await lockService.TryAcquireAsync(session.SessionId, CancellationToken.None);

        var ex = Assert.ThrowsAsync<SessionBusyException>(() =>
            orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void GetSessionAsync_ThrowsExpired_WhenDeleteAtPassed()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var orchestrator = CreateOrchestrator(repository, lockService);

        var session = new WorkflowSession
        {
            SessionId = "expired",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-31),
            LastAccessedAt = DateTimeOffset.UtcNow.AddDays(-31),
            DeleteAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        repository.CreateSessionAsync(session, CancellationToken.None).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<SessionNotFoundException>(() =>
            orchestrator.GetSessionAsync(session.SessionId, CancellationToken.None));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.IsExpired, Is.True);
    }

    [Test]
    public async Task GenerateStepAsync_ReturnsGeneratedOutline()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>();
        var composerMock = new Mock<IPromptComposer>();

        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composerMock);

        var session = new WorkflowSession
        {
            SessionId = "s2",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);

        retrievalMock
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(new List<RAGChunk>(), null));

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<IReadOnlyList<RAGChunk>>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Draft { Content = "outline", Model = "m1", GeneratedAt = DateTimeOffset.UtcNow });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("outline"));
        Assert.That(result.Model, Is.EqualTo("m1"));
        Assert.That(repository.Sessions[session.SessionId].OutlineGenerated, Is.EqualTo("outline"));
    }

    private static WorkflowOrchestrator CreateOrchestrator(
        InMemoryWorkflowRepository repository,
        WorkflowSessionLock lockService,
        Mock<ILlmClient>? llmMock = null,
        Mock<IRetrievalService>? retrievalMock = null,
        Mock<IPromptComposer>? composerMock = null)
    {
        var options = Options.Create(new WorkflowOptions
        {
            DatabasePath = "./data/test.db",
            SessionRetentionDays = 30,
        });

        var llm = llmMock?.Object ?? new Mock<ILlmClient>().Object;
        var retrieval = retrievalMock?.Object ?? new Mock<IRetrievalService>().Object;
        var composer = composerMock?.Object ?? new Mock<IPromptComposer>().Object;
        var styleCard = new StyleCard { Title = "t", Content = "c", SystemPrompt = "s" };

        return new WorkflowOrchestrator(
            repository,
            retrieval,
            composer,
            llm,
            styleCard,
            lockService,
            options,
            NullLogger<WorkflowOrchestrator>.Instance);
    }

    private sealed class InMemoryWorkflowRepository : IWorkflowRepository
    {
        public Dictionary<string, WorkflowSession> Sessions { get; } = new();
        public Dictionary<string, RagSnapshot> Snapshots { get; } = new();

        public Task CreateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
        {
            Sessions[session.SessionId] = session;
            return Task.CompletedTask;
        }

        public Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (!Sessions.TryGetValue(sessionId, out var session))
            {
                throw new SessionNotFoundException(sessionId);
            }

            return Task.FromResult(session);
        }

        public Task UpdateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
        {
            Sessions[session.SessionId] = session;
            return Task.CompletedTask;
        }

        public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            Sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<List<WorkflowSession>> ListSessionsAsync(int skip, int take, CancellationToken cancellationToken)
        {
            return Task.FromResult(Sessions.Values.Skip(skip).Take(take).ToList());
        }

        public Task<List<WorkflowSession>> ListExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            return Task.FromResult(Sessions.Values.Where(x => x.DeleteAt <= now).ToList());
        }

        public Task<int> DeleteExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var expired = Sessions.Values.Where(x => x.DeleteAt <= now).Select(x => x.SessionId).ToList();
            foreach (var id in expired)
            {
                Sessions.Remove(id);
            }

            return Task.FromResult(expired.Count);
        }

        public Task UpsertSnapshotAsync(RagSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshots[snapshot.SnapshotId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<RagSnapshot> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
        {
            if (!Snapshots.TryGetValue(snapshotId, out var snapshot))
            {
                throw new WorkflowStorageException("スナップショットが見つかりません", new KeyNotFoundException(snapshotId));
            }

            return Task.FromResult(snapshot);
        }
    }
}
