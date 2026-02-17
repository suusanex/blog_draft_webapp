namespace BlogDraftWebApp.Api.Models;

public sealed class RefreshRagSnapshotResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string PreviousSnapshotId { get; set; } = string.Empty;
    public string NewSnapshotId { get; set; } = string.Empty;
    public int AddedChunkCount { get; set; }
    public int RemovedChunkCount { get; set; }
    public double ChangeRate { get; set; }
    public bool Confirmed { get; set; }
}
