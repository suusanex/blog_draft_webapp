namespace BlogDraftWebApp.Core.Models;

public sealed class Outline
{
    public Outline(string content)
    {
        Content = content ?? string.Empty;
    }

    public string Content { get; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new ArgumentException("アウトラインを入力してください", nameof(Content));
        }

        if (Content.Length < 50)
        {
            throw new ArgumentException("アウトラインは 50 文字以上入力してください", nameof(Content));
        }

        if (Content.Length > 5000)
        {
            throw new ArgumentException("アウトラインは 5000 文字以内で入力してください", nameof(Content));
        }
    }
}
