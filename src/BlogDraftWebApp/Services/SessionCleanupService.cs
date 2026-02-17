using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Services;

public sealed class SessionCleanupService : BackgroundService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly WorkflowOptions _workflowOptions;
    private readonly ILogger<SessionCleanupService> _logger;
    private DateOnly? _lastRunDate;

    public SessionCleanupService(
        IWorkflowRepository workflowRepository,
        IOptions<WorkflowOptions> workflowOptions,
        ILogger<SessionCleanupService> logger)
    {
        _workflowRepository = workflowRepository;
        _workflowOptions = workflowOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (!ShouldRun(now))
                {
                    continue;
                }

                var deletedCount = await _workflowRepository.DeleteExpiredSessionsAsync(now, stoppingToken);
                _lastRunDate = DateOnly.FromDateTime(now.UtcDateTime);
                _logger.LogInformation("Session cleanup finished. DeletedCount={DeletedCount}", deletedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session cleanup failed.");
            }
        }
    }

    private bool ShouldRun(DateTimeOffset now)
    {
        if (_lastRunDate == DateOnly.FromDateTime(now.UtcDateTime))
        {
            return false;
        }

        var schedule = _workflowOptions.CleanupSchedule;
        var parts = schedule.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return now.Hour == 2 && now.Minute < 30;
        }

        if (!int.TryParse(parts[0], out var minute))
        {
            minute = 0;
        }

        if (!int.TryParse(parts[1], out var hour))
        {
            hour = 2;
        }

        return now.Hour == hour && now.Minute >= minute && now.Minute < minute + 30;
    }
}
