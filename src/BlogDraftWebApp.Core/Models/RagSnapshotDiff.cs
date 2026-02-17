namespace BlogDraftWebApp.Core.Models;

public sealed class RagSnapshotDiff
{
    public int AddedChunkCount { get; init; }
    public int RemovedChunkCount { get; init; }
    public double ChangeRate { get; init; }
}
