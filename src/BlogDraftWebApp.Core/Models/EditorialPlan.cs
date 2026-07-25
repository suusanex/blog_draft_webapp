namespace BlogDraftWebApp.Core.Models;

public sealed class EditorialPlan
{
    public BriefItem? Thesis { get; set; }
    public List<BriefItem> FocalPoints { get; set; } = [];
    public List<BriefItem> TriedOrObserved { get; set; } = [];
    public List<BriefItem> Judgements { get; set; } = [];
    public List<BriefItem> ReaderAssumptions { get; set; } = [];
    public List<BriefItem> ExcludedScope { get; set; } = [];
    public List<PlannedSection> Sections { get; set; } = [];
}
