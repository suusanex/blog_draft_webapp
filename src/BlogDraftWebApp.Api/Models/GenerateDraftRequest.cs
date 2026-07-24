using System.ComponentModel.DataAnnotations;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Api.Models;

public sealed class GenerateDraftRequest
{
    [Required(ErrorMessage = BlogOverview.RequiredErrorMessage)]
    [MinLength(BlogOverview.MinimumLength, ErrorMessage = BlogOverview.MinimumLengthErrorMessage)]
    [MaxLength(BlogOverview.MaximumLength, ErrorMessage = BlogOverview.MaximumLengthErrorMessage)]
    public string Overview { get; set; } = string.Empty;
}
