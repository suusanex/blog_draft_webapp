namespace BlogDraftWebApp.Core.Models;

public sealed class BlogOverview
{
    public BlogOverview(string content)
    {
        Content = content ?? string.Empty;
    }

    public string Content { get; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new ArgumentException("記事の概要を入力してください", nameof(Content));
        }

        if (Content.Length < 10)
        {
            throw new ArgumentException("概要は 10 文字以上入力してください", nameof(Content));
        }

        if (Content.Length > 5000)
        {
            throw new ArgumentException("概要は 5000 文字以内で入力してください", nameof(Content));
        }
    }
}
