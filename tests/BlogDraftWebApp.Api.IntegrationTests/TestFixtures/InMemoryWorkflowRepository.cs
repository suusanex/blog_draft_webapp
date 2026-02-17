using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.Api.IntegrationTests.TestFixtures;

public sealed class InMemoryWorkflowRepository : IWorkflowRepository
{
    private readonly Dictionary<string, WorkflowSession> _sessions = new();
    private readonly Dictionary<string, RagSnapshot> _snapshots = new();

    public Task CreateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new SessionNotFoundException(sessionId);
        }

        return Task.FromResult(session);
    }

    public Task UpdateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        _sessions.Remove(sessionId);
        return Task.CompletedTask;
    }

    public Task<List<WorkflowSession>> ListSessionsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        return Task.FromResult(_sessions.Values.Skip(skip).Take(take).ToList());
    }

    public Task<List<WorkflowSession>> ListExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return Task.FromResult(_sessions.Values.Where(x => x.DeleteAt <= now).ToList());
    }

    public Task<int> DeleteExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = _sessions.Values.Where(x => x.DeleteAt <= now).Select(x => x.SessionId).ToList();
        foreach (var id in expired)
        {
            _sessions.Remove(id);
        }

        return Task.FromResult(expired.Count);
    }

    public Task UpsertSnapshotAsync(RagSnapshot snapshot, CancellationToken cancellationToken)
    {
        _snapshots[snapshot.SnapshotId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<RagSnapshot> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            throw new WorkflowStorageException("スナップショットが見つかりません", new KeyNotFoundException(snapshotId));
        }

        return Task.FromResult(snapshot);
    }
}
