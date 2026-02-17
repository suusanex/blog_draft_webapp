using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public interface IWorkflowRepository
{
    Task CreateSessionAsync(WorkflowSession session, CancellationToken cancellationToken);
    Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task UpdateSessionAsync(WorkflowSession session, CancellationToken cancellationToken);
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<List<WorkflowSession>> ListSessionsAsync(int skip, int take, CancellationToken cancellationToken);
    Task<List<WorkflowSession>> ListExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<int> DeleteExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task UpsertSnapshotAsync(RagSnapshot snapshot, CancellationToken cancellationToken);
    Task<RagSnapshot> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken);
}
