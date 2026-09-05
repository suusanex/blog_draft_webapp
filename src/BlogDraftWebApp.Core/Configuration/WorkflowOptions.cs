namespace BlogDraftWebApp.Core.Configuration;

public sealed class WorkflowOptions
{
    public string DatabasePath { get; init; } = "./data/workflow-sessions.db";
    public int SessionRetentionDays { get; init; } = 30;
    public string CleanupSchedule { get; init; } = "0 2 * * *";
    public int? MaxSessionsPerUser { get; init; }
    public int OutlineMinLines { get; init; } = 1;
    public int OutlineMaxLines { get; init; } = 80;
    public int OutlineMaxDepth { get; init; } = 2;
    public int OutlineMaxLineLength { get; init; } = 120;
    public int OutlineMaxTotalChars { get; init; } = 8000;
    public int? OutlineMaxOutputTokens { get; init; }
}
