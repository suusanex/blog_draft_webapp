namespace BlogDraftWebApp.Core.Models;

public sealed class BlogOverview
{
    public BlogOverview()
    {
    }

    public BlogOverview(string content)
    {
        Content = content ?? string.Empty;
    }

    public string Content { get; private set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new ArgumentException("記事の概要を入力してください", nameof(Content));
        }

        if (Content.Length > 5000)
        {
            throw new ArgumentException("概要は 5000 文字以内で入力してください", nameof(Content));
        }
    }
}
