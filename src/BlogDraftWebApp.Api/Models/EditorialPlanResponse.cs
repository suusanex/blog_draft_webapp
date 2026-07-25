using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Api.Models;

public sealed class EditorialPlanResponse
{
    public EditorialPlan Plan { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}
