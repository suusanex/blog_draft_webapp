using System.Collections.Concurrent;

namespace BlogDraftWebApp.Core.Services;

public sealed class WorkflowSessionLock
{
    private sealed class Releaser : IDisposable
    {
        private readonly string _sessionId;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private bool _disposed;

        public Releaser(string sessionId, SemaphoreSlim semaphore, ConcurrentDictionary<string, SemaphoreSlim> locks)
        {
            _sessionId = sessionId;
            _semaphore = semaphore;
            _locks = locks;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _semaphore.Release();
            if (_semaphore.CurrentCount == 1)
            {
                _locks.TryRemove(_sessionId, out _);
            }

            _disposed = true;
        }
    }

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable?> TryAcquireAsync(string sessionId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(0, cancellationToken);
        if (!acquired)
        {
            return null;
        }

        return new Releaser(sessionId, semaphore, _locks);
    }
}
