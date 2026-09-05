namespace BlogDraftWebApp.Core.Models;

public sealed class EditorialMemo
{
    public List<MeaningElement> MeaningElements { get; set; } = new();
    public List<string> LogicalRelations { get; set; } = new();
    public string? ArticleQuestion { get; set; }
    public string? ReaderAssumption { get; set; }
    public List<SectionScope> ScopeBySection { get; set; } = new();
    public List<string> OpenQuestions { get; set; } = new();
}

public sealed class MeaningElement
{
    public string? Source { get; set; }
    public string? Role { get; set; }
}

public sealed class SectionScope
{
    public string? Heading { get; set; }
    public List<string> Covers { get; set; } = new();
    public string? AllowedSupplement { get; set; }
    public string? DoNotAdd { get; set; }
}
