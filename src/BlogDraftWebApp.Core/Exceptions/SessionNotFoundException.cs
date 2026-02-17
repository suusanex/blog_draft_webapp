namespace BlogDraftWebApp.Core.Exceptions;

public sealed class SessionNotFoundException : Exception
{
    public SessionNotFoundException(string sessionId, bool isExpired = false)
        : base(isExpired ? "セッションの有効期限が切れています" : "セッションが見つかりません")
    {
        SessionId = sessionId;
        IsExpired = isExpired;
    }

    public string SessionId { get; }
    public bool IsExpired { get; }
}
