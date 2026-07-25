using System.ComponentModel.DataAnnotations;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.Api.Models;

public sealed class EditorialPlanRequest
{
    [Required(ErrorMessage = BlogOverview.RequiredErrorMessage)]
    [MinLength(BlogOverview.MinimumLength, ErrorMessage = BlogOverview.MinimumLengthErrorMessage)]
    [MaxLength(BlogOverview.MaximumLength, ErrorMessage = BlogOverview.MaximumLengthErrorMessage)]
    public string Overview { get; set; } = string.Empty;

    public PlanGenerationMode Mode { get; set; } = PlanGenerationMode.Full;
    public EditorialPlan? CurrentPlan { get; set; }
}
