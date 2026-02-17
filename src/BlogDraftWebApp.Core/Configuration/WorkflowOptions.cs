namespace BlogDraftWebApp.Core.Configuration;

public sealed class WorkflowOptions
{
    public string DatabasePath { get; init; } = "./data/workflow-sessions.db";
    public int SessionRetentionDays { get; init; } = 30;
    public string CleanupSchedule { get; init; } = "0 2 * * *";
    public int? MaxSessionsPerUser { get; init; }
}
