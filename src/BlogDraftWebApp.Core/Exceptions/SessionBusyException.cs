namespace BlogDraftWebApp.Core.Exceptions;

public sealed class SessionBusyException : Exception
{
    public SessionBusyException(string sessionId)
        : base("同一セッションで操作が実行中です")
    {
        SessionId = sessionId;
    }

    public string SessionId { get; }
}
