using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BlogDraftWebApp.Core.Configuration;

namespace BlogDraftWebApp.Core.Services;

public sealed class LiteDbWorkflowRepository : IWorkflowRepository
{
    private readonly ILogger<LiteDbWorkflowRepository> _logger;
    private readonly string _databasePath;

    public LiteDbWorkflowRepository(IOptions<WorkflowOptions> options, ILogger<LiteDbWorkflowRepository> logger)
    {
        _logger = logger;
        _databasePath = options.Value.DatabasePath;
        EnsureDirectoryExists(_databasePath);
    }

    public Task CreateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() =>
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            sessions.Insert(session);
        }, "セッションの保存に失敗しました", cancellationToken);
    }

    public Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            var session = sessions.FindById(sessionId);
            if (session is null)
            {
                throw new SessionNotFoundException(sessionId);
            }

            return Task.FromResult(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "セッションの読み込みに失敗しました");
            throw new WorkflowStorageException("セッションの読み込みに失敗しました", ex);
        }
    }

    public Task UpdateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() =>
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            sessions.Update(session);
        }, "セッションの更新に失敗しました", cancellationToken);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() =>
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            sessions.Delete(sessionId);
        }, "セッションの削除に失敗しました", cancellationToken);
    }

    public Task<List<WorkflowSession>> ListSessionsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            var list = sessions.Query()
                .OrderByDescending(x => x.LastAccessedAt)
                .Skip(skip)
                .Limit(take)
                .ToList();
            return Task.FromResult(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "セッション一覧の取得に失敗しました");
            throw new WorkflowStorageException("セッション一覧の取得に失敗しました", ex);
        }
    }

    public Task<List<WorkflowSession>> ListExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            var list = sessions.Query()
                .Where(x => x.DeleteAt <= now)
                .ToList();
            return Task.FromResult(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "期限切れセッションの取得に失敗しました");
            throw new WorkflowStorageException("期限切れセッションの取得に失敗しました", ex);
        }
    }

    public Task<int> DeleteExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() =>
        {
            using var db = CreateDatabase();
            var sessions = GetSessions(db);
            return sessions.DeleteMany(x => x.DeleteAt <= now);
        }, "期限切れセッションの削除に失敗しました", cancellationToken);
    }

    public Task UpsertSnapshotAsync(RagSnapshot snapshot, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() =>
        {
            using var db = CreateDatabase();
            var snapshots = GetSnapshots(db);
            snapshots.Upsert(snapshot);
        }, "スナップショットの保存に失敗しました", cancellationToken);
    }

    public Task<RagSnapshot> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var db = CreateDatabase();
            var snapshots = GetSnapshots(db);
            var snapshot = snapshots.FindById(snapshotId);
            if (snapshot is null)
            {
                throw new WorkflowStorageException("スナップショットが見つかりません", new KeyNotFoundException(snapshotId));
            }

            return Task.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スナップショットの読み込みに失敗しました");
            throw new WorkflowStorageException("スナップショットの読み込みに失敗しました", ex);
        }
    }

    private static ILiteCollection<WorkflowSession> GetSessions(LiteDatabase db)
    {
        var sessions = db.GetCollection<WorkflowSession>("workflow_sessions");
        sessions.EnsureIndex(x => x.DeleteAt);
        sessions.EnsureIndex(x => x.LastAccessedAt);
        return sessions;
    }

    private static ILiteCollection<RagSnapshot> GetSnapshots(LiteDatabase db)
    {
        return db.GetCollection<RagSnapshot>("rag_snapshots");
    }

    private LiteDatabase CreateDatabase()
    {
        var connectionString = new ConnectionString
        {
            Filename = _databasePath,
            Connection = ConnectionType.Shared,
        };
        return new LiteDatabase(connectionString);
    }

    private static void EnsureDirectoryExists(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private Task ExecuteAsync(Action action, string message, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", message);
                throw new WorkflowStorageException(message, ex);
            }
        }, cancellationToken);
    }

    private Task<T> ExecuteAsync<T>(Func<T> action, string message, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", message);
                throw new WorkflowStorageException(message, ex);
            }
        }, cancellationToken);
    }

}
