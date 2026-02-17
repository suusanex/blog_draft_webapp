namespace BlogDraftWebApp.Core.Models;

public sealed class BlogOverview
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 5000;
    public const string RequiredErrorMessage = "この記事で伝えたいポイントを入力してください";
    public const string MinimumLengthErrorMessage = "伝えたいポイントは10文字以上入力してください";
    public const string MaximumLengthErrorMessage = "伝えたいポイントは5000文字以内で入力してください";

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
            throw new ArgumentException(RequiredErrorMessage, nameof(Content));
        }

        if (Content.Length < MinimumLength)
        {
            throw new ArgumentException(MinimumLengthErrorMessage, nameof(Content));
        }

        if (Content.Length > MaximumLength)
        {
            throw new ArgumentException(MaximumLengthErrorMessage, nameof(Content));
        }
    }
}
