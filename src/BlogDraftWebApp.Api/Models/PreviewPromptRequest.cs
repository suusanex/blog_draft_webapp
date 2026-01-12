using System.ComponentModel.DataAnnotations;

namespace BlogDraftWebApp.Api.Models;

public sealed class PreviewPromptRequest
{
    [Required(ErrorMessage = "記事の概要を入力してください")]
    [MinLength(10, ErrorMessage = "概要は 10 文字以上入力してください")]
    [MaxLength(5000, ErrorMessage = "概要は 5000 文字以内で入力してください")]
    public string Overview { get; set; } = string.Empty;
}
