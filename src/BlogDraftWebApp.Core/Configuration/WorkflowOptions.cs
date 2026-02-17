namespace BlogDraftWebApp.Core.Configuration;

public sealed class WorkflowOptions
{
    public string DatabasePath { get; init; } = "./data/workflow-sessions.db";
    public int SessionRetentionDays { get; init; } = 30;
    public string CleanupSchedule { get; init; } = "0 2 * * *";
    public int? MaxSessionsPerUser { get; init; }
    public int OutlineMinLines { get; init; } = 5;
    public int OutlineMaxLines { get; init; } = 15;
    public int OutlineMaxDepth { get; init; } = 2;
    public int OutlineMaxLineLength { get; init; } = 120;
    public int OutlineMaxTotalChars { get; init; } = 2000;
    public int OutlineMaxOutputTokens { get; init; } = 350;
}
