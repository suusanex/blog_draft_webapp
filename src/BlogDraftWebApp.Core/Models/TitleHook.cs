namespace BlogDraftWebApp.Core.Models;

public sealed class TitleHook
{
    public string Title { get; init; } = string.Empty;
    public string HookText { get; init; } = string.Empty;
    public int? OptionIndex { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("タイトルを入力してください", nameof(Title));
        }

        if (Title.Length > 200)
        {
            throw new ArgumentException("タイトルは 200 文字以内で入力してください", nameof(Title));
        }

        if (string.IsNullOrWhiteSpace(HookText))
        {
            throw new ArgumentException("導入部を入力してください", nameof(HookText));
        }

        if (HookText.Length > 2000)
        {
            throw new ArgumentException("導入部は 2000 文字以内で入力してください", nameof(HookText));
        }
    }
}
